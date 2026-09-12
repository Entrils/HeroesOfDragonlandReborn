using System;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    // Run through Unity MCP. Existing offline content and foundation scene stay intact.
    public static class MultiplayerPveSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MultiplayerPveTest.unity";
        public const string Folder = "Assets/Game/Data/MultiplayerPve";
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || AssetDatabase.IsValidFolder(Folder))
                throw new InvalidOperationException("PvE assets already exist; inspect instead of overwriting them.");
            AssetDatabase.CreateFolder("Assets/Game/Data", "MultiplayerPve");
            var wolf = CreateEnemy("Wolf", Load<EnemyDefinition>("Assets/Game/Data/Enemies/Wolf.asset"));
            var bear = CreateEnemy("Bear", Load<EnemyDefinition>("Assets/Game/Data/Enemies/Bear.asset"));
            // No Knight asset exists yet. Reuse the existing melee humanoid profile/data.
            var knightDefinition = UnityEngine.Object.Instantiate(Load<EnemyDefinition>("Assets/Game/Data/Enemies/Bandit.asset"));
            knightDefinition.name = "Knight";
            AssetDatabase.CreateAsset(knightDefinition, Folder + "/Knight.asset");
            var knight = CreateEnemy("Knight", knightDefinition);
            ConfigurePlayer();
            if (!AssetDatabase.CopyAsset(MultiplayerTestSceneBuilder.ScenePath, ScenePath)) throw new InvalidOperationException("Scene copy failed");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                MultiplayerBootstrap bootstrap = null;
                GameObject ground = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<MultiplayerBootstrap>(out var found)) bootstrap = found;
                    if (root.name == "Test Arena") ground = root;
                }
                if (bootstrap == null || ground == null) throw new InvalidOperationException("Missing foundation bootstrap/arena");
                var terrain = new GameObject("PvE Navigation");
                ground.transform.SetParent(terrain.transform, true);
                var surface = terrain.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("PvE NavMesh build failed");
                AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/ArenaNavMesh.asset");
                var spawner = bootstrap.gameObject.AddComponent<NetworkEnemySpawner>();
                var fields = new SerializedObject(spawner);
                var entries = fields.FindProperty("entries"); entries.arraySize = 3;
                var prefabs = new[] { wolf, bear, knight };
                var positions = new[] { new Vector3(-10, 0, 9), new Vector3(10, 0, 9), new Vector3(0, 0, 15) };
                for (int i = 0; i < 3; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
                    entry.FindPropertyRelative("localPosition").vector3Value = positions[i];
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                bootstrap.gameObject.AddComponent<MultiplayerPveProbe>();
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("PvE scene save failed");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            Debug.Log("[PvE] Saved Wolf, Bear, Knight prefabs and MultiplayerPveTest with server spawner/NavMesh");
        }
        private static T Load<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
        private static NetworkObject CreateEnemy(string name, EnemyDefinition definition)
        {
            string path = Folder + "/Network" + name + ".prefab";
            if (!AssetDatabase.CopyAsset("Assets/Game/AI/Prefabs/TestBandit.prefab", path)) throw new InvalidOperationException("Prefab copy failed");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = "Network " + name;
                root.transform.position = Vector3.zero;
                Set(root.GetComponent<EnemyBrain>(), "definition", definition);
                Set(root.GetComponent<CharacterStats>(), "configuration", definition.Stats);
                var fields = new SerializedObject(root.GetComponent<EnemyBrain>());
                fields.FindProperty("hasAuthority").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<NavMeshAgent>().enabled = false;
                root.GetComponent<EnemyDebugVisual>().enabled = false;
                fields = new SerializedObject(root.GetComponent<DamageReceiver>());
                fields.FindProperty("deactivateOnDeath").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                root.AddComponent<NetworkCharacterStats>();
                root.AddComponent<NetworkEnemy>();
                var view = root.AddComponent<NetworkEnemyPresentation>();
                Set(view, "body", root.transform.Find("Body"));
                Set(view, "label", root.GetComponentInChildren<TextMesh>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return Load<NetworkObject>(path);
        }
        private static void ConfigurePlayer()
        {
            const string path = "Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = root.GetComponent<EnemyThreatTarget>();
                if (target == null) target = root.AddComponent<EnemyThreatTarget>();
                var fields = new SerializedObject(target);
                fields.FindProperty("navigationOffset").vector3Value = Vector3.down;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var fields = new SerializedObject(target);
            fields.FindProperty(property).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
