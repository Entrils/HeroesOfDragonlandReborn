using System;
using System.Linq;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class WeaponMovesetSetup
    {
        public const string Folder = "Assets/Game/Data/Weapons/Movesets";
        [MenuItem("Heroes of Dragonland/Combat/Build Weapon Movesets")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            HumanoidAssetBuilder.Folder(Folder);
            var library = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrary>(ProductionCharacterAnimatorBuilder.LibraryPath);
            if (library == null) throw new InvalidOperationException("Build the humanoid animation pipeline first.");
            foreach (WeaponArchetype kind in Enum.GetValues(typeof(WeaponArchetype)))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + kind + ".asset");
                if (weapon == null) throw new InvalidOperationException("Missing weapon " + kind);
                string path = Folder + "/" + kind + "Moveset.asset";
                var moveset = AssetDatabase.LoadAssetAtPath<WeaponMoveset>(path);
                if (moveset == null)
                {
                    moveset = ScriptableObject.CreateInstance<WeaponMoveset>();
                    AssetDatabase.CreateAsset(moveset, path);
                    var data = new SerializedObject(moveset);
                    data.FindProperty("archetype").enumValueIndex = (int)kind;
                    data.FindProperty("animationSet").objectReferenceValue = weapon.AttackAnimationSet;
                    var lights = data.FindProperty("lightAttacks");
                    lights.arraySize = kind == WeaponArchetype.Greatsword ? 2 : 3;
                    for (int i = 0; i < lights.arraySize; i++)
                        Configure(lights.GetArrayElementAtIndex(i), weapon, i, false, library);
                    Configure(data.FindProperty("heavyAttack"), weapon, 0, true, library);
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var definition = new SerializedObject(weapon);
                definition.FindProperty("moveset").objectReferenceValue = moveset;
                definition.ApplyModifiedPropertiesWithoutUndo();
                var set = new SerializedObject(weapon.AttackAnimationSet);
                if (set.FindProperty("lightAttack").objectReferenceValue == null)
                    set.FindProperty("lightAttack").objectReferenceValue = library.Get(CharacterMotion.LightAttack);
                if (set.FindProperty("heavyAttack").objectReferenceValue == null)
                    set.FindProperty("heavyAttack").objectReferenceValue = library.Get(CharacterMotion.HeavyAttack);
                set.ApplyModifiedPropertiesWithoutUndo();
                if (!weapon.TryValidate(out string error)) throw new InvalidOperationException(kind + ": " + error);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponMovesets] Built/linked four reusable movesets; existing authored moves preserved.");
        }
        private static void Configure(SerializedProperty move, WeaponDefinition weapon, int index, bool heavy, CharacterAnimationLibrary library)
        {
            var source = weapon.AttackAnimationSet.CombatProfile;
            string path = Folder + "/" + weapon.Archetype + (heavy ? "Heavy" : "Light" + (index + 1)) + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<MeleeAttackDefinition>(path);
            if (profile == null)
            {
                profile = UnityEngine.Object.Instantiate(source);
                profile.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(profile, path);
                var data = new SerializedObject(profile);
                float timing = heavy ? source.HeavyTimingMultiplier : 1 + .08f * index;
                data.FindProperty("attackDuration").floatValue = source.AttackDuration * timing;
                data.FindProperty("recoveryDuration").floatValue = source.RecoveryDuration * timing;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            move.FindPropertyRelative("profile").objectReferenceValue = profile;
            // Shared placeholder Humanoid art; each step can be replaced without changing the Animator graph.
            AnimationClip clip = library.Get(heavy ? CharacterMotion.HeavyAttack : CharacterMotion.LightAttack);
            if (!heavy && index == 1)
            {
                var alternate = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Animations/PrototypeHumanoid/FBX/AN_PrototypeHumanoid_Attack.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (alternate != null) clip = alternate;
            }
            move.FindPropertyRelative("animation").objectReferenceValue = clip;
            move.FindPropertyRelative("attackSpeed").floatValue = weapon.AttackSpeed;
            move.FindPropertyRelative("staminaCost").floatValue = weapon.StaminaCost * (heavy ? source.HeavyStaminaMultiplier : 1);
            move.FindPropertyRelative("range").floatValue = weapon.Range;
            move.FindPropertyRelative("movementMultiplier").floatValue = heavy ? source.MovementMultiplier * .5f : source.MovementMultiplier;
            move.FindPropertyRelative("staggerPower").floatValue = source.StaggerPower * (heavy ? source.HeavyStaggerMultiplier : 1 + .15f * index);
            move.FindPropertyRelative("damageMultiplier").floatValue = heavy ? source.HeavyDamageMultiplier : 1 + .1f * index;
        }
    }
}
