using System;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.UI;
using UnityEditor.SceneManagement;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.World.Camps;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Editor
{
    public static class NeutralBossSetup
    {
        private const string Folder = BattlefieldSceneBuilder.Folder;
        private static void Edit(UnityEngine.Object target, Action<SerializedObject> action)
        {
            Undo.RecordObject(target, "Configure neutral boss");
            var data = new SerializedObject(target); action(data); data.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        public static void Configure(NetworkCampDefinition camp)
        {
            var attacks = AssetDatabase.LoadAssetAtPath<EnemyAttackSet>(Folder + "/NeutralBossAttacks.asset");
            if (attacks == null)
            {
                attacks = ScriptableObject.CreateInstance<EnemyAttackSet>();
                AssetDatabase.CreateAsset(attacks, Folder + "/NeutralBossAttacks.asset");
            }
            Edit(attacks, data => {
                var moves = data.FindProperty("attacks"); moves.arraySize = 3;
                string[] names = { "CLEAVE - step aside", "HEAVY SWEEP - get behind", "STOMP - move out" };
                float[] radii = { 4.5f, 5.5f, 5f }, angles = { 70, 180, 360 }, windups = { 1.1f, 1.5f, 1.8f }, damage = { 28, 48, 36 };
                for (int i = 0; i < 3; i++)
                {
                    var move = moves.GetArrayElementAtIndex(i);
                    move.FindPropertyRelative("displayName").stringValue = names[i];
                    move.FindPropertyRelative("radius").floatValue = radii[i];
                    move.FindPropertyRelative("angle").floatValue = angles[i];
                    move.FindPropertyRelative("windup").floatValue = windups[i];
                    move.FindPropertyRelative("recovery").floatValue = 1.4f;
                    move.FindPropertyRelative("damage").floatValue = damage[i];
                }
            });
            var phaseTwo = AssetDatabase.LoadAssetAtPath<EnemyAttackSet>(Folder + "/WardenPhaseTwoAttacks.asset");
            if (phaseTwo == null) { phaseTwo = ScriptableObject.CreateInstance<EnemyAttackSet>(); AssetDatabase.CreateAsset(phaseTwo, Folder + "/WardenPhaseTwoAttacks.asset"); }
            Edit(phaseTwo, data => {
                var moves = data.FindProperty("attacks"); moves.arraySize = 4;
                string[] names = { "FRENZIED CLEAVE - step aside", "CRUSHING BLOW - dodge the cone", "SEISMIC STOMP - move out", "SHOCKWAVE - escape the ring" };
                float[] radii = { 4.5f, 5.5f, 5f, 7f }, angles = { 80, 80, 360, 360 }, windups = { .85f, 1.5f, 1.4f, 1.8f }, damage = { 34, 55, 42, 46 };
                for (int i = 0; i < 4; i++) {
                    var move = moves.GetArrayElementAtIndex(i);
                    move.FindPropertyRelative("displayName").stringValue = names[i];
                    move.FindPropertyRelative("radius").floatValue = radii[i]; move.FindPropertyRelative("angle").floatValue = angles[i];
                    move.FindPropertyRelative("windup").floatValue = windups[i]; move.FindPropertyRelative("recovery").floatValue = i == 1 ? 1.7f : 1.1f;
                    move.FindPropertyRelative("damage").floatValue = damage[i];
                }
            });
            var encounter = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(Folder + "/StoneWardenEncounter.asset");
            if (encounter == null) { encounter = ScriptableObject.CreateInstance<BossEncounterDefinition>(); AssetDatabase.CreateAsset(encounter, Folder + "/StoneWardenEncounter.asset"); }
            Edit(encounter, data => data.FindProperty("phaseTwoAttacks").objectReferenceValue = phaseTwo);
            var stats = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(Folder + "/BossKnightStats.asset");
            if (stats == null)
            {
                AssetDatabase.CopyAsset(Folder + "/HighKnightStats.asset", Folder + "/BossKnightStats.asset");
                stats = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(Folder + "/BossKnightStats.asset");
            }
            Edit(stats, data => {
                data.FindProperty("maxHealth").floatValue = 1800;
                data.FindProperty("poise").floatValue = 250;
            });
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Folder + "/BossKnight.asset");
            if (definition == null)
            {
                AssetDatabase.CopyAsset(Folder + "/HighKnight.asset", Folder + "/BossKnight.asset");
                definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Folder + "/BossKnight.asset");
            }
            Edit(definition, data => {
                data.FindProperty("stats").objectReferenceValue = stats;
                data.FindProperty("attackSet").objectReferenceValue = attacks;
                data.FindProperty("boss").objectReferenceValue = encounter;
                data.FindProperty("archetype").enumValueIndex = (int)EnemyArchetype.Standard;
                data.FindProperty("staggerPower").floatValue = 75;
                data.FindProperty("attackRange").floatValue = 4;
                data.FindProperty("detectionRadius").floatValue = 10;
                data.FindProperty("leashDistance").floatValue = 14;
                data.FindProperty("patrolRadius").floatValue = 0;
                data.FindProperty("chaseSpeed").floatValue = 2.8f;
                data.FindProperty("lostTargetTimeout").floatValue = 5;
            });
            string path = Folder + "/BossKnight.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset(Folder + "/HighKnight.prefab", path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = "Neutral Stone Warden";
                Edit(root.GetComponent<EnemyBrain>(), data => data.FindProperty("definition").objectReferenceValue = definition);
                Edit(root.GetComponent<CharacterStats>(), data => data.FindProperty("configuration").objectReferenceValue = stats);
                if (root.GetComponent<BossEncounter>() == null) root.AddComponent<BossEncounter>();
                if (root.GetComponent<BossTelegraphPresentation>() == null) root.AddComponent<BossTelegraphPresentation>();
                var body = root.transform.Find("Body");
                body.localScale = new Vector3(2.4f, 1.8f, 2.4f);
                body.localPosition = new Vector3(0, 1.8f, 0);
                var capsule = root.GetComponent<CapsuleCollider>();
                capsule.height = 3.6f; capsule.radius = 1.1f; capsule.center = Vector3.up * 1.8f;
                var agent = root.GetComponent<NavMeshAgent>();
                agent.radius = 1.1f; agent.height = 3.6f; agent.enabled = false;
                Edit(root.GetComponent<Targetable>(), data => data.FindProperty("localAimOffset").vector3Value = Vector3.up * 2);
                root.GetComponentInChildren<TextMesh>().transform.localPosition = Vector3.up * 4.5f;
                // Existing PvE prefabs are neutral; explicitly retain that identity if present.
                var team = root.GetComponent<HeroesOfDragonland.Teams.TeamIdentity>();
                if (team != null) Edit(team, data => data.FindProperty("team").enumValueIndex = 0);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var chest = CreateReliquary();
            Edit(camp, data => {
                data.FindProperty("chestPrefab").objectReferenceValue = chest;
                data.FindProperty("chestOffset").vector3Value = new Vector3(0, 0, -2);
                data.FindProperty("experienceRadius").floatValue = 20;
                var members = data.FindProperty("enemies"); members.arraySize = 1;
                var member = members.GetArrayElementAtIndex(0);
                member.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkEnemy>(path);
                member.FindPropertyRelative("offset").vector3Value = Vector3.zero;
                member.FindPropertyRelative("yaw").floatValue = 0;
                data.FindProperty("experienceReward").intValue = 300;
            });
        }


        private static NetworkLootChest CreateReliquary()
        {
            string lootPath = Folder + "/WardenReliquaryLoot.asset";
            var loot = AssetDatabase.LoadAssetAtPath<LootTable>(lootPath);
            if (loot == null) { AssetDatabase.CopyAsset(Folder + "/BossLoot.asset", lootPath); loot = AssetDatabase.LoadAssetAtPath<LootTable>(lootPath); }
            Edit(loot, data => {
                data.FindProperty("minimumDrops").intValue = 4; data.FindProperty("maximumDrops").intValue = 5;
                var weights = data.FindProperty("rarityWeights");
                for (int i = 0; i < 5; i++) weights.GetArrayElementAtIndex(i).floatValue = i == 3 ? 1 : i == 4 ? .5f : 0;
            });
            if (!loot.TryValidate(null, out var reason)) throw new InvalidOperationException(reason);
            string path = Folder + "/WardenReliquary.prefab";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CopyAsset(Folder + "/BossChest.prefab", path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                root.name = "Warden Reliquary";
                Edit(root.GetComponent<HeroesOfDragonland.World.LootChest>(), data => data.FindProperty("lootTable").objectReferenceValue = loot);
                var label = root.GetComponentInChildren<TextMesh>();
                if (label != null) { label.text = "WARDEN RELIQUARY\nEPIC / LEGENDARY - FIRST CLAIM WINS"; label.color = new Color(1, .8f, .2f); }
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/WardenGold.mat");
                if (material == null) {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", new Color(.8f, .48f, .08f)); material.SetFloat("_Metallic", .8f); material.SetFloat("_Smoothness", .65f);
                    AssetDatabase.CreateAsset(material, Folder + "/WardenGold.mat");
                }
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) if (renderer.GetComponent<TextMesh>() == null) renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<NetworkLootChest>(path);
        }
        private static void InstallBossHud()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(BattlefieldSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(BattlefieldSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try {
                foreach (var root in scene.GetRootGameObjects()) foreach (var hud in root.GetComponentsInChildren<GameplayHudPresenter>(true))
                    if (hud.GetComponent<BossHealthPresenter>() == null) Undo.AddComponent<BossHealthPresenter>(hud.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save boss HUD.");
            } finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem("Heroes of Dragonland/Battlefield/Configure Neutral Boss")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure the boss in Edit Mode.");
            Configure(AssetDatabase.LoadAssetAtPath<NetworkCampDefinition>(Folder + "/BossCamp.asset"));
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset"))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.SaveAssets();
            foreach (var path in new[] { Folder + "/WardenReliquary.prefab", Folder + "/BossKnight.prefab", "Assets/Game/Data/EnemyArchetypes/Archer.prefab", "Assets/Game/Data/EnemyArchetypes/Mage.prefab" })
                if (!Fusion.Editor.NetworkProjectConfigUtilities.TryGetPrefabId(path, out _)) throw new InvalidOperationException("Unregistered network prefab: " + path);
            InstallBossHud();
            Debug.Log("[NeutralBoss] Saved two-phase 1800 HP Stone Warden, 250 poise, seven attack variants and exclusive Warden Reliquary. Camp reward remains server-owned.");
        }
    }
}
