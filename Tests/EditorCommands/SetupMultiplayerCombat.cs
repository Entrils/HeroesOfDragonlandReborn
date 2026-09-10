using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (Application.isPlaying) throw new Exception("Use Edit Mode");
        const string prefabPath = "Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab";
        const string definitionPath = "Assets/Game/Data/MultiplayerFoundation/NetworkMelee.asset";
        const string scenePath = "Assets/Scenes/MultiplayerFoundationTest.unity";
        var definition = AssetDatabase.LoadAssetAtPath<MeleeAttackDefinition>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<MeleeAttackDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
            result.RegisterObjectCreation(definition);
        }
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            result.RegisterObjectModification(root);
            foreach (string name in new[] { "NetworkCharacterStats", "NetworkPlayerResources", "NetworkPlayerCombat" })
            {
                var type = typeof(CharacterStats).Assembly.GetType("HeroesOfDragonland.Multiplayer." + name, true);
                if (root.GetComponent(type) == null) root.AddComponent(type);
            }
            var stats = new SerializedObject(root.GetComponent<CharacterStats>());
            stats.FindProperty("configuration").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>("Assets/Game/Data/DefaultCharacterStats.asset");
            stats.ApplyModifiedPropertiesWithoutUndo();
            var attack = new SerializedObject(root.GetComponent<MeleeAttack>());
            attack.FindProperty("definition").objectReferenceValue = definition;
            attack.ApplyModifiedPropertiesWithoutUndo();
            var collider = root.transform.Find("Body").GetComponent<Collider>();
            result.RegisterObjectModification(collider);
            collider.enabled = true;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new Exception("Test scene has unsaved changes; inspect before changing");
            foreach (var item in scene.GetRootGameObjects())
            {
                if (item.GetComponent<MultiplayerBootstrap>() == null) continue;
                result.RegisterObjectModification(item);
                if (item.GetComponent<MultiplayerCombatProbe>() == null) item.AddComponent<MultiplayerCombatProbe>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        result.Log("Saved multiplayer combat prefab, existing Stats configuration, attack definition and probe scene.");
    }
}
