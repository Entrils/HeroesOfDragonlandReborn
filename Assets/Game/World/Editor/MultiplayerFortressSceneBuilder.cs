using System;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class MultiplayerFortressSceneBuilder
    {
        public const string Folder = "Assets/Game/Data/MultiplayerFortress";
        public const string MaterialFolder = "Assets/Art/Materials/MultiplayerFortress";
        public const string ScenePath = "Assets/Scenes/MultiplayerFortressTest.unity";
        public static void Build()
        {
            if (Application.isPlaying || AssetDatabase.IsValidFolder(Folder) || AssetDatabase.IsValidFolder(MaterialFolder) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Use Edit Mode with no existing fortress assets.");
            AssetDatabase.CreateFolder("Assets/Game/Data", "MultiplayerFortress");
            AssetDatabase.CreateFolder("Assets/Art/Materials", "MultiplayerFortress");
            var profile = ScriptableObject.CreateInstance<CharacterStatsConfig>();
            var profileFields = new SerializedObject(profile);
            profileFields.FindProperty("maxHealth").floatValue = 1000;
            profileFields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, Folder + "/KingStats.asset");
            var gold = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.75f, 0.1f) };
            AssetDatabase.CreateAsset(gold, MaterialFolder + "/Crown.mat");
            var a = CreateKing(TeamId.TeamA, profile);
            var b = CreateKing(TeamId.TeamB, profile);
            var ga = CreateGuard(TeamId.TeamA); var gb = CreateGuard(TeamId.TeamB);
            var root = new GameObject("Network Fortress Match");
            try
            {
                root.AddComponent<NetworkObject>();
                var match = root.AddComponent<NetworkMatch>();
                Set(match, "teamAKingPrefab", a); Set(match, "teamBKingPrefab", b);
                Set(match, "teamAGuardPrefab", ga); Set(match, "teamBGuardPrefab", gb);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/NetworkMatch.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            if (!AssetDatabase.CopyAsset(MultiplayerCampSceneBuilder.ScenePath, ScenePath)) throw new InvalidOperationException("Scene copy failed.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var obj in scene.GetRootGameObjects()) if (obj.TryGetComponent<MultiplayerBootstrap>(out var bootstrap))
                {
                    // Reuse M7 camp player, progression, inventory and local chest input.
                    bootstrap.GetComponent<MultiplayerCampProbe>().enabled = false;
                    bootstrap.GetComponent<MultiplayerLootProbe>().enabled = false;
                    Set(obj.AddComponent<NetworkFortressSpawner>(), "matchPrefab", AssetDatabase.LoadAssetAtPath<NetworkMatch>(Folder + "/NetworkMatch.prefab"));
                    obj.AddComponent<NetworkFortressPresentation>();
                    obj.AddComponent<MultiplayerFortressProbe>();
                }
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Fortress scene save failed.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Register();
            InstallRespawn();
        }
        public static void InstallRespawn()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            string configPath = Folder + "/SessionRespawn.asset";
            var config = AssetDatabase.LoadAssetAtPath<SessionRespawnConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SessionRespawnConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            string playerPath = Folder + "/NetworkFortressPlayer.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(playerPath) == null
                && !AssetDatabase.CopyAsset("Assets/Game/Data/MultiplayerCamps/NetworkCampPlayer.prefab", playerPath))
                throw new InvalidOperationException("Cannot copy fortress player.");
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                if (player.GetComponent<NetworkPlayerRespawn>() == null) player.AddComponent<NetworkPlayerRespawn>();
                var damage = new SerializedObject(player.GetComponent<DamageReceiver>());
                damage.FindProperty("deactivateOnDeath").boolValue = false;
                damage.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent<NetworkFortressSpawner>(out var fortress))
                    {
                        Set(fortress, "respawnConfig", config);
                        if (root.GetComponent<MultiplayerRespawnProbe>() == null) root.AddComponent<MultiplayerRespawnProbe>();
                        Set(root.GetComponent<MultiplayerBootstrap>(), "playerPrefab", AssetDatabase.LoadAssetAtPath<NetworkObject>(playerPath));
                    }
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Respawn scene save failed.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Register();
            TeamSpawnAreaSceneBuilder.Install();
            MatchLifecycleSceneBuilder.Install();
        }
        private static NetworkKingObjective CreateKing(TeamId team, CharacterStatsConfig profile)
        {
            var root = new GameObject(team + " King");
            try
            {
                root.AddComponent<NetworkObject>(); root.AddComponent<NetworkTransform>();
                Set(root.AddComponent<CharacterStats>(), "configuration", profile);
                root.AddComponent<NetworkCharacterStats>(); root.AddComponent<DamageReceiver>(); root.AddComponent<Targetable>();
                root.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC);
                var king = root.AddComponent<NetworkKingObjective>(); Team(king, team);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "King Body";
                body.transform.SetParent(root.transform, false); body.transform.localPosition = Vector3.up * 1.25f;
                body.transform.localScale = new Vector3(1.3f, 1.25f, 1.3f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = team == TeamId.TeamA ? Color.blue : Color.red };
                AssetDatabase.CreateAsset(material, MaterialFolder + "/" + team + ".mat"); body.GetComponent<Renderer>().sharedMaterial = material;
                var crown = GameObject.CreatePrimitive(PrimitiveType.Cube); crown.name = "Crown";
                crown.transform.SetParent(root.transform, false); crown.transform.localPosition = Vector3.up * 2.6f;
                crown.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);
                UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
                var gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Crown.mat");
                crown.GetComponent<Renderer>().sharedMaterial = gold;
                var label = new GameObject("King Label").AddComponent<TextMesh>(); label.transform.SetParent(root.transform, false);
                label.transform.localPosition = Vector3.up * 3.2f; label.text = team + " King";
                label.anchor = TextAnchor.MiddleCenter; label.characterSize = 0.15f; label.fontSize = 40;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + team + "King.prefab");
                return prefab.GetComponent<NetworkKingObjective>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static NetworkEnemy CreateGuard(TeamId team)
        {
            string path = Folder + "/" + team + "Guard.prefab";
            if (!AssetDatabase.CopyAsset(MultiplayerPveSceneBuilder.Folder + "/NetworkKnight.prefab", path)) throw new InvalidOperationException("Guard copy failed.");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = team + " Fortress Guard";
                (root.GetComponent<TeamIdentity>() ?? root.AddComponent<TeamIdentity>()).TryConfigure(team, CombatantKind.NPC);
                Team(root.AddComponent<NetworkFortressGuard>(), team);
                root.GetComponentInChildren<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + team + ".mat");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<NetworkEnemy>(path);
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Team(UnityEngine.Object target, TeamId team)
        { var data = new SerializedObject(target); data.FindProperty("team").intValue = (int)team; data.ApplyModifiedPropertiesWithoutUndo(); }
        public static void Register()
        {
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset")) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            foreach (var name in new[] { "NetworkMatch", "TeamAKing", "TeamBKing", "TeamAGuard", "TeamBGuard" })
                if (!Fusion.Editor.NetworkProjectConfigUtilities.TryGetPrefabId(Folder + "/" + name + ".prefab", out _)) throw new InvalidOperationException("Unregistered " + name);
            AssetDatabase.SaveAssets();
        }
    }
}
