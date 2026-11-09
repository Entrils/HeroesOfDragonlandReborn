using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    public sealed class MultiplayerFortressProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerBootstrap bootstrap;
        private bool active, checkedStart, checkedEnd, subscribed;
        private TeamId attackingTeam;
        private float readyAt, endedAt, nextLog;
        private int endEvents;
        private NetworkFortressGuard[] guards;
        private NetworkKingObjective[] kings;
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-hodrFortress")
            { active = true; attackingTeam = args[i + 1] == "A" ? TeamId.TeamA : TeamId.TeamB; }
            if (active) Application.SetStackTraceLogType(UnityEngine.LogType.Log, StackTraceLogType.None);
        }
        private void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException("[FortressCheck] FAIL " + label); Debug.Log("[FortressCheck] PASS " + label); }
        private void Update()
        {
            if (!active) return;
            var runner = bootstrap.Runner;
            var match = GetComponent<NetworkFortressSpawner>().Match;
            if (runner == null || !runner.IsRunning || match == null || match.Object == null || !match.Object.IsValid) return;
            if (match.State != MatchPhase.Playing && match.State != MatchPhase.Finished) return;
            if (!subscribed)
            {
                subscribed = true;
                match.Ended += winner =>
                {
                    endEvents++;
                    if (runner.IsServer)
                    {
                        foreach (var king in kings)
                            Check(king.Stats.ApplyDamage(9999) == 0 && !match.TryFinish(king), "reentrant-finish-and-damage-blocked");
                    }
                    Debug.Log($"[FortressEndEvent] winner={winner} count={endEvents} server={runner.IsServer}");
                };
            }
            guards = FindObjectsByType<NetworkFortressGuard>().Where(g => g.GetComponent<NetworkEnemy>().Object != null && g.GetComponent<NetworkEnemy>().Object.IsValid).ToArray();
            kings = FindObjectsByType<NetworkKingObjective>().Where(k => k.Object != null && k.Object.IsValid).ToArray();
            var players = runner.ActivePlayers.Select(p => { runner.TryGetPlayerObject(p, out var obj); return obj; }).Where(p => p != null).ToArray();
            if (guards.Length != 2 || kings.Length != 2 || players.Length < 2) return;
            if (readyAt == 0) readyAt = Time.realtimeSinceStartup;
            foreach (var guard in guards)
            {
                var enemy = guard.GetComponent<NetworkEnemy>();
                if (enemy.TargetId != default && runner.TryFindObject(enemy.TargetId, out var target)
                    && !guard.IsEnemy(target.GetComponent<NetworkTestPlayer>())) Check(false, "guard-targeted-friendly");
            }
            if (!checkedStart && !match.MatchEnded)
            {
                checkedStart = true;
                Check(kings.All(k => k.Object.InputAuthority == PlayerRef.None) && guards.All(g => g.GetComponent<NetworkEnemy>().Object.InputAuthority == PlayerRef.None), "server-owned-fortresses");
                foreach (var guard in guards)
                {
                    var enemy = guard.GetComponent<NetworkEnemy>();
                    foreach (var player in players)
                    {
                        bool opponent = player.GetComponent<NetworkTestPlayer>().Team != guard.Team;
                        Check(guard.AllowsTarget(player.GetComponent<EnemyThreatTarget>()) == opponent, "guard-team-filter");
                        Check(guard.GetComponent<EnemyPerception>().CanTarget(player.GetComponent<EnemyThreatTarget>()) == (runner.IsServer && opponent), "authoritative-perception");
                    }
                    if (!runner.IsServer) Check(!enemy.CanSimulateEnemy && enemy.SimulationTicks == 0 && !guard.GetComponent<EnemyBrain>().enabled, "client-ai-passive");
                    else guard.GetComponent<DamageReceiver>().DamageTaken += (amount, source) => Debug.Log($"[FortressGuardHit] team={guard.Team} amount={amount} attacker={source.GetComponent<NetworkTestPlayer>()?.Team}");
                }
                foreach (var king in kings)
                {
                    float health = king.Stats.CurrentHealth;
                    foreach (var player in players)
                        Check(king.GetComponent<DamageReceiver>().TakeDamage(9999, player.gameObject) == 0, "king-forged-hit-rejected");
                    Check(king.Stats.CurrentHealth == health && !match.TryFinish(king), "living-king-cannot-end-match");
                    if (!runner.IsServer) Check(king.Stats.ApplyDamage(9999) == 0 && !king.Stats.CanMutate, "client-king-state-guard");
                    else king.GetComponent<DamageReceiver>().DamageTaken += (amount, source) => Debug.Log($"[FortressKingHit] team={king.Team} amount={amount} attacker={source.GetComponent<NetworkTestPlayer>()?.Team}");
                }
            }
            bootstrap.TestInput = Vector2.zero; bootstrap.TestAttack = false; bootstrap.TestDodge = false;
            if (!match.MatchEnded && Time.realtimeSinceStartup - readyAt > 3
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var local) && local != null
                && local.GetComponent<NetworkTestPlayer>().Team == attackingTeam)
            {
                var targetGuard = guards.FirstOrDefault(g => g.Team != attackingTeam && !g.GetComponent<NetworkCharacterStats>().Dead);
                Transform target = targetGuard != null ? targetGuard.transform : kings.First(k => k.Team != attackingTeam).transform;
                var delta = target.position - local.transform.position; delta.y = 0;
                bootstrap.TestFacing = new Vector2(delta.x, delta.z).normalized;
                bootstrap.TestInput = delta.magnitude > 1.25f ? bootstrap.TestFacing : Vector2.zero;
                bootstrap.TestAttack = delta.magnitude < 1.8f;
            }
            if (match.MatchEnded)
            {
                if (endedAt == 0) endedAt = Time.realtimeSinceStartup;
                if (!checkedEnd && Time.realtimeSinceStartup - endedAt > 2)
                {
                    checkedEnd = true;
                    Check(match.Winner == attackingTeam && match.FinishCount == 1 && !NetworkMatch.AllowsGameplay(runner), "one-replicated-winner");
                    Check(endEvents == (checkedStart ? 1 : 0), "end-notification-once");
                    Check(kings.First(k => k.Team != attackingTeam).GetComponent<NetworkCharacterStats>().Health == 0
                        && kings.First(k => k.Team == attackingTeam).GetComponent<NetworkCharacterStats>().Health == 1000, "only-opposing-king-defeated");
                    foreach (var king in kings)
                    {
                        Check(!match.TryFinish(king), "repeat-or-client-finish-rejected");
                        Check(king.Stats.ApplyDamage(1) == 0 && king.Stats.Heal(1) == 0, "post-end-health-frozen");
                    }
                    foreach (var player in players)
                    {
                        var stats = player.GetComponent<CharacterStats>();
                        Check(stats.ApplyDamage(1) == 0 && !player.GetComponent<MeleeAttack>().TryAttack(), "post-end-player-combat-blocked");
                        Check(!player.GetComponent<NetworkSessionProgression>().TryGrantExperience(100, "after-end"), "post-end-xp-blocked");
                        var progression = player.GetComponent<SessionProgression>();
                        Check(!progression.TryAddExperience(100, progression.SessionId, "direct-after-end", out _), "post-end-direct-xp-blocked");
                        Check(!player.GetComponent<Inventory>().TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "post-end-inventory-blocked");
                    }
                    foreach (var enemy in FindObjectsByType<NetworkEnemy>()) Check(!enemy.CanSimulateEnemy && !enemy.IsResolvingAttack && !enemy.AttackBusy, "post-end-ai-blocked");
                    foreach (var camp in FindObjectsByType<NetworkCamp>()) Check(!camp.CanMutateCamp && !camp.TryComplete(), "post-end-camp-blocked");
                    Debug.Log("[FortressComplete] server=" + runner.IsServer);
                }
            }
            if (Time.realtimeSinceStartup >= nextLog)
            {
                nextLog = Time.realtimeSinceStartup + 1;
                Debug.Log($"[FortressState] server={runner.IsServer} state={match.State} ended={match.MatchEnded} winner={match.Winner} count={match.FinishCount} kingA={kings.First(k => k.Team == TeamId.TeamA).GetComponent<NetworkCharacterStats>().Health} kingB={kings.First(k => k.Team == TeamId.TeamB).GetComponent<NetworkCharacterStats>().Health} events={endEvents}");
            }
        }
#endif
    }
}
