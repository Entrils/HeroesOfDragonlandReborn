using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;

internal class CommandScript : IRunCommand
{
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); }
    public void Execute(ExecutionResult result)
    {
        Check(Application.isPlaying, "Run in Play Mode in FortressGuardTest");
        Check(NavMesh.SamplePosition(Vector3.zero, out var sample, 1, NavMesh.AllAreas), "test NavMesh");
        var home = sample.position;
        GameObject guard = null, targetObject = null, wall = null;
        try
        {
            guard = new GameObject("Guard validation"); guard.SetActive(false); guard.transform.position = home;
            var guardTeam = guard.AddComponent<TeamIdentity>(); guardTeam.TryConfigure(TeamId.TeamA, CombatantKind.NPC);
            guard.AddComponent<FortressGuard>();
            var brain = guard.GetComponent<EnemyBrain>();
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Game/Data/FortressTest/FortressGuard.asset");
            Check(brain.TryConfigureSpawn(definition, true), "inactive guard configuration");
            var collider = guard.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2;
            guard.SetActive(true);
            targetObject = new GameObject("Team validation player"); targetObject.transform.position = home + Vector3.forward * 1.2f;
            var targetTeam = targetObject.AddComponent<TeamIdentity>(); targetTeam.TryConfigure(TeamId.TeamA, CombatantKind.Player);
            var target = targetObject.AddComponent<EnemyThreatTarget>();
            var targetCollider = targetObject.AddComponent<CapsuleCollider>(); targetCollider.center = Vector3.up; targetCollider.height = 2;
            var stats = targetObject.GetComponent<CharacterStats>();
            var receiver = guard.GetComponent<DamageReceiver>();
            var guardStats = guard.GetComponent<CharacterStats>();
            var perception = guard.GetComponent<EnemyPerception>();
            var attack = guard.GetComponent<EnemyMeleeAttack>();
            Physics.SyncTransforms();
            int damageEvents = 0; target.Receiver.DamageTaken += (amount, source) => damageEvents++;
            Check(target.Receiver.TakeDamage(10, guard) == 0 && stats.CurrentHealth == stats.MaxHealth && damageEvents == 0,
                "allied NPC to player damage blocked without events");
            var childSource = new GameObject("weapon"); childSource.transform.SetParent(targetObject.transform, false);
            Check(receiver.TakeDamage(10, childSource) == 0, "child weapon resolves owning player team");
            Check(!perception.CanTarget(target) && !attack.TryStart(target), "allied player ignored");
            brain.Tick(0.1f); Check(brain.AggroTarget == null, "allied player does not aggro");
            targetTeam.TryConfigure(TeamId.TeamA, CombatantKind.Player, true);
            Check(receiver.TakeDamage(1, childSource) == 1 && brain.AggroTarget == null, "explicit friendly fire allowed but guard never retaliates against ally");
            targetTeam.TryConfigure(TeamId.TeamB, CombatantKind.NPC);
            Check(!perception.CanTarget(target), "enemy NPC ignored even with threat marker");
            targetTeam.TryConfigure(TeamId.Unassigned, CombatantKind.Player);
            Check(!perception.CanTarget(target), "unassigned player not treated as opposing team");
            targetTeam.TryConfigure(TeamId.TeamB, CombatantKind.Player);
            Check(perception.CanSee(target), "opposing player detected");
            brain.Tick(0.1f); Check(brain.AggroTarget == target && brain.State == EnemyState.Alert, "enemy acquired");
            brain.Tick(definition.AlertDuration + 0.1f); Check(attack.IsBusy, "guard starts attack");
            brain.Tick(definition.AttackWindup + 0.01f);
            Check(stats.CurrentHealth == stats.MaxHealth - definition.Damage && damageEvents == 1, "existing attack damages enemy once");
            attack.Cancel(); attack.Tick(10); Check(attack.TryStart(target), "new windup");
            float hp = stats.CurrentHealth; targetTeam.TryConfigure(TeamId.TeamA, CombatantKind.Player);
            attack.Tick(definition.AttackWindup + 0.01f); Check(stats.CurrentHealth == hp, "team switch during windup cancels damage");
            brain.Tick(0.1f); Check(brain.AggroTarget == null, "changed allegiance releases target");
            // Return completes immediately at home, so allow its next idle tick.
            brain.Tick(0.1f); targetTeam.TryConfigure(TeamId.TeamB, CombatantKind.Player);
            brain.Tick(0.1f); Check(brain.AggroTarget == target, "enemy reacquired");
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = home + new Vector3(0, 1, 0.6f);
            wall.transform.localScale = new Vector3(3, 3, 0.1f); Physics.SyncTransforms();
            Check(!perception.CanSee(target), "wall blocks detection");
            brain.Tick(definition.LostTargetTimeout + 0.1f);
            Check(brain.AggroTarget == null && brain.State == EnemyState.Return, "lost target begins return");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            targetObject.transform.position = home + Vector3.forward * 20; Physics.SyncTransforms();
            var agent = guard.GetComponent<NavMeshAgent>(); Check(agent.Warp(home + Vector3.right * 3), "move guard away from home");
            brain.Tick(0.3f); Check(brain.State == EnemyState.Return && agent.hasPath && !agent.isStopped, "return requests navigation to guard area");
            agent.Warp(home); brain.Tick(0.1f); Check(brain.State == EnemyState.Idle, "arrival resumes guarding");
            brain.Tick(definition.IdleDuration + 0.1f); Check(brain.State == EnemyState.Patrol, "guard patrol route requested");
            brain.SetAuthority(false); targetObject.transform.position = home + Vector3.forward * 1.2f; Physics.SyncTransforms();
            brain.Tick(1); Check(brain.AggroTarget == null && !attack.IsBusy, "non-authority cannot acquire or attack");
            Check(receiver.TakeDamage(1000, targetObject) > 0 && brain.State == EnemyState.Dead && !agent.enabled,
                "enemy player kills guard through shared damage and death architecture");
            // Team B guards use the identical policy with reversed identities.
            guardTeam.TryConfigure(TeamId.TeamB, CombatantKind.NPC); targetTeam.TryConfigure(TeamId.TeamA, CombatantKind.Player);
            Check(guard.GetComponent<FortressGuard>().AllowsTarget(target), "Team B accepts Team A player");
            Check(!EditorUtility.scriptCompilationFailed, "Editor compilation");
            result.Log("PASS: team damage, opt-in friendly fire, child sources, player-only guard targeting, windup allegiance, loss/return/patrol, authority and death. Real movement remains a Play Mode observation.");
        }
        finally
        {
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (guard != null) UnityEngine.Object.DestroyImmediate(guard);
            if (targetObject != null) UnityEngine.Object.DestroyImmediate(targetObject);
        }
    }
}
