using System;
using HeroesOfDragonland.AI;
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
                string[] names = { "CLEAVE - step aside", "SWEEP - get behind", "STOMP - move out" };
                float[] radii = { 4.5f, 5.5f, 5f }, angles = { 70, 180, 360 }, windups = { 1.1f, 1.5f, 1.8f }, damage = { 34, 28, 40 };
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
            var stats = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(Folder + "/BossKnightStats.asset");
            if (stats == null)
            {
                AssetDatabase.CopyAsset(Folder + "/HighKnightStats.asset", Folder + "/BossKnightStats.asset");
                stats = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(Folder + "/BossKnightStats.asset");
            }
            Edit(stats, data => {
                data.FindProperty("maxHealth").floatValue = 1800;
                data.FindProperty("staggerResistance").floatValue = 250;
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
            Edit(camp, data => {
                var members = data.FindProperty("enemies"); members.arraySize = 1;
                var member = members.GetArrayElementAtIndex(0);
                member.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkEnemy>(path);
                member.FindPropertyRelative("offset").vector3Value = Vector3.zero;
                member.FindPropertyRelative("yaw").floatValue = 0;
                data.FindProperty("experienceReward").intValue = 300;
            });
        }

        [MenuItem("Heroes of Dragonland/Battlefield/Configure Neutral Boss")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure the boss in Edit Mode.");
            Configure(AssetDatabase.LoadAssetAtPath<NetworkCampDefinition>(Folder + "/BossCamp.asset"));
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            AssetDatabase.SaveAssets();
            Debug.Log("[NeutralBoss] Saved 1800 HP neutral Stone Warden with cleave, sweep and stomp. Camp reward: 300 XP and existing Epic/Legendary chest.");
        }
    }
}
