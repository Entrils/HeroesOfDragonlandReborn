using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) throw new Exception("Setup requires Edit Mode.");
        if (GameObject.Find("Test Neutral Camp") != null) throw new Exception("Test camp already exists; inspect instead of creating a duplicate.");
        if (!AssetDatabase.IsValidFolder("Assets/Game/World/Prefabs")) AssetDatabase.CreateFolder("Assets/Game/World", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Game/Data/Camps")) AssetDatabase.CreateFolder("Assets/Game/Data", "Camps");
        const string chestPath = "Assets/Game/World/Prefabs/CampLootChest.prefab";
        var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chestPath);
        if (chestPrefab == null)
            chestPrefab = PrefabUtility.SaveAsPrefabAsset(GameObject.Find("Test Loot Chest"), chestPath);
        var definition = ScriptableObject.CreateInstance<CampDefinition>();
        var settings = new SerializedObject(definition);
        settings.FindProperty("experienceReward").intValue = 150;
        settings.FindProperty("chestPrefab").objectReferenceValue = chestPrefab.GetComponent<LootChest>();
        settings.FindProperty("lootTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/Game/Data/TestChestLoot.asset");
        var groups = settings.FindProperty("enemies");
        groups.arraySize = 2;
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/AI/Prefabs/TestBandit.prefab").GetComponent<EnemyBrain>();
        for (int i = 0; i < 2; i++)
        {
            var group = groups.GetArrayElementAtIndex(i);
            group.FindPropertyRelative("prefab").objectReferenceValue = enemy;
            group.FindPropertyRelative("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Game/Data/Enemies/" + (i == 0 ? "Bandit" : "Wolf") + ".asset");
            group.FindPropertyRelative("count").intValue = i == 0 ? 2 : 1;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        if (!definition.TryValidate(out string reason)) throw new Exception(reason);
        AssetDatabase.CreateAsset(definition, "Assets/Game/Data/Camps/TestNeutralCamp.asset");
        result.RegisterObjectCreation(definition);

        var root = new GameObject("Test Neutral Camp");
        result.RegisterObjectCreation(root);
        root.transform.position = new Vector3(-14, 0, 14);
        var camp = root.AddComponent<Camp>();
        var campSettings = new SerializedObject(camp);
        campSettings.FindProperty("campId").stringValue = "sample-neutral-camp-01";
        campSettings.FindProperty("definition").objectReferenceValue = definition;
        var points = campSettings.FindProperty("enemySpawnPoints");
        points.arraySize = 3;
        var offsets = new[] { new Vector3(-2, 0, 0), new Vector3(2, 0, 0), new Vector3(0, 0, 4) };
        for (int i = 0; i < 3; i++)
        {
            var point = new GameObject("Enemy Spawn " + (i + 1));
            result.RegisterObjectCreation(point);
            point.transform.SetParent(root.transform, false);
            if (!NavMesh.SamplePosition(root.transform.position + offsets[i], out var hit, 1, NavMesh.AllAreas))
                throw new Exception("Camp spawn is outside NavMesh.");
            point.transform.position = hit.position;
            points.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
        }
        var chestPoint = new GameObject("Chest Spawn");
        result.RegisterObjectCreation(chestPoint);
        chestPoint.transform.SetParent(root.transform, false);
        chestPoint.transform.localPosition = new Vector3(0, 0.05f, -4);
        campSettings.FindProperty("chestSpawnPoint").objectReferenceValue = chestPoint.transform;
        campSettings.ApplyModifiedPropertiesWithoutUndo();

        var labelObject = new GameObject("Camp Label");
        result.RegisterObjectCreation(labelObject);
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0, 3, 0);
        var label = labelObject.AddComponent<TextMesh>();
        label.text = "NEUTRAL CAMP\n3 enemies / 150 XP";
        label.characterSize = 0.1f;
        label.fontSize = 40;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        var visual = root.AddComponent<CampDebugVisual>();
        var visualSettings = new SerializedObject(visual);
        visualSettings.FindProperty("label").objectReferenceValue = label;
        visualSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(root.scene);
        result.Log("Saved test camp: two Bandits, one Wolf profile, 150 XP; chest at (-14, 0.05, 10).");
    }
}
