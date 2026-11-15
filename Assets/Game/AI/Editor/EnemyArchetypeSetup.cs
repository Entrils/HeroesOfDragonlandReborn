using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.AI.Editor
{
    public static class EnemyArchetypeSetup
    {
        private const string Folder = "Assets/Game/Data/EnemyArchetypes";
        [MenuItem("Heroes of Dragonland/Enemies/Install Archetypes")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Data", "EnemyArchetypes");
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/Game/Data" }))
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var kind = d.name.IndexOf("Wolf", StringComparison.OrdinalIgnoreCase) >= 0 ? EnemyArchetype.Wolf
                    : d.name.IndexOf("Bear", StringComparison.OrdinalIgnoreCase) >= 0 ? EnemyArchetype.Bear
                    : d.name.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0 ? EnemyArchetype.Knight : EnemyArchetype.Standard;
                if (kind != EnemyArchetype.Standard) Tune(d, kind);
            }
            CreateRanged("Archer", EnemyArchetype.Archer);
            CreateRanged("Mage", EnemyArchetype.Mage);
            // The high camp retains three members and rewards, with one of each humanoid role.
            var camp = AssetDatabase.LoadMainAssetAtPath("Assets/Game/Data/Battlefield/HighCamp.asset");
            if (camp != null)
            {
                var data = new SerializedObject(camp); var enemies = data.FindProperty("enemies");
                if (enemies.arraySize >= 3)
                {
                    enemies.GetArrayElementAtIndex(1).FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HeroesOfDragonland.Multiplayer.NetworkEnemy>(Folder + "/Archer.prefab");
                    enemies.GetArrayElementAtIndex(2).FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HeroesOfDragonland.Multiplayer.NetworkEnemy>(Folder + "/Mage.prefab");
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset"))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("[Archetypes] Installed Wolf/Bear/Knight tuning and Archer/Mage in battlefield high camps.");
        }
        private static void Tune(EnemyDefinition d, EnemyArchetype kind)
        {
            var s = new SerializedObject(d);
            s.FindProperty("archetype").enumValueIndex = (int)kind;
            Set(s, "chaseSpeed", kind == EnemyArchetype.Wolf ? 5.5f : kind == EnemyArchetype.Bear ? 2f : 3.2f);
            Set(s, "attackWindup", kind == EnemyArchetype.Wolf ? .18f : kind == EnemyArchetype.Bear ? 1.1f : .45f);
            Set(s, "attackCooldown", kind == EnemyArchetype.Wolf ? .85f : kind == EnemyArchetype.Bear ? 2.8f : 1.6f);
            Set(s, "staggerPower", kind == EnemyArchetype.Bear ? 75 : kind == EnemyArchetype.Wolf ? 12 : 30);
            Set(s, "alertDuration", kind == EnemyArchetype.Wolf ? .1f : .4f);
            if (kind == EnemyArchetype.Bear) Set(s, "damage", Mathf.Max(24, d.Damage));
            s.ApplyModifiedPropertiesWithoutUndo();
            if (kind == EnemyArchetype.Knight)
            {
                var guardStats = new SerializedObject(d.Stats);
                Set(guardStats, "maxStamina", Mathf.Max(100, d.Stats.MaxStamina));
                Set(guardStats, "staminaRegenPerSecond", 20); Set(guardStats, "staminaRegenDelay", 1);
                guardStats.ApplyModifiedPropertiesWithoutUndo();
            }
            if (kind == EnemyArchetype.Bear)
            {
                var stats = new SerializedObject(d.Stats);
                Set(stats, "maxHealth", Mathf.Max(d.Stats.MaxHealth, d.name.Contains("Boss") ? 400 : d.name.Contains("High") ? 220 : 160));
                stats.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        private static void CreateRanged(string name, EnemyArchetype kind)
        {
            string path = Folder + "/" + name;
            var stats = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(path + "Stats.asset");
            if (stats == null) { stats = ScriptableObject.CreateInstance<CharacterStatsConfig>(); AssetDatabase.CreateAsset(stats, path + "Stats.asset"); }
            var fields = new SerializedObject(stats);
            Set(fields, "maxHealth", kind == EnemyArchetype.Mage ? 75 : 90);
            Set(fields, "maxMana", 90); Set(fields, "manaRegenPerSecond", 8); Set(fields, "manaRegenDelay", 2);
            fields.ApplyModifiedPropertiesWithoutUndo();
            var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path + ".asset");
            if (d == null) { d = ScriptableObject.CreateInstance<EnemyDefinition>(); AssetDatabase.CreateAsset(d, path + ".asset"); }
            fields = new SerializedObject(d); fields.FindProperty("stats").objectReferenceValue = stats;
            fields.FindProperty("archetype").enumValueIndex = (int)kind;
            Set(fields, "attackRange", 12); Set(fields, "preferredDistance", 8); Set(fields, "detectionRadius", 12);
            Set(fields, "leashDistance", 18); Set(fields, "chaseSpeed", kind == EnemyArchetype.Archer ? 4.2f : 3);
            Set(fields, "attackWindup", kind == EnemyArchetype.Archer ? .65f : 1);
            Set(fields, "attackCooldown", kind == EnemyArchetype.Archer ? 1.8f : 2.2f);
            Set(fields, "projectileSpeed", kind == EnemyArchetype.Archer ? 18 : 10);
            Set(fields, "damage", kind == EnemyArchetype.Archer ? 14 : 28);
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (AssetDatabase.LoadMainAssetAtPath(path + ".prefab") == null &&
                !AssetDatabase.CopyAsset("Assets/Game/Data/MultiplayerPve/NetworkKnight.prefab", path + ".prefab"))
                throw new InvalidOperationException("Cannot clone shared enemy prefab.");
            var root = PrefabUtility.LoadPrefabContents(path + ".prefab");
            try
            {
                root.name = name;
                fields = new SerializedObject(root.GetComponent<EnemyBrain>()); fields.FindProperty("definition").objectReferenceValue = d; fields.ApplyModifiedPropertiesWithoutUndo();
                fields = new SerializedObject(root.GetComponent<CharacterStats>()); fields.FindProperty("configuration").objectReferenceValue = stats; fields.ApplyModifiedPropertiesWithoutUndo();
                var label = root.GetComponentInChildren<TextMesh>(); if (label != null) label.text = name;
                PrefabUtility.SaveAsPrefabAsset(root, path + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Set(SerializedObject s, string name, float value) => s.FindProperty(name).floatValue = value;
    }
}
