using System;
using HeroesOfDragonland.Skills;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class SkillTreeMilestoneSetup
    {
        private const string SkillsFolder = "Assets/Game/Data/Skills/";

        [MenuItem("Heroes of Dragonland/Skills/Install Session Skill Tree")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            var melee = LoadSkill(SkillsFolder + "MeleeTraining.asset");
            var meleeData = new SerializedObject(melee);
            meleeData.FindProperty("maxRank").intValue = 3;
            meleeData.FindProperty("description").stringValue = "Adds 5 damage to melee hits per rank.";
            meleeData.ApplyModifiedPropertiesWithoutUndo();

            var guard = Passive("GuardEfficiency", "Efficient Guard", "Spend 12% less guard stamina per rank.",
                SkillBranch.Warrior, SkillEffect.GuardEfficiency, .12f, 2);
            var stamina = Passive("Stamina", "Endurance", "Gain 12 maximum stamina per rank.",
                SkillBranch.Agility, SkillEffect.Stamina, 12, 3);
            var mana = Passive("Mana", "Mana Reserve", "Gain 12 maximum mana per rank.",
                SkillBranch.Magic, SkillEffect.Mana, 12, 3);
            var health = Passive("Health", "Vitality", "Gain 15 maximum health per rank.",
                SkillBranch.Survival, SkillEffect.Health, 15, 3);

            const string abilities = "Assets/Game/Data/Abilities/";
            SkillDefinition[] nodes =
            {
                LoadSkill(SkillsFolder + "PowerStrike.asset"), LoadSkill(SkillsFolder + "Guard.asset"), melee,
                MagicSkill(abilities + "FireProjectileSkill.asset"), MagicSkill(abilities + "HealingSpellSkill.asset"),
                MagicSkill(abilities + "MagicShieldSkill.asset"),
                guard,
                Passive("Poise", "Steadfast", "Gain 12 maximum poise per rank.", SkillBranch.Warrior, SkillEffect.Poise, 12, 2, guard),
                stamina,
                Passive("DodgeEfficiency", "Economical Dodge", "Spend 12% less stamina on dodges per rank.",
                    SkillBranch.Agility, SkillEffect.DodgeEfficiency, .12f, 2, stamina),
                Passive("AttackSpeed", "Quick Strikes", "Gain 10% melee and bow attack speed per rank.",
                    SkillBranch.Agility, SkillEffect.AttackSpeed, .1f, 2, stamina, 2),
                mana,
                Passive("SpellEfficiency", "Spell Efficiency", "Spend 12% less mana on spells per rank.",
                    SkillBranch.Magic, SkillEffect.SpellEfficiency, .12f, 2, mana),
                Passive("SpellCooldown", "Quick Study", "Reduce spell cooldowns by 12% per rank.",
                    SkillBranch.Magic, SkillEffect.SpellCooldown, .12f, 2, mana, 2),
                health,
                Passive("PotionEfficiency", "Restorative Training", "Restore 15% more health, stamina and mana from potions per rank.",
                    SkillBranch.Survival, SkillEffect.PotionEfficiency, .15f, 2, health),
                Passive("Defense", "Resilience", "Take 6% less damage per rank.",
                    SkillBranch.Survival, SkillEffect.Defense, .06f, 2, health, 2)
            };
            var tree = LoadOrCreate<SkillTreeDefinition>(SkillsFolder + "SessionSkillTree.asset");
            var data = new SerializedObject(tree);
            var entries = data.FindProperty("nodes");
            entries.arraySize = nodes.Length;
            for (int i = 0; i < nodes.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!tree.IsValid()) throw new InvalidOperationException("Session Skill Tree failed validation.");

            AssignTree("Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab", tree);
            AssignTree("Assets/Game/Data/MultiplayerFortress/NetworkFortressPlayer.prefab", tree);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillTree] Installed {tree.Count} nodes on both multiplayer player prefabs.");
        }

        private static SkillDefinition Passive(string id, string title, string description, SkillBranch branch,
            SkillEffect effect, float magnitude, int ranks, SkillDefinition prerequisite = null, int requiredRank = 1)
        {
            var skill = LoadOrCreate<SkillDefinition>(SkillsFolder + id + ".asset");
            var data = new SerializedObject(skill);
            data.FindProperty("displayName").stringValue = title;
            data.FindProperty("description").stringValue = description;
            data.FindProperty("branch").intValue = (int)branch;
            data.FindProperty("effect").intValue = (int)effect;
            data.FindProperty("magnitude").floatValue = magnitude;
            data.FindProperty("maxRank").intValue = ranks;
            var requirements = data.FindProperty("prerequisites");
            requirements.arraySize = prerequisite == null ? 0 : 1;
            if (prerequisite != null)
            {
                var entry = requirements.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("skill").objectReferenceValue = prerequisite;
                entry.FindPropertyRelative("rank").intValue = requiredRank;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static void AssignTree(string path, SkillTreeDefinition tree)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var skills = scope.prefabContentsRoot.GetComponent<SessionSkills>();
            if (skills == null) throw new InvalidOperationException($"Missing SessionSkills at {path}");
            var data = new SerializedObject(skills);
            data.FindProperty("tree").objectReferenceValue = tree;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SkillDefinition MagicSkill(string path)
        {
            var skill = LoadSkill(path);
            var data = new SerializedObject(skill);
            data.FindProperty("branch").intValue = (int)SkillBranch.Magic;
            data.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static SkillDefinition LoadSkill(string path) =>
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(path) ?? throw new InvalidOperationException($"Missing {path}");

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
