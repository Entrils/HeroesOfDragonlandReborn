using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class AttackChainSetup
    {
        public const string Folder = "Assets/Game/Data/Weapons/Combos";
        [MenuItem("Heroes of Dragonland/Combat/Build Attack Chains")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            WeaponMovesetSetup.Build(); HumanoidAssetBuilder.Folder(Folder);
            foreach (WeaponArchetype kind in Enum.GetValues(typeof(WeaponArchetype)))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + kind + ".asset");
                var moveset = weapon.Moveset;
                if (moveset.Combo != null) continue;
                string path = Folder + "/" + kind + "Combo.asset";
                var combo = AssetDatabase.LoadAssetAtPath<WeaponComboDefinition>(path);
                if (combo == null)
                {
                    combo = ScriptableObject.CreateInstance<WeaponComboDefinition>(); AssetDatabase.CreateAsset(combo, path);
                    var data = new SerializedObject(combo); var steps = data.FindProperty("lightSteps");
                    steps.arraySize = moveset.LightAttackCount;
                    for (int i = 0; i < steps.arraySize; i++)
                    {
                        var rule = steps.GetArrayElementAtIndex(i);
                        Configure(rule, i + 1 < steps.arraySize ? i + 1 : -1,
                            kind != WeaponArchetype.Greatsword && i + 1 < steps.arraySize,
                            kind != WeaponArchetype.Greatsword);
                    }
                    Configure(data.FindProperty("heavyStep"), -1, false, false);
                    data.FindProperty("maximumChainAttacks").intValue = moveset.LightAttackCount + 1;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var m = new SerializedObject(moveset); m.FindProperty("combo").objectReferenceValue = combo;
                m.ApplyModifiedPropertiesWithoutUndo();
                if (!moveset.TryValidate(out string error)) throw new InvalidOperationException(kind + ": " + error);
            }
            AssetDatabase.SaveAssets(); Debug.Log("[AttackChains] Saved four finite configurable combo definitions.");
        }
        private static void Configure(SerializedProperty rule, int next, bool heavy, bool dodge)
        {
            rule.FindPropertyRelative("nextLightIndex").intValue = next;
            rule.FindPropertyRelative("allowHeavy").boolValue = heavy;
            rule.FindPropertyRelative("bufferStart").floatValue = .45f;
            rule.FindPropertyRelative("bufferEnd").floatValue = .92f;
            rule.FindPropertyRelative("minimumRecovery").floatValue = .5f;
            rule.FindPropertyRelative("allowDodgeCancel").boolValue = dodge;
            rule.FindPropertyRelative("dodgeCancelRecovery").floatValue = .85f;
        }
    }
}
