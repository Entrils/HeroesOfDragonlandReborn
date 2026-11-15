using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Reflection;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Explicit development acceptance only. Production values and match rules are unchanged.
    public sealed class MatchPacingNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("-hodrPacingCheck"))
                FindAnyObjectByType<MultiplayerBootstrap>()?.gameObject.AddComponent<MatchPacingNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private NetworkMatch match;
        private NetworkObject attacker, victim, helper;
        private NetworkCamp camp;
        private CharacterStats target;
        private int stage, firstRound;
        private float started = -1, changed, nextCast;
        private long attackerXP, helperXP, victimXP;
        private bool initialized, complete, clientObserved, replicaDenied;
        private static readonly Vector3 Duel = new(0, 1, 22);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("[PacingNetwork] FAIL " + name); }
        private static void Place(NetworkObject actor, Vector3 position, Vector3 facing) => actor.GetComponent<NetworkTransform>().Teleport(position, Quaternion.LookRotation(facing));
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void Next(int next) { stage = next; changed = Time.realtimeSinceStartup; }
        private void LateUpdate()
        {
            if (complete) return;
            var runner = bootstrap.Runner; match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (runner == null || !runner.IsRunning || match == null || match.Object == null || !match.Object.IsValid) return;
            if (started >= 0 && Time.realtimeSinceStartup - started > 160) { complete = true; Check(false, "timeout stage=" + stage); }
            if (!match.CanPlay) return;
            var humans = FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 2) return;
            if (!initialized)
            {
                initialized = true; started = changed = Time.realtimeSinceStartup; firstRound = match.RoundNumber;
                attacker = humans.Single(p => p.Team == TeamId.TeamA).Object; victim = humans.Single(p => p.Team == TeamId.TeamB).Object;
                Check(match.Balance != null && match.GetComponent<NetworkMatchPacing>() != null, "central balance replicated prefab");
                if (runner.IsServer)
                {
                    foreach (var enemy in FindObjectsByType<NetworkEnemy>().Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner)) enemy.GetComponent<EnemyBrain>().enabled = false;
                    helper = bootstrap.Avatars.First(a => a.GetComponent<NetworkTestPlayer>().IsBot && a.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA);
                    foreach (var bot in bootstrap.Avatars.Where(a => a.GetComponent<NetworkTestPlayer>().IsBot))
                    { bot.GetComponent<NetworkTestPlayer>().enabled = false; Place(bot, new Vector3(-5, 1, -22), Vector3.forward); }
                    foreach (var actor in bootstrap.Avatars.Where(a => a.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamB))
                        actor.GetComponent<NetworkSessionProgression>().TryGrantExperience(100, "pacing-level-seed");
                    Check(attacker.GetComponent<SessionSkills>().TryLearn(3, attacker.GetComponent<SessionSkills>().SessionId, out _), "learn normal projectile");
                    Place(attacker, Duel - Vector3.forward * 4, Vector3.forward); Place(victim, Duel, Vector3.back); Place(helper, Duel - Vector3.forward * 1.5f, Vector3.forward);
                    attackerXP = attacker.GetComponent<SessionProgression>().CurrentXP; helperXP = helper.GetComponent<SessionProgression>().CurrentXP; victimXP = victim.GetComponent<SessionProgression>().CurrentXP;
                }
            }
            if (!runner.IsServer)
            {
                if (!replicaDenied)
                {
                    var progression = victim.GetComponent<SessionProgression>();
                    if (progression.IsInitialized) { Check(!progression.TryAddExperience(999, progression.SessionId, "forged", out _) && !progression.TryApplyDeathLoss(999, progression.SessionId, "forged-death", out _), "replica XP and death loss denied"); replicaDenied = true; }
                }
                if (match.Balance.IsLate(match.PlayingSeconds))
                {
                    Check(replicaDenied && match.GetComponent<NetworkMatchPacing>().CampMultiplier(TeamId.TeamA) > 1, "visible replicated catch-up");
                    Check(attacker.GetComponent<NetworkPlayerExperienceRewards>().Kills == 1, "replicated kill reward count");
                    Check(FindObjectsByType<NetworkPlayerExperienceRewards>().Any(p => p.Object != null && p.Object.IsValid && p.Runner == runner && p.Assists == 1), "replicated assist");
                    clientObserved = true;
                }
                if (clientObserved && match.RoundNumber > firstRound) Finish(false);
            }
            else ServerTick();
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
            {
                bootstrap.TestInput = Vector2.zero; bootstrap.TestFacing = Vector2.up;
                bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestBlock = bootstrap.TestParry = bootstrap.TestDodge = false;
                bool cast = local == attacker && (stage == 2 || stage == 4);
                var skills = local.GetComponent<SessionSkills>();
                if (cast && Time.realtimeSinceStartup >= nextCast && skills.IsLearned(3) && skills.CooldownRemaining(3) <= 0)
                { local.GetComponent<NetworkSessionSkills>().RequestActivate(3); nextCast = Time.realtimeSinceStartup + 1; }
            }
        }
        private void ServerTick()
        {
            float age = Time.realtimeSinceStartup - changed;
            var config = match.Balance; var runner = match.Runner;
            switch (stage)
            {
                case 0 when age > 2:
                    Check(match.GetComponent<NetworkMatchPacing>().CampMultiplier(TeamId.TeamA) > 1, "behind team earns camp catch-up");
                    helper.GetComponent<NetworkPlayerCombat>().TickIntent(new NetworkPlayerInput { LightAttackPress = 1 }, Vector3.forward, false); Next(1); break;
                case 1:
                    if (victim.GetComponent<CharacterStats>().CurrentHealth >= victim.GetComponent<CharacterStats>().MaxHealth) break;
                    Place(helper, Duel + Vector3.left * 6, Vector3.forward);
                    var health = victim.GetComponent<CharacterStats>(); health.ApplyDamage(health.CurrentHealth - 10); Next(2); break;
                case 2:
                    if (attacker.GetComponent<NetworkPlayerExperienceRewards>().Kills != 1) break;
                    Check(helper.GetComponent<NetworkPlayerExperienceRewards>().Assists == 1, "confirmed melee assist");
                    Check(attacker.GetComponent<SessionProgression>().CurrentXP == attackerXP + config.KillExperience, "kill XP has no comeback multiplier");
                    Check(helper.GetComponent<SessionProgression>().CurrentXP == helperXP + config.AssistExperiencePool, "shared assist pool");
                    Check(victim.GetComponent<SessionProgression>().CurrentXP == victimXP && victim.GetComponent<SessionProgression>().Level == 2, "death retains XP/level");
                    var respawn = victim.GetComponent<NetworkPlayerRespawn>();
                    Check(respawn.WaitingForRespawn && respawn.SecondsUntilRespawn <= config.RespawnDelay(match.PlayingSeconds, 2) && respawn.SecondsUntilRespawn > 0, "scaled authoritative respawn");
                    Next(3); break;
                case 3:
                    if (victim.GetComponent<CharacterStats>().IsDead || victim.GetComponent<NetworkPlayerRespawn>().IsSpawnProtected) break;
                    Place(victim, Duel, Vector3.back); victim.GetComponent<CharacterStats>().ApplyDamage(victim.GetComponent<CharacterStats>().CurrentHealth - 10); Next(4); break;
                case 4:
                    if (!victim.GetComponent<CharacterStats>().IsDead || age < 1) break;
                    Check(attacker.GetComponent<NetworkPlayerExperienceRewards>().Kills == 1 && attacker.GetComponent<SessionProgression>().CurrentXP == attackerXP + config.KillExperience, "repeat victim gives no farm XP");
                    camp = FindObjectsByType<NetworkCamp>().First(c => c.Runner == runner && c.Definition.Difficulty == CampDifficulty.Tier1 && c.State == NetworkCampState.Active);
                    var enemies = FindObjectsByType<NetworkEnemy>().Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner && Vector3.Distance(e.transform.position, camp.transform.position) < 5).ToArray();
                    Check(enemies.Length == camp.EnemyCount, "camp fixture members");
                    foreach (var enemy in enemies) enemy.GetComponent<EnemyBrain>().enabled = false;
                    target = enemies[0].GetComponent<CharacterStats>(); target.ApplyDamage(target.CurrentHealth - 1);
                    foreach (var enemy in enemies.Skip(1)) enemy.GetComponent<CharacterStats>().ApplyDamage(10000);
                    Place(helper, target.transform.position - Vector3.forward * 1.5f + Vector3.up, Vector3.forward);
                    Place(attacker, target.transform.position + new Vector3(3, 1, 0), Vector3.forward);
                    helperXP = helper.GetComponent<SessionProgression>().CurrentXP;
                    helper.GetComponent<NetworkPlayerCombat>().TickIntent(new NetworkPlayerInput { LightAttackPress = 2 }, Vector3.forward, false);
                    Next(5); break;
                case 5:
                    if (camp.State != NetworkCampState.Cleared) break;
                    int expected = Mathf.RoundToInt(camp.AvailableExperience * match.GetComponent<NetworkMatchPacing>().CampMultiplier(TeamId.TeamA));
                    Check(camp.RewardedPlayers == 1 && helper.GetComponent<SessionProgression>().CurrentXP == helperXP + expected, "participating nearby player earns catch-up camp XP");
                    Check(attacker.GetComponent<SessionProgression>().CurrentXP == attackerXP + config.KillExperience, "nearby nonparticipant gets no camp XP");
                    Check(!camp.TryComplete() && camp.CompletionCount == 1, "camp reward idempotence");
                    typeof(NetworkMatch).GetProperty("PlayingSeconds").SetValue(match, config.LateMatchStartsAtSeconds + 1);
                    foreach (var objective in FindObjectsByType<NetworkCamp>().Where(c => c.Runner == runner))
                    { Check(config.TryGetCamp(objective.Definition.Difficulty, out var profile) && objective.AvailableExperience == profile.Experience(true), "late objective value"); }
                    Next(6); break;
                case 6 when age > 22:
                    Check(runner.TryFindObject(match.TeamAKingId, out var king), "King exists"); king.GetComponent<CharacterStats>().ApplyDamage(100000); Next(7); break;
                case 7:
                    if (match.RoundNumber <= firstRound) break;
                    Check(bootstrap.Avatars.All(a => a.GetComponent<NetworkPlayerExperienceRewards>().Kills == 0 && a.GetComponent<NetworkPlayerExperienceRewards>().Assists == 0 && a.GetComponent<SessionProgression>().Level == 1), "round reset clears rewards/progression");
                    Finish(true); break;
            }
        }
        private void Finish(bool server) { complete = true; Debug.Log("[PacingNetwork] PASS authoritative kill/assist XP, repeat victim denial, death retention, scaled respawn, contributor camp XP, visible catch-up, late opportunity and round reset; server=" + server); }
        private void OnDisable()
        {
            if (bootstrap == null) return; bootstrap.TestInput = bootstrap.TestFacing = null;
            bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestBlock = bootstrap.TestParry = bootstrap.TestDodge = null;
        }
#endif
    }
}
