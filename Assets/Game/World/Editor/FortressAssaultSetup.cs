using System;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.World.Editor
{
    public static class FortressAssaultSetup
    {
        public const string Folder = "Assets/Game/Data/FortressAssault";
        private static Material Stone => AssetDatabase.LoadAssetAtPath<Material>(BattlefieldSceneBuilder.Materials + "/Placeholder stone.mat");
        private static Material TeamMaterial(TeamId team) => AssetDatabase.LoadAssetAtPath<Material>(BattlefieldSceneBuilder.Materials + (team == TeamId.TeamA ? "/Team A.mat" : "/Team B.mat"));
        private static void Edit(Object target, Action<SerializedObject> edit)
        { var data = new SerializedObject(target); edit(data); data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
        private static T Asset<T>(string name) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Folder + "/" + name + ".asset");
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, Folder + "/" + name + ".asset"); }
            return asset;
        }
        public static Vector3 World(TeamId team, Vector3 local) => (team == TeamId.TeamA ? new Vector3(-33, 0, 0) : new Vector3(33, 0, 0))
            + Quaternion.Euler(0, team == TeamId.TeamA ? 0 : 180, 0) * local;
        [MenuItem("Heroes of Dragonland/Battlefield/Install Fortress Assault")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != BattlefieldSceneBuilder.ScenePath) throw new InvalidOperationException("Open DragonlandBattlefield in Edit Mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Data", "FortressAssault");
            var config = Asset<FortressAssaultDefinition>("FortressAssault");
            var gateStats = Asset<CharacterStatsConfig>("GateStats");
            Edit(gateStats, s => { s.FindProperty("maxHealth").floatValue = 600; s.FindProperty("maxStamina").floatValue = 0; s.FindProperty("maxMana").floatValue = 0; s.FindProperty("poise").floatValue = 10000; });
            var outer = GuardDefinition("OuterGuard", 140, 8, 6.5f);
            var flank = GuardDefinition("FlankGuard", 140, 6, 5);
            var royal = GuardDefinition("RoyalGuard", 200, 6, 5.5f);
            var gates = new NetworkFortressGate[2]; var outerPrefabs = new NetworkEnemy[2];
            var posts = new NetworkEnemy[2, 2];
            var map = GameObject.Find("Battlefield | 88 x 88 | primitive blockout");
            if (map == null) throw new InvalidOperationException("Missing battlefield geometry.");
            for (int i = 0; i < 2; i++)
            {
                var team = i == 0 ? TeamId.TeamA : TeamId.TeamB;
                gates[i] = Gate(team, gateStats); outerPrefabs[i] = Guard(team, outer);
                posts[i, 0] = Guard(team, flank); posts[i, 1] = Guard(team, royal);
                Geometry(map.transform.Find(i == 0 ? "Team A Fortress" : "Team B Fortress"), team);
            }
            Edit(config, s => {
                var gateEntries = s.FindProperty("gates"); gateEntries.arraySize = 2;
                var guardEntries = s.FindProperty("guards"); guardEntries.arraySize = 6;
                for (int i = 0; i < 2; i++)
                {
                    var team = i == 0 ? TeamId.TeamA : TeamId.TeamB;
                    Entry(gateEntries.GetArrayElementAtIndex(i), gates[i], World(team, new Vector3(9, 0, 0)), i == 0 ? 0 : 180);
                    Entry(guardEntries.GetArrayElementAtIndex(i * 3), posts[i, 0], World(team, new Vector3(4, 0, 8.5f)), i == 0 ? 0 : 180);
                    Entry(guardEntries.GetArrayElementAtIndex(i * 3 + 1), posts[i, 0], World(team, new Vector3(4, 0, -8.5f)), i == 0 ? 180 : 0);
                    Entry(guardEntries.GetArrayElementAtIndex(i * 3 + 2), posts[i, 1], World(team, new Vector3(-4, 0, 6)), i == 0 ? 90 : -90);
                }
            });
            const string matchPath = "Assets/Game/Data/Battlefield/BattlefieldMatch.prefab";
            var matchRoot = PrefabUtility.LoadPrefabContents(matchPath);
            try {
                Edit(matchRoot.GetComponent<NetworkMatch>(), s => {
                    s.FindProperty("teamAKingPosition").vector3Value = World(TeamId.TeamA, new Vector3(-5, 0, 7));
                    s.FindProperty("teamBKingPosition").vector3Value = World(TeamId.TeamB, new Vector3(-5, 0, 7));
                    s.FindProperty("teamAGuardPosition").vector3Value = World(TeamId.TeamA, new Vector3(11, 0, 0));
                    s.FindProperty("teamBGuardPosition").vector3Value = World(TeamId.TeamB, new Vector3(11, 0, 0));
                    s.FindProperty("teamAGuardPrefab").objectReferenceValue = outerPrefabs[0];
                    s.FindProperty("teamBGuardPrefab").objectReferenceValue = outerPrefabs[1];
                });
                PrefabUtility.SaveAsPrefabAsset(matchRoot, matchPath);
            } finally { PrefabUtility.UnloadPrefabContents(matchRoot); }
            var bootstrap = Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var spawner = bootstrap.GetComponent<FortressAssaultSpawner>() ?? Undo.AddComponent<FortressAssaultSpawner>(bootstrap.gameObject);
            Edit(spawner, s => s.FindProperty("definition").objectReferenceValue = config);
            Physics.SyncTransforms();
            var surface = map.GetComponent<NavMeshSurface>(); var previousData = surface.navMeshData;
            surface.BuildNavMesh(); var baked = surface.navMeshData;
            if (baked == null) throw new InvalidOperationException("Fortress navigation bake failed.");
            if (previousData != null && AssetDatabase.Contains(previousData))
            {
                surface.RemoveData(); EditorUtility.CopySerialized(baked, previousData); previousData.name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(previousData)); EditorUtility.SetDirty(previousData);
                surface.navMeshData = previousData; surface.AddData(); Object.DestroyImmediate(baked);
            }
            else AssetDatabase.CreateAsset(baked, Folder + "/FortressNavMesh.asset");
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset")) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            foreach (var name in new[] { "TeamAGate", "TeamBGate", "TeamAOuterGuard", "TeamBOuterGuard", "TeamAFlankGuard", "TeamBFlankGuard", "TeamARoyalGuard", "TeamBRoyalGuard" })
                if (!Fusion.Editor.NetworkProjectConfigUtilities.TryGetPrefabId(Folder + "/" + name + ".prefab", out _)) throw new InvalidOperationException("Unregistered " + name);
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save fortress assault scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("[FortressAssault] Saved two 600 HP gates, eight stationed guards, outer defenses, two side approaches and protected King chambers. NavMesh GUID preserved.");
        }
        private static void Entry(SerializedProperty entry, Object prefab, Vector3 position, float yaw)
        { entry.FindPropertyRelative("prefab").objectReferenceValue = prefab; entry.FindPropertyRelative("position").vector3Value = position; entry.FindPropertyRelative("yaw").floatValue = yaw; }
        private static EnemyDefinition GuardDefinition(string name, float hp, float detection, float leash)
        {
            var stats = Asset<CharacterStatsConfig>(name + "Stats");
            Edit(stats, s => { s.FindProperty("maxHealth").floatValue = hp; s.FindProperty("maxStamina").floatValue = 100; s.FindProperty("poise").floatValue = 70; });
            string path = Folder + "/" + name + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (definition == null) { AssetDatabase.CopyAsset("Assets/Game/Data/MultiplayerPve/Knight.asset", path); definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path); }
            definition.name = name;
            Edit(definition, s => {
                s.FindProperty("stats").objectReferenceValue = stats; s.FindProperty("boss").objectReferenceValue = null; s.FindProperty("attackSet").objectReferenceValue = null;
                s.FindProperty("detectionRadius").floatValue = detection; s.FindProperty("leashDistance").floatValue = leash;
                s.FindProperty("patrolRadius").floatValue = .3f; s.FindProperty("lostTargetTimeout").floatValue = 2;
                s.FindProperty("damage").floatValue = name == "RoyalGuard" ? 22 : 16;
            });
            return definition;
        }
        private static NetworkEnemy Guard(TeamId team, EnemyDefinition definition)
        {
            string path = Folder + "/" + team + definition.name + ".prefab";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CopyAsset("Assets/Game/Data/MultiplayerFortress/" + team + "Guard.prefab", path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                Edit(root.GetComponent<EnemyBrain>(), s => s.FindProperty("definition").objectReferenceValue = definition);
                Edit(root.GetComponent<CharacterStats>(), s => s.FindProperty("configuration").objectReferenceValue = definition.Stats);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<NetworkEnemy>(path);
        }
        private static NetworkFortressGate Gate(TeamId team, CharacterStatsConfig stats)
        {
            string path = Folder + "/" + team + "Gate.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<NetworkFortressGate>(path); if (existing != null) return existing;
            var go = new GameObject(team + " Gate");
            try {
                go.AddComponent<NetworkObject>();
                Edit(go.AddComponent<CharacterStats>(), s => s.FindProperty("configuration").objectReferenceValue = stats);
                go.AddComponent<NetworkCharacterStats>(); go.AddComponent<DamageReceiver>(); go.AddComponent<Targetable>();
                var gate = go.AddComponent<FortressGate>(); Edit(gate, s => s.FindProperty("team").intValue = (int)team);
                go.GetComponent<BoxCollider>().center = Vector3.up * 2; go.GetComponent<BoxCollider>().size = new Vector3(1.2f, 4, 5.8f);
                var obstacle = go.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = Vector3.up * 2; obstacle.size = new Vector3(1.2f, 4, 5.8f); obstacle.carving = true;
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name = "Gate body"; body.transform.SetParent(go.transform, false);
                body.transform.localPosition = Vector3.up * 2; body.transform.localScale = new Vector3(1.2f, 4, 5.8f); Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = TeamMaterial(team);
                Edit(gate, s => s.FindProperty("gateBody").objectReferenceValue = body.transform);
                go.AddComponent<NetworkFortressGate>();
                var label = new GameObject("Gate health").AddComponent<TextMesh>(); label.transform.SetParent(go.transform, false); label.transform.localPosition = Vector3.up * 4.8f;
                label.anchor = TextAnchor.MiddleCenter; label.fontSize = 48; label.characterSize = .12f;
                Edit(go.AddComponent<FortressGatePresentation>(), s => s.FindProperty("label").objectReferenceValue = label);
                Edit(go.GetComponent<Targetable>(), s => s.FindProperty("localAimOffset").vector3Value = Vector3.up * 2);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            } finally { Object.DestroyImmediate(go); }
            return AssetDatabase.LoadAssetAtPath<NetworkFortressGate>(path);
        }
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var found = parent.Find(name); var go = found != null ? found.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid && go.TryGetComponent<Collider>(out var collider)) Object.DestroyImmediate(collider);
            if (solid) { var modifier = go.GetComponent<NavMeshModifier>() ?? go.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1; }
            return go;
        }
        private static void Geometry(Transform fortress, TeamId team)
        {
            var found = fortress.Find("Assault Layers"); var root = found != null ? found : new GameObject("Assault Layers").transform;
            root.SetParent(fortress, false);
            Box(root, "Outer defensive zone", new Vector3(13, .08f, 0), new Vector3(7, .03f, 20), TeamMaterial(team), false);
            Box(root, "Inner courtyard", new Vector3(3, .08f, 0), new Vector3(9, .03f, 18), Stone, false);
            Box(root, "King chamber floor", new Vector3(-4.75f, .085f, 5.5f), new Vector3(6.5f, .03f, 9), TeamMaterial(team), false);
            Box(root, "King chamber rear", new Vector3(-8, 2.25f, 5.5f), new Vector3(.7f, 4.5f, 9), Stone);
            Box(root, "King chamber south", new Vector3(-4.75f, 2.25f, 1), new Vector3(6.5f, 4.5f, .7f), Stone);
            Box(root, "King chamber front shield", new Vector3(-1.5f, 2.25f, 7), new Vector3(.7f, 4.5f, 6), Stone);
            // East-facing doorway z=1.35..4; north doorway x=-4..-1.85. Neither is a straight main-gate shot.
            Box(root, "King chamber north shield", new Vector3(-6, 2.25f, 10), new Vector3(4, 4.5f, .7f), Stone);
            Box(root, "Chamber doorway lintel", new Vector3(-1.5f, 4.65f, 2.5f), new Vector3(.9f, .5f, 3), TeamMaterial(team), false);
            Box(root, "North doorway lintel", new Vector3(-2.75f, 4.65f, 10), new Vector3(2.5f, .5f, .9f), TeamMaterial(team), false);
            foreach (int side in new[] { -1, 1 })
            {
                Box(root, "Outer cover " + side, new Vector3(13.5f, .75f, 7 * side), new Vector3(2, 1.5f, 2), Stone);
                Box(root, "Gate shoulder " + side, new Vector3(8.8f, 2.3f, 3.2f * side), new Vector3(1.4f, 4.6f, .5f), Stone);
            }
        }
    }
}
