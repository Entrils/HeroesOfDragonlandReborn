using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;

internal class CommandScript : IRunCommand
{
    private readonly List<GameObject> fixtures = new List<GameObject>();
    private ExecutionResult result;
    private CampDefinition definition;
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Debug.Log("Camp edge PASS: " + label);
    }
    private Camp Create(bool authority)
    {
        var root = new GameObject("Camp Validation Fixture");
        fixtures.Add(root);
        result.RegisterObjectCreation(root);
        root.SetActive(false);
        root.transform.position = new Vector3(25, 0, -25);
        var camp = root.AddComponent<Camp>();
        var settings = new SerializedObject(camp);
        settings.FindProperty("campId").stringValue = Guid.NewGuid().ToString("N");
        settings.FindProperty("definition").objectReferenceValue = definition;
        settings.FindProperty("hasAuthority").boolValue = authority;
        var points = settings.FindProperty("enemySpawnPoints");
        points.arraySize = definition.EnemyCount;
        for (int i = 0; i < definition.EnemyCount; i++)
        {
            var point = new GameObject("Spawn " + i);
            result.RegisterObjectCreation(point);
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = Vector3.right * (i * 2);
            points.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
        }
        var chestPoint = new GameObject("Chest Spawn");
        result.RegisterObjectCreation(chestPoint);
        chestPoint.transform.SetParent(root.transform, false);
        chestPoint.transform.localPosition = new Vector3(0, 0.05f, -3);
        settings.FindProperty("chestSpawnPoint").objectReferenceValue = chestPoint.transform;
        settings.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        return camp;
    }
    private void PauseEnemies(Camp camp)
    {
        foreach (var enemy in camp.Enemies) enemy.SetAuthority(false);
    }
    public void Execute(ExecutionResult executionResult)
    {
        result = executionResult;
        if (!Application.isPlaying) throw new Exception("Run in Play Mode");
        definition = AssetDatabase.LoadAssetAtPath<CampDefinition>("Assets/Game/Data/Camps/TestNeutralCamp.asset");
        CampDefinition invalid = null;
        try
        {
            var camp = Create(false);
            Check(!camp.TryInitialize(out _) && camp.Enemies.Count == 0 && !camp.TryComplete(), "replica cannot spawn or complete");
            camp.SetAuthority(true);
            Check(camp.TryInitialize(out string reason), "authority initializes: " + reason);
            Check(!camp.TryInitialize(out _) && camp.Enemies.Count == 3, "initialization cannot duplicate enemies");
            int xp = 0;
            camp.ExperienceAwarded += _ => xp++;
            camp.SetAuthority(false);
            foreach (var enemy in camp.Enemies) enemy.GetComponent<CharacterStats>().ApplyDamage(10000);
            Check(!camp.IsCleared && camp.SpawnedChest == null && xp == 0 && !camp.TryComplete(), "no rewards without authority");
            camp.SetAuthority(true);
            Check(camp.IsCleared && xp == 1 && camp.SpawnedChest != null, "authority reconciles deaths once");
            camp.SetAuthority(false); camp.SetAuthority(true);
            Check(xp == 1, "authority cycling cannot repeat XP");

            var missing = Create(true);
            Check(missing.TryInitialize(out _), "despawn fixture initialized");
            PauseEnemies(missing);
            result.DestroyObject(missing.Enemies[0].gameObject);
            for (int i = 1; i < missing.Enemies.Count; i++) missing.Enemies[i].GetComponent<CharacterStats>().ApplyDamage(10000);
            Check(!missing.TryComplete() && !missing.IsCleared && missing.DeadCount == 2 && missing.SpawnedChest == null,
                "despawning live member does not clear camp");

            var disabled = Create(true);
            Check(disabled.TryInitialize(out _), "disabled fixture initialized");
            PauseEnemies(disabled);
            disabled.enabled = false;
            foreach (var enemy in disabled.Enemies) enemy.GetComponent<CharacterStats>().ApplyDamage(10000);
            Check(!disabled.IsCleared && disabled.DeadCount == 3 && disabled.SpawnedChest == null, "disabled camp records deaths but grants nothing");
            disabled.enabled = true;
            var rewardChest = disabled.SpawnedChest;
            Check(disabled.IsCleared && rewardChest != null, "re-enable completes pending clear");
            result.DestroyObject(rewardChest.gameObject);
            Check(!disabled.TryComplete() && disabled.IsCleared, "destroyed reward chest is never respawned");

            invalid = UnityEngine.Object.Instantiate(definition);
            result.RegisterObjectCreation(invalid);
            var config = new SerializedObject(invalid);
            config.FindProperty("enemies").arraySize = 0;
            config.ApplyModifiedPropertiesWithoutUndo();
            Check(!invalid.TryValidate(out _), "empty camp config rejected");
            var invalidCamp = Create(true);
            var setup = new SerializedObject(invalidCamp);
            setup.FindProperty("definition").objectReferenceValue = invalid;
            setup.ApplyModifiedPropertiesWithoutUndo();
            Check(!invalidCamp.TryInitialize(out _) && !invalidCamp.IsCleared && invalidCamp.Enemies.Count == 0,
                "invalid setup has no partial spawns or rewards");
            setup.FindProperty("definition").objectReferenceValue = definition;
            setup.FindProperty("chestSpawnPoint").objectReferenceValue = null;
            setup.ApplyModifiedPropertiesWithoutUndo();
            Check(!invalidCamp.TryInitialize(out _) && invalidCamp.Enemies.Count == 0, "missing chest point rejected before spawning");
            Debug.Log("CAMP EDGE VALIDATION PASSED");
        }
        finally
        {
            foreach (var fixture in fixtures) if (fixture != null) result.DestroyObject(fixture);
            if (invalid != null) result.DestroyObject(invalid);
        }
    }
}
