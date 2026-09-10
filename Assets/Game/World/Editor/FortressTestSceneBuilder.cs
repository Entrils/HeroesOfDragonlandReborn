using System;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    // Invoke through Unity MCP in Edit Mode. Does not run automatically on import.
    public static class FortressTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/FortressGuardTest.unity";
        public static void Build() => BuildBattlefield(ScenePath, "Assets/Game/Data/FortressTest", null);

        internal static void BuildBattlefield(string scenePath, string dataPath, Action<Scene> configureObjectives)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null || AssetDatabase.IsValidFolder(dataPath))
                throw new InvalidOperationException("Fortress test assets already exist; inspect instead of overwriting.");
            var prefab = Load<GameObject>("Assets/Game/AI/Prefabs/TestBandit.prefab");
            var baseDefinition = Load<EnemyDefinition>("Assets/Game/Data/Enemies/Bandit.asset");
            var statsConfig = Load<CharacterStatsConfig>("Assets/Game/Data/DefaultCharacterStats.asset");
            var attackConfig = Load<MeleeAttackDefinition>("Assets/Game/Data/LightAttack.asset");
            var dodgeConfig = Load<DodgeDefinition>("Assets/Game/Data/DefaultDodge.asset");
            var inputs = Load<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
            AssetDatabase.CreateFolder("Assets/Game/Data", System.IO.Path.GetFileName(dataPath));
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var blue = Material("TeamA", new Color(0.15f, 0.35f, 0.85f), shader, dataPath);
            var red = Material("TeamB", new Color(0.85f, 0.2f, 0.15f), shader, dataPath);
            var stone = Material("Stone", new Color(0.4f, 0.43f, 0.46f), shader, dataPath);
            var terrain = new GameObject("Fortress Terrain");
            Cube("Ground", terrain.transform, new Vector3(0, -0.25f, 0), new Vector3(48, 0.5f, 28), stone);
            Area("Team A Area", new Vector3(-14, 0, 0), blue, terrain.transform);
            Area("Team B Area", new Vector3(14, 0, 0), red, terrain.transform);
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.4f; light.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            var surface = terrain.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh build failed.");
            AssetDatabase.CreateAsset(surface.navMeshData, dataPath + "/FortressNavMesh.asset");
            var guardDefinition = UnityEngine.Object.Instantiate(baseDefinition);
            guardDefinition.name = "FortressGuard";
            Set(guardDefinition, "detectionRadius", 9f); Set(guardDefinition, "leashDistance", 11f);
            Set(guardDefinition, "patrolRadius", 2f);
            AssetDatabase.CreateAsset(guardDefinition, dataPath + "/FortressGuard.asset");
            Guard(prefab, guardDefinition, TeamId.TeamA, new Vector3(-14, 0, 0), blue);
            Guard(prefab, guardDefinition, TeamId.TeamB, new Vector3(14, 0, 0), red);
            var player = new GameObject("Player (Team A)");
            player.transform.position = new Vector3(-6, 0.1f, -5);
            var identity = player.AddComponent<TeamIdentity>(); identity.TryConfigure(TeamId.TeamA, CombatantKind.Player);
            player.AddComponent<TeamDebug>();
            var controller = player.AddComponent<CharacterController>(); controller.height = 2; controller.radius = 0.4f; controller.center = Vector3.up;
            Set(player.AddComponent<CharacterStats>(), "configuration", statsConfig);
            player.AddComponent<DamageReceiver>(); player.AddComponent<EnemyThreatTarget>();
            player.AddComponent<PlayerMovement>();
            Set(player.AddComponent<MeleeAttack>(), "definition", attackConfig);
            Set(player.AddComponent<PlayerDodge>(), "definition", dodgeConfig);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Player Body";
            body.transform.SetParent(player.transform, false); body.transform.localPosition = Vector3.up;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial = blue;
            var camera = new GameObject("Main Camera"); camera.tag = "MainCamera";
            var view = camera.AddComponent<Camera>(); camera.AddComponent<AudioListener>();
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 4, -6), Quaternion.Euler(20, 0, 0));
            var follow = camera.AddComponent<ThirdPersonCamera>(); Set(follow, "target", player.transform);
            Set(player.AddComponent<TargetLock>(), "viewCamera", view);
            var input = player.AddComponent<PlayerController>(); Set(input, "inputActions", inputs); Set(input, "followCamera", follow);
            configureObjectives?.Invoke(scene);
            foreach (var guard in UnityEngine.Object.FindObjectsByType<FortressGuard>())
            {
                if (guard.gameObject.scene != scene) continue;
                if (!guard.GetComponent<TeamIdentity>().HasTeam || guard.GetComponent<EnemyBrain>().Definition == null
                    || !NavMesh.SamplePosition(guard.transform.position, out _, 1f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Guard has incomplete team, definition or navigation setup.");
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Scene save failed.");
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Debug.Log("Saved " + scenePath + ". Open it by itself for Play Mode. TeamDebug context menus switch player team.");
        }
        private static T Load<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string property, float value)
        { var so = new SerializedObject(target); so.FindProperty(property).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static Material Material(string name, Color color, Shader shader, string dataPath)
        {
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, dataPath + "/" + name + ".mat"); return material;
        }
        private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static void Area(string name, Vector3 center, Material material, Transform terrain)
        {
            var root = new GameObject(name); root.transform.SetParent(terrain, false);
            // Open courtyard faces the centre; walls make loss-of-sight testable.
            Cube("Back Wall", root.transform, center + new Vector3(0, 1.5f, 6), new Vector3(10, 3, 0.5f), material);
            Cube("Side Wall", root.transform, center + new Vector3(-5, 1.5f, 2), new Vector3(0.5f, 3, 8), material);
            var label = new GameObject(name + " Label").AddComponent<TextMesh>();
            label.transform.SetParent(root.transform, false); label.transform.position = center + new Vector3(0, 4, 6);
            label.text = name; label.anchor = TextAnchor.MiddleCenter; label.characterSize = 0.3f; label.fontSize = 40; label.color = material.color;
        }
        private static void Guard(GameObject prefab, EnemyDefinition definition, TeamId team, Vector3 position, Material material)
        {
            var guard = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            guard.name = team + " Fortress Guard"; guard.transform.position = position;
            Set(guard.GetComponent<EnemyBrain>(), "definition", definition);
            guard.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC);
            guard.AddComponent<FortressGuard>();
            foreach (var renderer in guard.GetComponentsInChildren<MeshRenderer>())
                if (renderer.GetComponent<TextMesh>() == null) renderer.sharedMaterial = material;
        }
    }
}
