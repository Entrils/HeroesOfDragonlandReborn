using System;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.World.Camps;
using HeroesOfDragonland.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    public static class MultiplayerCampSceneBuilder
    {
        public const string Folder = "Assets/Game/Data/MultiplayerCamps";
        public const string ScenePath = "Assets/Scenes/MultiplayerCampTest.unity";
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            if (AssetDatabase.IsValidFolder(Folder) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Camp assets exist; inspect instead of overwriting.");
            AssetDatabase.CreateFolder("Assets/Game/Data", "MultiplayerCamps");
            string playerPath = Folder + "/NetworkCampPlayer.prefab";
            Copy("Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab", playerPath);
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var progression = player.AddComponent<SessionProgression>();
                Set(progression, "definition", Load<LevelProgressionDefinition>("Assets/Game/Data/Progression/DefaultLevelProgression.asset"));
                var fields = new SerializedObject(progression);
                fields.FindProperty("hasAuthority").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
                player.AddComponent<NetworkSessionProgression>();
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            string chestPath = Folder + "/NetworkCampChest.prefab";
            Copy("Assets/Game/World/Prefabs/CampLootChest.prefab", chestPath);
            var chest = PrefabUtility.LoadPrefabContents(chestPath);
            try
            {
                chest.AddComponent<NetworkObject>(); chest.AddComponent<NetworkTransform>(); chest.AddComponent<NetworkLootChest>();
                Set(chest.GetComponent<LootChest>(), "lootTable", Load<LootTable>("Assets/Game/Data/TestChestLoot.asset"));
                var fields = new SerializedObject(chest.GetComponent<LootChest>());
                fields.FindProperty("hasAuthority").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(chest, chestPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(chest); }

            var definition = ScriptableObject.CreateInstance<NetworkCampDefinition>();
            var data = new SerializedObject(definition);
            data.FindProperty("experienceReward").intValue = 100;
            data.FindProperty("chestPrefab").objectReferenceValue = Load<NetworkLootChest>(chestPath);
            data.FindProperty("chestOffset").vector3Value = new Vector3(0, 0, 11);
            var entries = data.FindProperty("enemies"); entries.arraySize = 3;
            var names = new[] { "Wolf", "Bear", "Knight" };
            var positions = new[] { new Vector3(-9, 0, 14), new Vector3(0, 0, 16), new Vector3(9, 0, 14) };
            for (int i = 0; i < names.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = Load<NetworkEnemy>(MultiplayerPveSceneBuilder.Folder + "/Network" + names[i] + ".prefab");
                entry.FindPropertyRelative("offset").vector3Value = positions[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, Folder + "/NeutralCamp.asset");

            var root = new GameObject("Network Neutral Camp");
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
                Set(root.AddComponent<NetworkCamp>(), "definition", definition);
                var label = new GameObject("Camp Label").AddComponent<TextMesh>();
                label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, 3, 11);
                label.anchor = TextAnchor.MiddleCenter; label.characterSize = 0.12f; label.fontSize = 32;
                Set(root.AddComponent<NetworkCampPresentation>(), "label", label);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/NetworkNeutralCamp.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            Copy(MultiplayerPveSceneBuilder.ScenePath, ScenePath);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                MultiplayerBootstrap bootstrap = null;
                foreach (var obj in scene.GetRootGameObjects())
                    if (obj.TryGetComponent<MultiplayerBootstrap>(out var found)) bootstrap = found;
                if (bootstrap == null) throw new InvalidOperationException("Missing multiplayer bootstrap.");
                bootstrap.GetComponent<NetworkEnemySpawner>().enabled = false;
                bootstrap.GetComponent<MultiplayerPveProbe>().enabled = false;
                Set(bootstrap, "playerPrefab", Load<NetworkObject>(playerPath));
                var spawner = bootstrap.gameObject.AddComponent<NetworkCampSpawner>();
                data = new SerializedObject(spawner);
                entries = data.FindProperty("entries"); entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = Load<NetworkCamp>(Folder + "/NetworkNeutralCamp.prefab");
                data.ApplyModifiedPropertiesWithoutUndo();
                bootstrap.gameObject.AddComponent<MultiplayerCampProbe>();
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Camp scene save failed.");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            RegisterPrefabs();
            Debug.Log("[Camps] Saved network camp/player/chest prefabs, definition and test scene.");
        }
        public static void RegisterPrefabs()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            // This project contains two imported config assets. Refresh both tables so
            // no stale Resources asset can omit the newly authored prefabs in a build.
            foreach (string guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset"))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            ValidateRegistration();
            AssetDatabase.SaveAssets();
        }
        public static void ValidateRegistration()
        {
            foreach (string name in new[] { "NetworkCampPlayer", "NetworkCampChest", "NetworkNeutralCamp" })
                if (!Fusion.Editor.NetworkProjectConfigUtilities.TryGetPrefabId(Folder + "/" + name + ".prefab", out _))
                    throw new InvalidOperationException("Camp prefab missing from Fusion table: " + name);
            Debug.Log("[Camps] All three camp prefabs registered in Fusion table.");
        }
        private static T Load<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
        private static void Copy(string source, string target)
        { if (!AssetDatabase.CopyAsset(source, target)) throw new InvalidOperationException("Copy failed: " + target); }
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var fields = new SerializedObject(target); fields.FindProperty(name).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
