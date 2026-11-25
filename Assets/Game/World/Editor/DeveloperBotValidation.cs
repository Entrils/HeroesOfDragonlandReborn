#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using HeroesOfDragonland.Items;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.World.Camps;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class DeveloperBotValidation
    {
        public static string ScenarioResult { get; private set; } = "Not run";
        private static void Check(bool value, string message) { if (!value) throw new Exception("Bot validation: " + message); }
        public static void Start()
        {
            Check(Application.isPlaying, "Use Play Mode");
            var service = UnityEngine.Object.FindAnyObjectByType<DeveloperBotService>();
            Check(!service.SetBotCount(-1) && !service.SetBotCount(6), "Reject out of range counts");
            Check(service.SetBotCount(5), "Accept five bots");
            service.StartLocalMatch();
        }
        public static string Report()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var report = new System.Text.StringBuilder(bootstrap.Status + "\n");
            if (!bootstrap.Running) return report.ToString();
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            report.AppendLine("phase=" + match.State + " A=" + match.TeamACount + " B=" + match.TeamBCount + " preparation=" + match.PreparationError);
            foreach (var actor in bootstrap.Avatars)
            {
                var player = actor.GetComponent<NetworkTestPlayer>(); var brain = player.BotBrain;
                report.AppendLine(player.name + " pos=" + player.transform.position + " hp=" + actor.GetComponent<CharacterStats>().CurrentHealth
                    + " attack=" + actor.GetComponent<NetworkPlayerCombat>().AttackSequence + " dodge=" + actor.GetComponent<NetworkPlayerDodge>().DodgeSequence
                    + " level=" + brain?.Level + " quality=" + brain?.EquipmentQuality + " potions=" + brain?.ConsumablesUsed
                    + " abilities=" + brain?.AbilitiesUsed + " goal=" + brain?.Goal + " distance=" + brain?.DistanceTravelled + " chests=" + brain?.ChestOpens + " chestResult=" + brain?.LastChestResult);
            }
            return report.ToString();
        }
        public static void AssertRoster()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var service = bootstrap.GetComponent<DeveloperBotService>();
            var actors = bootstrap.Avatars.ToArray();
            Check(bootstrap.Running && service.SpawnedCount == 5 && actors.Length == 6, "One human plus five bots");
            Check(actors.Count(a => a.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA) == 3
                && actors.Count(a => a.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamB) == 3, "Balanced 3v3");
            foreach (var actor in service.Avatars)
            {
                Check(actor.InputAuthority == PlayerRef.None && actor.GetComponent<NetworkTestPlayer>().IsBot, "Server owned bot");
                Check(NetworkParticipant.IsAdmitted(actor) && actor.GetComponent<NetworkInventory>().IsAdmitted
                    && actor.GetComponent<NetworkSessionProgression>().SessionId.ToString().Length > 0, "Bots admitted and progression initialized");
                var admit = typeof(NetworkInventory).GetMethod("AdmitRequest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Check(!(bool)admit.Invoke(actor.GetComponent<NetworkInventory>(), new object[] { PlayerRef.None }), "Bots reject connectionless RPC requests");
            }
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            Check(match.State == MatchPhase.Playing && match.TeamACount == 3 && match.TeamBCount == 3, "Bots satisfy lobby and start match");
            Debug.Log("[BotValidation] PASS live solo 3v3 admission, balanced roster, lobby, progression and request authority.");
        }

        [MenuItem("Heroes of Dragonland/Bots/Validate Advanced Bots")]
        public static async void StartAdvanced()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            Check(Application.isPlaying && bootstrap != null && !bootstrap.Running && !bootstrap.Busy, "Idle Battlefield Play Mode");
            var service = bootstrap.GetComponent<DeveloperBotService>();
            int previousCount = service.RequestedBots;
            ScenarioResult = "RUNNING advanced setup";
            try
            {
                service.SetBotCount(5);
                await bootstrap.StartSession(GameMode.Single, "m88-advanced-bots");
                var spawner = bootstrap.GetComponent<NetworkFortressSpawner>();
                await Wait(() => spawner.Match != null && spawner.Match.State == MatchPhase.Lobby
                    && spawner.Match.TeamACount + spawner.Match.TeamBCount == 6, "Balanced lobby");
                var match = spawner.Match;
                match.RPC_SetReady(true);
                await Wait(() => match.CanStartLobby, "Ready acknowledgement");
                Check(match.TryStartLobby(), "Start live round");
                await Wait(() => match.CanPlay, "Round playing");
                await Wait(() => service.Avatars.Count(actor => actor.GetComponent<NetworkTestPlayer>().BotBrain.DistanceTravelled > 2) >= 3,
                    "Unscripted movement from three bots");
                Debug.Log("[BotValidation] PASS unscripted opening travel.");
                StartScenarios();
                await Wait(() => !ScenarioResult.StartsWith("RUNNING"), "Advanced scenarios complete", 60);
            }
            catch (Exception error) { ScenarioResult = "FAILED: " + error.Message; Debug.LogException(error); }
            finally { if (bootstrap != null) await bootstrap.Disconnect(); if (service != null) service.SetBotCount(previousCount); }
        }

        private static async Task Wait(Func<bool> ready, string message, int seconds = 12)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (Application.isPlaying && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (ready()) return;
                await Task.Delay(40);
            }
            Check(false, message);
        }

        public static void StartScenarios()
        {
            AssertRoster();
            ScenarioResult = "RUNNING";
            UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>().StartCoroutine(Scenarios());
        }
        public static void StartLifecycle()
        {
            AssertRoster(); ScenarioResult = "RUNNING lifecycle";
            UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>().StartCoroutine(Lifecycle());
        }
        private static IEnumerator Lifecycle()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var service = bootstrap.GetComponent<DeveloperBotService>();
            try
            {
                for (int count = 0; count <= 5; count++)
                {
                    Check(service.SetBotCount(count), "Accept count " + count);
                    yield return new WaitForSeconds(.6f);
                    Check(service.SpawnedCount == count && bootstrap.Avatars.Count() == count + 1, "Exact count " + count);
                    Check(bootstrap.Avatars.Count(a => a.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA) <= 3
                        && bootstrap.Avatars.Count(a => a.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamB) <= 3, "Capacity " + count);
                }
                AssertRoster();
                var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
                int round = match.RoundNumber;
                var oldIds = service.Avatars.Select(a => a.Id).ToArray();
                var king = UnityEngine.Object.FindObjectsByType<NetworkKingObjective>().First(k => k.Object.IsValid && k.Runner == bootstrap.Runner);
                king.Stats.ApplyDamage(100000);
                Check(match.State == MatchPhase.Finished && !NetworkMatch.AllowsGameplay(bootstrap.Runner), "King death closes gameplay");
                ScenarioResult = "RUNNING lifecycle: all counts passed, waiting for round reset";
                yield return new WaitForSeconds(match.Configuration.FinishedDelay + match.Configuration.ReturningDelay + match.Configuration.StartingDelay + 3);
                AssertRoster();
                Check(match.RoundNumber > round && service.Avatars.All(a => !oldIds.Contains(a.Id)), "Round reset recreates bots without duplicate participants");
                ScenarioResult = "RUNNING lifecycle: round reset passed, checking reconnect";
                var disconnect = bootstrap.Disconnect();
                while (!disconnect.IsCompleted) yield return null;
                Check(!bootstrap.Running && service.SpawnedCount == 0 && !bootstrap.Avatars.Any(), "Disconnect cleans avatars and roster");
                service.StartLocalMatch();
                float until = Time.realtimeSinceStartup + 15;
                while (Time.realtimeSinceStartup < until)
                {
                    var nextMatch = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
                    if (bootstrap.Running && nextMatch != null && nextMatch.Object.IsValid && nextMatch.State == MatchPhase.Playing) break;
                    yield return null;
                }
                AssertRoster();
                ScenarioResult = "PASS lifecycle: every count 0–5, capacity, King finish, round reset with fresh bots, disconnect cleanup and reconnect to balanced 3v3.";
                Debug.Log("[BotValidation] " + ScenarioResult);
            }
            finally { if (ScenarioResult.StartsWith("RUNNING")) ScenarioResult = "FAILED: " + ScenarioResult + "; see Console"; }
        }
        private static void Reconsider(NetworkTestPlayer bot)
        {
            typeof(DeveloperBotBrain).GetField("think", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bot.BotBrain, 0f);
        }
        private static void Place(NetworkObject actor, Vector3 position)
        {
            actor.GetComponent<NetworkTransform>().Teleport(position, Quaternion.identity);
            Physics.SyncTransforms();
        }
        private static IEnumerator Scenarios()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var service = bootstrap.GetComponent<DeveloperBotService>();
            var human = bootstrap.Avatars.First(a => !a.GetComponent<NetworkTestPlayer>().IsBot);
            var runner = bootstrap.Runner;
            GameObject screen = null;
            try
            {
                var old = service.Avatars.ToArray();
                Check(service.SetBotCount(0) && !service.Avatars.Any() && bootstrap.Avatars.Count() == 1, "Remove all five bots");
                foreach (var actor in old) Check(!NetworkParticipant.IsAdmitted(actor), "Removed bots lose admission");
                Check(service.SetBotCount(1), "Spawn one bot"); yield return new WaitForSeconds(1);
                var botObject = service.Avatars.Single(); var bot = botObject.GetComponent<NetworkTestPlayer>();
                Check(bot.Team != human.GetComponent<NetworkTestPlayer>().Team, "Replacement balances human team");
                Place(human, new Vector3(0, 1, -40));
                var neutral = UnityEngine.Object.FindObjectsByType<NetworkEnemy>()
                    .First(e => e.Object != null && e.Object.IsValid && e.Runner == runner && e.GetComponent<NetworkFortressGuard>() == null
                        && e.GetComponent<CharacterStats>().MaxHealth <= 40 && !e.GetComponent<CharacterStats>().IsDead);
                Place(botObject, neutral.transform.position + Vector3.up + Vector3.back * 1.5f); Reconsider(bot);
                float health = neutral.GetComponent<CharacterStats>().CurrentHealth;
                yield return new WaitForSeconds(5);
                Check(neutral.GetComponent<CharacterStats>().CurrentHealth < health, "Bot attacks neutral camp through normal combat");
                ScenarioResult = "RUNNING: camp combat passed";
                // Isolated chest fixture; no direct inventory changes are used.
                var chestPrefab = AssetDatabase.LoadAssetAtPath<NetworkLootChest>(BattlefieldSceneBuilder.Folder + "/LowChest.prefab");
                var chest = runner.Spawn(chestPrefab, new Vector3(0, 0, 34), Quaternion.identity, PlayerRef.None);
                botObject.GetComponent<CharacterStats>().ResetToFull();
                Place(botObject, new Vector3(0, 1, 32.5f)); Reconsider(bot);
                yield return new WaitForSeconds(5);
                Check(chest.IsOpen && chest.OpenedByBot == botObject.Id, "Bot opens chest through existing claim and reach rules; position="
                    + bot.transform.position + " goal=" + bot.BotBrain.Goal + " chest=" + chest.IsOpen + " last=" + bot.BotBrain.LastChestResult);
                Check(!chest.GetComponent<HeroesOfDragonland.World.LootChest>().TryInteract(null, out _), "Chest rejects invalid claimant");
                runner.Despawn(chest.Object);
                ScenarioResult = "RUNNING: camp and chest passed";
                // A wall blocks perception even when both actors are within the spotting radius.
                Place(human, new Vector3(0, 1, 34)); Place(botObject, new Vector3(0, 1, 31));
                var observe = typeof(DeveloperBotBrain).GetMethod("CanObserve", BindingFlags.Instance | BindingFlags.NonPublic);
                Check((bool)observe.Invoke(bot.BotBrain, new object[] { human.transform }), "Opponent visible in clear sight");
                screen = new GameObject("Bot visibility fixture");
                screen.transform.position = new Vector3(0, 2, 32.5f);
                screen.AddComponent<BoxCollider>().size = new Vector3(5, 5, .35f);
                Physics.SyncTransforms();
                Check(!(bool)observe.Invoke(bot.BotBrain, new object[] { human.transform }), "Wall hides the enemy");
                screen.SetActive(false);
                UnityEngine.Object.Destroy(screen);
                screen = null;
                Physics.SyncTransforms();

                var botHealth = botObject.GetComponent<CharacterStats>();
                botHealth.ResetToFull();
                for (int attempt = 0; attempt < 5 && botHealth.CurrentHealth / botHealth.MaxHealth > .22f; attempt++)
                    botHealth.ApplyDamage(botHealth.CurrentHealth * .74f);
                var potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/HealthPotion.asset");
                Check(potion != null && botObject.GetComponent<NetworkInventory>().TryAddItem(potion.Id), "Seed real health potion");
                Reconsider(bot);
                int used = bot.BotBrain.ConsumablesUsed;
                float itemDeadline = Time.realtimeSinceStartup + 3;
                bool retreated = false;
                while (Time.realtimeSinceStartup < itemDeadline && (bot.BotBrain.ConsumablesUsed == used || !retreated))
                {
                    retreated |= bot.BotBrain.Goal == DeveloperBotGoal.Retreat;
                    yield return null;
                }
                Check(retreated && bot.BotBrain.ConsumablesUsed > used, "Low health triggers retreat and normal potion use");
                ScenarioResult = "RUNNING: camp, chest, sight, retreat and consumable passed";
                // Place opposing avatars close together, then let generated inputs resolve PvP.
                human.GetComponent<CharacterStats>().ResetToFull(); botObject.GetComponent<CharacterStats>().ResetToFull();
                Place(human, new Vector3(0, 1, 34)); Place(botObject, new Vector3(0, 1, 32.6f)); Reconsider(bot);
                health = human.GetComponent<CharacterStats>().CurrentHealth;
                yield return new WaitForSeconds(4);
                Check(human.GetComponent<CharacterStats>().CurrentHealth < health, "Bot identifies and attacks enemy player");
                var progression = botObject.GetComponent<NetworkSessionProgression>();
                Check(progression.TryGrantExperience(4000, "m88-bot-ability-fixture"), "Grant normal progression for ability check");
                float abilityDeadline = Time.realtimeSinceStartup + 9;
                while (Time.realtimeSinceStartup < abilityDeadline && bot.BotBrain.AbilitiesUsed == 0)
                {
                    if (human.GetComponent<CharacterStats>().CurrentHealth < 35)
                        human.GetComponent<CharacterStats>().ResetToFull();
                    yield return null;
                }
                Check(bot.BotBrain.AbilitiesUsed > 0, "Bot learns and uses a legal skill");
                Place(human, new Vector3(0, 1, -40));
                botObject.GetComponent<CharacterStats>().ResetToFull();
                var guard = UnityEngine.Object.FindObjectsByType<NetworkFortressGuard>()
                    .First(g => g.IsEnemy(bot) && g.GetComponent<NetworkEnemy>().Object.IsValid);
                var guardStats = guard.GetComponent<CharacterStats>();
                Place(botObject, guard.transform.position + Vector3.up + Vector3.back * 1.4f); Reconsider(bot);
                health = guardStats.CurrentHealth;
                yield return new WaitForSeconds(4);
                Check(guardStats.CurrentHealth < health, "Bot attacks enemy guard, goal=" + bot.BotBrain.Goal
                    + " position=" + bot.transform.position + " guard=" + guard.transform.position);
                ScenarioResult = "RUNNING: camp, chest, PvP and guard passed";
                // Clear the guard as scenario arrangement; King damage still comes from bot attacks.
                foreach (var defender in UnityEngine.Object.FindObjectsByType<NetworkFortressGuard>())
                    if (defender.IsEnemy(bot) && defender.GetComponent<NetworkEnemy>().Runner == runner)
                        defender.GetComponent<CharacterStats>().ApplyDamage(100000);
                var king = UnityEngine.Object.FindObjectsByType<NetworkKingObjective>()
                    .First(k => k.Team != bot.Team && k.Object.IsValid);
                botObject.GetComponent<CharacterStats>().ResetToFull();
                Place(botObject, king.transform.position + Vector3.up + Vector3.back * 1.8f); Reconsider(bot);
                health = king.Stats.CurrentHealth;
                yield return new WaitForSeconds(4);
                Check(king.Stats.CurrentHealth < health, "Bot attacks opposing King through objective damage policy");
                int respawns = botObject.GetComponent<NetworkPlayerRespawn>().RespawnSequence;
                botObject.GetComponent<CharacterStats>().ApplyDamage(100000);
                yield return new WaitForSeconds(8);
                Check(botObject.GetComponent<NetworkPlayerRespawn>().RespawnSequence > respawns
                    && !botObject.GetComponent<CharacterStats>().IsDead, "Bot uses normal respawn");
                Check(service.SetBotCount(5), "Refill to five bots"); yield return new WaitForSeconds(2);
                AssertRoster();
                ScenarioResult = "PASS: 0/1/5 counts, camp combat, chest, blocked sight, retreat, potion use, PvP, ability use, guard and King damage, and respawn.";
                Debug.Log("[BotValidation] " + ScenarioResult);
            }
            finally
            {
                if (screen != null) UnityEngine.Object.Destroy(screen);
                if (ScenarioResult.StartsWith("RUNNING")) ScenarioResult = "FAILED: " + ScenarioResult + "; see Console";
            }
        }
    }
}
#endif

