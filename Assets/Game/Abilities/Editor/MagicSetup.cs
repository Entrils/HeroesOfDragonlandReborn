using System;
using HeroesOfDragonland.Skills;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Abilities.Editor
{
    public static class MagicSetup
    {
        public const string Folder = "Assets/Game/Data/Abilities";
        [MenuItem("Heroes of Dragonland/Magic/Install Initial Magic")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Data", "Abilities");
            var definitions = new[] {
                Create("FireProjectile", "Fire Projectile", "Launch fire straight ahead. Allies are ignored; solid geometry stops it.", AbilityEffect.FireProjectile, AbilityTarget.Forward, 20, 4, .6f, 20, 30, 1),
                Create("HealingSpell", "Short-range Healing Spell", "Heal the nearest injured ally in front within 5m, or yourself. Requires line of sight; never revives.", AbilityEffect.Heal, AbilityTarget.SelfOrAlly, 25, 7, 1, 5, 35, 1),
                Create("MagicShield", "Defensive Magic Shield", "Absorb up to 40 damage for 6 seconds. Recasting replaces the shield.", AbilityEffect.Shield, AbilityTarget.Self, 25, 10, .4f, 1, 40, 6)
            };
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<SessionSkills>() == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(root.GetComponent<SessionSkills>(), definitions);
                    PrefabUtility.SaveAsPrefabAsset(root, path); count++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("[MagicSetup] Installed three ability assets and session unlocks on " + count + " player prefabs.");
        }
        private static void Configure(SessionSkills skills, SkillDefinition[] definitions)
        {
            var root = skills.gameObject;
            if (root.GetComponent<AbilityCaster>() == null) root.AddComponent<AbilityCaster>();
            if (root.GetComponent<NetworkSessionSkills>() != null && root.GetComponent<NetworkAbilities>() == null) root.AddComponent<NetworkAbilities>();
            if (root.GetComponent<AbilityPresentation>() == null) root.AddComponent<AbilityPresentation>();
            var data = new SerializedObject(skills); var entries = data.FindProperty("definitions"); entries.arraySize = SessionSkills.SkillCount;
            for (int i = 0; i < 3; i++) entries.GetArrayElementAtIndex(3 + i).objectReferenceValue = definitions[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static SkillDefinition Create(string id, string title, string description, AbilityEffect effect, AbilityTarget target,
            float mana, float cooldown, float cast, float range, float magnitude, float duration)
        {
            string path = Folder + "/" + id + ".asset";
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<AbilityDefinition>(); var data = new SerializedObject(ability);
                data.FindProperty("effect").enumValueIndex = (int)effect; data.FindProperty("target").enumValueIndex = (int)target;
                data.FindProperty("manaCost").floatValue = mana; data.FindProperty("cooldown").floatValue = cooldown;
                data.FindProperty("castTime").floatValue = cast; data.FindProperty("range").floatValue = range;
                data.FindProperty("magnitude").floatValue = magnitude; data.FindProperty("duration").floatValue = duration;
                data.FindProperty("animationTrigger").stringValue = "CastMagic";
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(ability, path);
            }
            if (!ability.IsValid) throw new InvalidOperationException("Invalid ability: " + path);
            path = Folder + "/" + id + "Skill.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillDefinition>(); var data = new SerializedObject(skill);
                data.FindProperty("displayName").stringValue = title; data.FindProperty("description").stringValue = description;
                data.FindProperty("effect").enumValueIndex = (int)SkillEffect.Ability; data.FindProperty("ability").objectReferenceValue = ability;
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(skill, path);
            }
            if (!skill.IsValid) throw new InvalidOperationException("Invalid skill: " + path);
            return skill;
        }
    }
}
