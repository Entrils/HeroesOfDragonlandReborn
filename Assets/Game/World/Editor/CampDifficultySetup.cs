using System;
using System.Linq;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.World.Camps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    // Content migration only: all tiers retain the same authoritative camp lifecycle.
    public static class CampDifficultySetup
    {
        private const string Folder = BattlefieldSceneBuilder.Folder;
        private static readonly string[] Names = { "Low", "Medium", "High", "Boss" };
        private static readonly int[] Xp = { 25, 50, 100, 300 };
        private static readonly int[] Respawn = { 75, 105, 150, 210 };
        private static readonly float[][] Weights = {
            new[] { 1f, .5f, 0f, 0f, 0f }, new[] { 0f, 1f, .6f, 0f, 0f },
            new[] { 0f, 0f, 1f, .5f, 0f }, new[] { 0f, 0f, 0f, 1f, .1f }
        };

        private static void Edit(UnityEngine.Object target, Action<SerializedObject> edit)
        {
            Undo.RecordObject(target, "Configure camp difficulty");
            var data = new SerializedObject(target);
            edit(data);
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static T Copy<T>(string source, string destination) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(destination);
            if (asset != null) return asset;
            if (!AssetDatabase.CopyAsset(source, destination)) throw new InvalidOperationException("Cannot copy " + source);
            return AssetDatabase.LoadAssetAtPath<T>(destination);
        }

        [MenuItem("Heroes of Dragonland/Battlefield/Apply Camp Difficulty Tiers")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != BattlefieldSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open DragonlandBattlefield in Edit Mode.");
            Copy<LootTable>(Folder + "/HighLoot.asset", Folder + "/BossLoot.asset");
            Copy<GameObject>(Folder + "/HighChest.prefab", Folder + "/BossChest.prefab");
            Copy<NetworkCampDefinition>(Folder + "/HighCamp.asset", Folder + "/BossCamp.asset");
            Copy<GameObject>(Folder + "/HighCamp.prefab", Folder + "/BossCamp.prefab");
            for (int tier = 0; tier < Names.Length; tier++)
            {
                string prefix = Folder + "/" + Names[tier];
                var loot = AssetDatabase.LoadAssetAtPath<LootTable>(prefix + "Loot.asset");
                Edit(loot, data => {
                    data.FindProperty("minimumDrops").intValue = tier < 2 ? 1 : tier == 2 ? 2 : 3;
                    data.FindProperty("maximumDrops").intValue = tier + 1;
                    for (int r = 0; r < 5; r++) data.FindProperty("rarityWeights").GetArrayElementAtIndex(r).floatValue = Weights[tier][r];
                });
                Edit(AssetDatabase.LoadAssetAtPath<GameObject>(prefix + "Chest.prefab").GetComponent<HeroesOfDragonland.World.LootChest>(),
                    data => data.FindProperty("lootTable").objectReferenceValue = loot);
                var definition = AssetDatabase.LoadAssetAtPath<NetworkCampDefinition>(prefix + "Camp.asset");
                Edit(definition, data => {
                    data.FindProperty("difficulty").enumValueIndex = tier;
                    data.FindProperty("experienceReward").intValue = Xp[tier];
                    data.FindProperty("experienceRadius").floatValue = 9;
                    data.FindProperty("initialSpawnDelay").floatValue = tier == 3 ? 150 : 0;
                    data.FindProperty("respawnDelay").floatValue = Respawn[tier];
                    data.FindProperty("chestPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkLootChest>(prefix + "Chest.prefab");
                });
                if (tier == 3) NeutralBossSetup.Configure(definition);
                var camp = AssetDatabase.LoadAssetAtPath<GameObject>(prefix + "Camp.prefab").GetComponent<NetworkCamp>();
                Edit(camp, data => data.FindProperty("definition").objectReferenceValue = definition);
                if (!definition.TryValidate(out var reason)) throw new InvalidOperationException(reason);
            }
            ConfigureAnchors();
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CampDifficulty] Saved 4 Tier 1, 4 Tier 2, 2 Tier 3 and one central contested camp.");
        }

        private static void ConfigureAnchors()
        {
            var spawner = UnityEngine.Object.FindFirstObjectByType<NetworkCampSpawner>();
            Edit(spawner, data => {
                var entries = data.FindProperty("entries");
                entries.arraySize = 11;
                for (int i = 0; i < 11; i++)
                {
                    int tier = i < 4 ? 0 : i < 8 ? 1 : i == 8 ? 3 : 2;
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkCamp>(Folder + "/" + Names[tier] + "Camp.prefab");
                    if (i >= 8) entry.FindPropertyRelative("localPosition").vector3Value = i == 8 ? Vector3.zero : new Vector3(0, 0, i == 9 ? 18 : -18);
                    if (i >= 8) entry.FindPropertyRelative("yaw").floatValue = i == 10 ? 180 : 0;
                    string markerName = "Camp " + (i + 1) + " |";
                    var marker = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.StartsWith(markerName));
                    if (marker == null)
                    {
                        var template = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name.StartsWith("Camp 9 |"));
                        marker = UnityEngine.Object.Instantiate(template, template.parent);
                        Undo.RegisterCreatedObjectUndo(marker.gameObject, "Add Tier 3 camp marker");
                    }
                    Undo.RecordObject(marker, "Label camp tier");
                    marker.name = markerName + " " + (CampDifficulty)tier;
                    marker.position = spawner.transform.TransformPoint(entry.FindPropertyRelative("localPosition").vector3Value);
                    var label = marker.GetComponentInChildren<TextMesh>();
                    Undo.RecordObject(label, "Label camp rewards");
                    label.text = (tier == 3 ? "CONTESTED" : "TIER " + (tier + 1)) + " / " + Xp[tier] + " XP / " + Respawn[tier] + "s" + (tier == 3 ? " / OPENS 2:30" : "");
                }
            });
        }
    }
}

