using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine.AI;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Development-only scenario driver. Movement and attacks use the owning input path.
    public sealed class MultiplayerPveProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerBootstrap bootstrap;
        private string scenario;
        private float nextReport;
        private bool completed, disconnected;
        private readonly HashSet<NetworkEnemy> observed = new HashSet<NetworkEnemy>();
        private readonly HashSet<NetworkObject> playersObserved = new HashSet<NetworkObject>();
        private readonly HashSet<int> targeted = new HashSet<int>();
        private readonly Dictionary<int, int> deathTicks = new Dictionary<int, int>();
        private NetworkEnemy[] enemies = Array.Empty<NetworkEnemy>();
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-hodrPve") scenario = args[i + 1];
        }
        private void Check(bool value, string label)
        {
            if (value) Debug.Log("[PveCheck] PASS " + label);
            else { Debug.LogError("[PveCheck] FAIL " + label); enabled = false; }
        }
        private void Update()
        {
            var runner = bootstrap.Runner;
            if (string.IsNullOrEmpty(scenario) || runner == null || !runner.IsRunning) return;
            DriveOwner(runner);
            if (Time.realtimeSinceStartup < nextReport) return;
            nextReport = Time.realtimeSinceStartup + 0.2f;
            enemies = FindObjectsByType<NetworkEnemy>().Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner).ToArray();
            foreach (var peer in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(peer, out var player) || player == null || !playersObserved.Add(player)) continue;
                if (runner.IsServer) player.GetComponent<DamageReceiver>().DamageTaken += (amount, source) =>
                {
                    var attacker = source != null ? source.GetComponent<NetworkEnemy>() : null;
                    if (attacker != null) Debug.Log(FormattableString.Invariant($"[PveHit] kind=enemy index={attacker.SpawnIndex} victim={player.Id} sequence={attacker.AttackSequence} amount={amount:F2}"));
                };
            }
            foreach (var enemy in enemies)
            {
                var stats = enemy.GetComponent<CharacterStats>();
                var state = enemy.GetComponent<NetworkCharacterStats>();
                var brain = enemy.GetComponent<EnemyBrain>();
                if (observed.Add(enemy))
                {
                    int deaths = 0;
                    stats.Died += () => Debug.Log($"[PveDeath] index={enemy.SpawnIndex} count={++deaths} server={runner.IsServer}");
                    if (runner.IsServer)
                    {
                        brain.StateChanged += value =>
                        {
                            if (brain.AggroTarget != null) targeted.Add(enemy.SpawnIndex);
                            Debug.Log($"[PveTransition] index={enemy.SpawnIndex} state={value}");
                        };
                        enemy.GetComponent<DamageReceiver>().DamageTaken += (amount, source) =>
                        {
                            var attacker = source != null ? source.GetComponent<NetworkPlayerCombat>() : null;
                            if (attacker != null) Debug.Log(FormattableString.Invariant($"[PveHit] kind=player index={enemy.SpawnIndex} source={attacker.Object.Id} sequence={attacker.AttackSequence} amount={amount:F2}"));
                        };
                        var owner = runner.ActivePlayers.FirstOrDefault();
                        if (runner.TryGetPlayerObject(owner, out var player))
                            Check(enemy.GetComponent<DamageReceiver>().TakeDamage(9999, player.gameObject) == 0
                                && player.GetComponent<DamageReceiver>().TakeDamage(9999, enemy.gameObject) == 0,
                                "out-of-attack damage rejected index=" + enemy.SpawnIndex);
                        Check(stats.MaxHealth == enemy.Definition.Stats.MaxHealth, "definition health index=" + enemy.SpawnIndex);
                    }
                    else ValidateReplica(enemy, runner);
                }
                if (runner.IsServer && state.Dead)
                {
                    if (!deathTicks.ContainsKey(enemy.SpawnIndex)) deathTicks.Add(enemy.SpawnIndex, enemy.SimulationTicks);
                    else if (deathTicks[enemy.SpawnIndex] != enemy.SimulationTicks) Check(false, "dead AI kept ticking");
                }
                if (scenario == "disconnect" && runner.IsServer && targeted.Contains(enemy.SpawnIndex)
                    && brain.AggroTarget == null && brain.State == EnemyState.Idle
                    && Vector3.Distance(enemy.transform.position, brain.SpawnPosition) < 0.6f && !completed)
                { completed = true; Debug.Log("[PveReturn] index=" + enemy.SpawnIndex + " arrived=True"); }
                var view = enemy.GetComponent<NetworkEnemyPresentation>();
                Debug.Log(FormattableString.Invariant($"[PveState] index={enemy.SpawnIndex} id={enemy.Object.Id} server={runner.IsServer} hp={state.Health:F2} max={state.MaxHealth:F2} dead={state.Dead} state={enemy.State} target={enemy.TargetId} attack={enemy.AttackSequence} tick={enemy.AttackStartTick} dt={runner.DeltaTime:F6} simulation={enemy.SimulationTicks} agent={enemy.GetComponent<NavMeshAgent>().enabled} view={view.AttackVisible} x={enemy.transform.position.x:F3} z={enemy.transform.position.z:F3}"));
            }
            Debug.Log($"[PveSession] players={runner.ActivePlayers.Count()} enemies={enemies.Length}");
            if (scenario == "combat" && enemies.Length == 3 && enemies.All(e => e.GetComponent<NetworkCharacterStats>().Dead) && !completed)
            { completed = true; Debug.Log("[PveComplete] all-dead count=3"); }
        }
        private void ValidateReplica(NetworkEnemy enemy, NetworkRunner runner)
        {
            var brain = enemy.GetComponent<EnemyBrain>();
            var attack = enemy.GetComponent<EnemyMeleeAttack>();
            var nav = enemy.GetComponent<EnemyNavigation>();
            var perception = enemy.GetComponent<EnemyPerception>();
            var stats = enemy.GetComponent<CharacterStats>();
            bool safe = !brain.enabled && !attack.enabled && !nav.enabled && !perception.enabled
                && !enemy.GetComponent<NavMeshAgent>().enabled && !brain.HasAuthority && enemy.SimulationTicks == 0;
            var previous = brain.State;
            var rotation = enemy.transform.rotation;
            float health = stats.CurrentHealth;
            brain.enabled = attack.enabled = nav.enabled = perception.enabled = true;
            brain.SetAuthority(true); brain.Tick(100);
            nav.Face(enemy.transform.position + Vector3.right, 1);
            safe &= brain.State == previous && enemy.transform.rotation == rotation
                && !nav.MoveTo(enemy.transform.position + Vector3.right, 100, 0)
                && perception.FindTarget(100, enemy.transform.position, 100) == null;
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var player))
            {
                safe &= !attack.TryStart(player.GetComponent<EnemyThreatTarget>());
                safe &= enemy.GetComponent<DamageReceiver>().TakeDamage(10000, player.gameObject) == 0;
            }
            attack.Tick(100);
            stats.ApplyDamage(10000); stats.Heal(10000); stats.ResetToFull();
            safe &= stats.CurrentHealth == health && !attack.IsBusy;
            brain.enabled = attack.enabled = nav.enabled = perception.enabled = false;
            Check(safe, "replica-guards index=" + enemy.SpawnIndex);
        }
        private void DriveOwner(NetworkRunner runner)
        {
            bootstrap.TestInput = Vector2.zero; bootstrap.TestAttack = false; bootstrap.TestFacing = Vector2.zero;
            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var player) || player == null
                || player.GetComponent<NetworkCharacterStats>().Dead) return;
            int participant = player.GetComponent<NetworkTestPlayer>().ParticipantNumber;
            NetworkEnemy enemy;
            if (scenario == "disconnect")
            {
                if (participant != 2 || disconnected) return;
                enemy = enemies.FirstOrDefault(e => e != null && e.SpawnIndex == 1);
                if (enemy != null && enemy.TargetId == player.Id)
                {
                    disconnected = true;
                    Debug.Log("[PveDisconnect] targeted owner leaving");
                    _ = bootstrap.Disconnect();
                    return;
                }
            }
            else
            {
                if (runner.ActivePlayers.Count() < 6 || participant > 3) return;
                enemy = enemies.FirstOrDefault(e => e != null && e.SpawnIndex == participant - 1 && !e.GetComponent<NetworkCharacterStats>().Dead)
                    ?? enemies.FirstOrDefault(e => e != null && !e.GetComponent<NetworkCharacterStats>().Dead);
            }
            if (enemy == null) return;
            Vector3 delta = enemy.transform.position - player.transform.position;
            var move = new Vector2(delta.x, delta.z);
            bootstrap.TestFacing = move.normalized;
            bootstrap.TestInput = move.magnitude > 1.05f ? move.normalized : Vector2.zero;
            bootstrap.TestAttack = scenario == "combat" && enemy.AttackSequence >= 2 && move.magnitude < 1.7f;
        }
#endif
    }
}
