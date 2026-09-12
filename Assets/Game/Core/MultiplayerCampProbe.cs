using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Development acceptance driver. Combat uses owner input, never synthetic deaths/rewards.
    public sealed class MultiplayerCampProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerBootstrap bootstrap;
        private bool active, removed, complete, campGuard, chestGuard, reentryChecked;
        private string scenario;
        private float nextReport, clearedAt = -1;
        private readonly HashSet<NetworkId> playersObserved = new HashSet<NetworkId>();
        private readonly HashSet<NetworkId> enemiesObserved = new HashSet<NetworkId>();
        private readonly HashSet<NetworkId> xpGuards = new HashSet<NetworkId>();
        private NetworkCamp camp;
        private NetworkEnemy[] enemies = Array.Empty<NetworkEnemy>();
        private int completionEvents;
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-hodrCamps") { active = true; scenario = args[i + 1]; }
            if (active) { Application.SetStackTraceLogType(UnityEngine.LogType.Log, StackTraceLogType.None); Application.targetFrameRate = 60; }
        }
        private void Check(bool value, string label)
        {
            if (value) Debug.Log("[CampCheck] PASS " + label);
            else { Debug.LogError("[CampCheck] FAIL " + label); enabled = false; }
        }
        private void Update()
        {
            var runner = bootstrap.Runner;
            if (!active || runner == null || !runner.IsRunning) return;
            DriveOwner(runner);
            if (Time.realtimeSinceStartup < nextReport) return;
            nextReport = Time.realtimeSinceStartup + 0.5f;
            camp = FindObjectsByType<NetworkCamp>().FirstOrDefault(c => c.Object != null && c.Object.IsValid && c.Runner == runner);
            if (camp == null) return;
            enemies = FindObjectsByType<NetworkEnemy>().Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner).OrderBy(e => e.SpawnIndex).ToArray();
            if (!campGuard && camp.State == NetworkCampState.Active)
            {
                campGuard = true;
                Check(!camp.TryInitialize() && !camp.TryComplete() && camp.State == NetworkCampState.Active
                    && camp.CompletionCount == 0 && camp.ChestId == default, "live-camp-cannot-complete");
                if (runner.IsServer) camp.Cleared += value =>
                {
                    completionEvents++;
                    Check(!value.TryComplete() && completionEvents == 1, "reentrant-completion-rejected");
                    Debug.Log("[CampClearEvent] count=" + completionEvents);
                };
            }
            foreach (var enemy in enemies)
            {
                if (enemiesObserved.Add(enemy.Object.Id))
                {
                    int deaths = 0;
                    enemy.GetComponent<CharacterStats>().Died += () => Debug.Log($"[CampDeath] index={enemy.SpawnIndex} count={++deaths} server={runner.IsServer}");
                    if (runner.IsServer) enemy.GetComponent<DamageReceiver>().DamageTaken += (amount, source) =>
                        Debug.Log(FormattableString.Invariant($"[CampHit] index={enemy.SpawnIndex} amount={amount:F2} player={source.GetComponent<NetworkPlayerCombat>() != null}"));
                    else Check(!enemy.CanSimulateEnemy && !enemy.GetComponent<EnemyBrain>().enabled, "replica-enemy-passive index=" + enemy.SpawnIndex);
                }
                if (!runner.IsServer && enemy.SimulationTicks != 0) Check(false, "replica AI ticked");
            }
            foreach (var owner in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(owner, out var player) || player == null) continue;
                var progression = player.GetComponent<SessionProgression>();
                var net = player.GetComponent<NetworkSessionProgression>();
                int participant = player.GetComponent<NetworkTestPlayer>().ParticipantNumber;
                if (playersObserved.Add(player.Id))
                    progression.ExperienceGained += amount => Debug.Log($"[CampXPEvent] participant={participant} amount={amount} server={runner.IsServer}");
                if (!runner.IsServer && net.Revision > 0 && progression.IsInitialized && xpGuards.Add(player.Id))
                {
                    long xp = progression.CurrentXP, revision = progression.Revision;
                    progression.SetAuthority(true);
                    Check(!progression.TryAddExperience(999, progression.SessionId, "forged", out _)
                        && !progression.TryBeginSession("forged", out _)
                        && !progression.TryIncreaseAttribute(HeroesOfDragonland.Items.ItemStat.Strength, progression.SessionId, out _)
                        && !net.TryGrantExperience(999, "forged") && xp == progression.CurrentXP && revision == progression.Revision,
                        "replica-xp-guard participant=" + participant);
                }
                Debug.Log($"[CampXP] participant={participant} level={net.Level} xp={net.CurrentXP} points={net.Points} revision={net.Revision} mirror={progression.Level},{progression.CurrentXP},{progression.ProgressionPoints}");
            }
            var chests = FindObjectsByType<NetworkLootChest>().Where(c => c.Object != null && c.Object.IsValid && c.Runner == runner).ToArray();
            if (chests.Length > 0 && !chestGuard)
            {
                chestGuard = true;
                Check(chests.Length == 1 && chests[0].CampId == camp.Object.Id && chests[0].Object.InputAuthority == PlayerRef.None, "one-linked-server-chest");
                if (!runner.IsServer)
                {
                    var chest = chests[0].GetComponent<LootChest>(); chest.SetAuthority(true);
                    runner.TryGetPlayerObject(runner.LocalPlayer, out var local);
                    Check(!chest.HasAuthority && !chest.TryInteract(local.GetComponent<HeroesOfDragonland.InventorySystem.Inventory>(), out _), "replica-chest-guard");
                }
            }
            CheckInvariant(camp.State != NetworkCampState.Cleared || camp.DeadCount == camp.EnemyCount, "premature-clear");
            if (!runner.IsServer) CheckInvariant(!camp.CanMutateCamp && !camp.TryComplete() && !camp.TryInitialize(), "replica-camp-mutation");
            Debug.Log($"[CampState] server={runner.IsServer} id={camp.Object.Id} state={camp.State} dead={camp.DeadCount} total={camp.EnemyCount} completions={camp.CompletionCount} xp={camp.AwardedExperience} rewarded={camp.RewardedPlayers} chest={camp.ChestId} visible={chests.Length} players={runner.ActivePlayers.Count()}");
            if (scenario == "removed" && runner.IsServer && !removed && runner.ActivePlayers.Count() >= 2 && enemies.Length == 3)
            {
                removed = true;
                runner.Despawn(enemies[0].Object);
                clearedAt = Time.realtimeSinceStartup;
                Debug.Log("[CampRemoved] live-member-despawned");
            }
            if (scenario == "removed" && removed && !complete && Time.realtimeSinceStartup - clearedAt > 4)
            {
                Check(camp.State == NetworkCampState.Active && camp.DeadCount == 0 && camp.CompletionCount == 0 && chests.Length == 0, "despawn-is-not-death");
                complete = true; Debug.Log("[CampComplete] removed");
            }
            if (camp.State != NetworkCampState.Cleared || chests.Length != 1) return;
            if (clearedAt < 0) clearedAt = Time.realtimeSinceStartup;
            if (runner.IsServer && !reentryChecked)
            {
                reentryChecked = true;
                Check(!camp.TryComplete() && !camp.TryInitialize() && camp.CompletionCount == 1, "repeat-completion-rejected");
                foreach (var owner in runner.ActivePlayers)
                    if (runner.TryGetPlayerObject(owner, out var player))
                        Check(!player.GetComponent<NetworkSessionProgression>().TryGrantExperience(camp.AwardedExperience, "camp:" + camp.Object.Id), "duplicate-xp-rejected");
            }
            if (!complete && Time.realtimeSinceStartup - clearedAt > 3)
            { complete = true; Debug.Log("[CampComplete] cleared"); }
        }
        private void CheckInvariant(bool value, string label) { if (!value) Check(false, label); }
        private void DriveOwner(NetworkRunner runner)
        {
            bootstrap.TestInput = Vector2.zero; bootstrap.TestAttack = false; bootstrap.TestFacing = Vector2.zero;
            if (scenario != "combat" || runner.ActivePlayers.Count() < 6
                || !runner.TryGetPlayerObject(runner.LocalPlayer, out var player) || player == null
                || player.GetComponent<NetworkCharacterStats>().Dead) return;
            var actor = player.GetComponent<NetworkTestPlayer>();
            if (actor.Team != TeamId.TeamA || actor.ParticipantNumber > 6) return;
            var enemy = enemies.FirstOrDefault(e => e != null && !e.GetComponent<NetworkCharacterStats>().Dead);
            if (enemy == null) return;
            float angle = actor.SpawnLane * 120f * Mathf.Deg2Rad;
            Vector3 destination = enemy.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.1f;
            var delta = destination - player.transform.position;
            var facing = enemy.transform.position - player.transform.position;
            bootstrap.TestFacing = new Vector2(facing.x, facing.z).normalized;
            var move = new Vector2(delta.x, delta.z);
            bootstrap.TestInput = move.magnitude > 0.2f ? move.normalized : Vector2.zero;
            bootstrap.TestAttack = new Vector2(facing.x, facing.z).magnitude < 1.7f;
        }
#endif
    }
}
