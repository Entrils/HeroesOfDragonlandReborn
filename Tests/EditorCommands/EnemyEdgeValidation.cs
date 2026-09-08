using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    private void Check(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Debug.Log("Enemy edge PASS: " + message);
    }

    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new Exception("Run in Play Mode after EnemyValidation");
        GameObject enemy = null;
        GameObject targetObject = null;
        GameObject wall = null;
        EnemyDefinition definition = null;
        try
        {
            Check(NavMesh.SamplePosition(new Vector3(-25, 0, -25), out var hit, 2, NavMesh.AllAreas), "fixture NavMesh");
            var home = hit.position;
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/AI/Prefabs/TestBandit.prefab"), home, Quaternion.identity);
            result.RegisterObjectCreation(enemy);
            var brain = enemy.GetComponent<EnemyBrain>();
            var stats = enemy.GetComponent<CharacterStats>();
            var receiver = enemy.GetComponent<DamageReceiver>();
            var agent = enemy.GetComponent<NavMeshAgent>();
            definition = UnityEngine.Object.Instantiate(brain.Definition);
            result.RegisterObjectCreation(definition);
            var config = new SerializedObject(definition);
            config.FindProperty("aggroOnSight").boolValue = false;
            config.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(brain);
            settings.FindProperty("definition").objectReferenceValue = definition;
            settings.ApplyModifiedPropertiesWithoutUndo();

            targetObject = new GameObject("Enemy edge target");
            result.RegisterObjectCreation(targetObject);
            targetObject.transform.position = home + Vector3.forward * 4;
            targetObject.AddComponent<CharacterStats>();
            targetObject.AddComponent<DamageReceiver>();
            var target = targetObject.AddComponent<EnemyThreatTarget>();
            var collider = targetObject.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up;
            Physics.SyncTransforms();
            brain.Tick(0.1f);
            Check(brain.AggroTarget == null && brain.State == EnemyState.Idle, "neutral profile ignores proximity");
            receiver.TakeDamage(1, targetObject);
            Check(brain.AggroTarget == target && brain.State == EnemyState.Alert, "neutral retaliates when damaged");

            agent.Warp(home + Vector3.right);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.RegisterObjectCreation(wall);
            wall.transform.position = home + new Vector3(0, 1, 2);
            wall.transform.localScale = new Vector3(6, 3, 0.2f);
            Physics.SyncTransforms();
            Check(!enemy.GetComponent<EnemyPerception>().CanSee(target), "occluded target invisible");
            brain.Tick(definition.LostTargetTimeout + 0.1f);
            Check(brain.State == EnemyState.Return && brain.AggroTarget == null, "lost target times out");
            receiver.TakeDamage(1, targetObject);
            Check(brain.AggroTarget == null, "return ignores retaliation");
            result.DestroyObject(wall);
            wall = null;
            agent.Warp(home);
            brain.Tick(0.1f);
            receiver.TakeDamage(1, targetObject);
            agent.Warp(home + Vector3.right);
            result.DestroyObject(targetObject);
            targetObject = null;
            brain.Tick(0.1f);
            Check(brain.State == EnemyState.Return && brain.AggroTarget == null, "destroyed target released");

            brain.enabled = false;
            Check(agent.isStopped && !enemy.GetComponent<EnemyMeleeAttack>().IsBusy, "disable stops movement and attack");
            stats.ApplyDamage(1000);
            brain.enabled = true;
            Check(brain.State == EnemyState.Dead && !agent.enabled && !enemy.GetComponent<Collider>().enabled,
                "death while disabled handled on enable");
            Debug.Log("ENEMY EDGE VALIDATION PASSED");
        }
        finally
        {
            if (wall != null) result.DestroyObject(wall);
            if (targetObject != null) result.DestroyObject(targetObject);
            if (enemy != null) result.DestroyObject(enemy);
            if (definition != null) result.DestroyObject(definition);
        }
    }
}
