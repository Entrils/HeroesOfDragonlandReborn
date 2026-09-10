using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); }
    public void Execute(ExecutionResult result)
    {
        Check(Application.isPlaying, "Run in Play Mode");
        var actor = new GameObject("Attribute validation");
        actor.SetActive(false);
        var curve = ScriptableObject.CreateInstance<LevelProgressionDefinition>();
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
        try
        {
            var c = new SerializedObject(curve);
            var levels = c.FindProperty("levels"); levels.arraySize = 1;
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("requiredXP").intValue = 1;
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("progressionPoints").intValue = 10;
            c.ApplyModifiedPropertiesWithoutUndo();
            var p = actor.AddComponent<SessionProgression>();
            var ps = new SerializedObject(p); ps.FindProperty("definition").objectReferenceValue = curve; ps.ApplyModifiedPropertiesWithoutUndo();
            var a = actor.AddComponent<CharacterAttributes>();
            var inv = actor.AddComponent<Inventory>();
            var gear = actor.AddComponent<Equipment>();
            var stats = actor.AddComponent<CharacterStats>();
            var data = new SerializedObject(item);
            data.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
            data.FindProperty("displayName").stringValue = "Attribute test sword";
            data.FindProperty("equipmentSlot").intValue = (int)EquipmentSlot.MainHand;
            var requirements = data.FindProperty("equipmentRequirements"); requirements.arraySize = 1;
            requirements.GetArrayElementAtIndex(0).FindPropertyRelative("stat").intValue = (int)ItemStat.Strength;
            requirements.GetArrayElementAtIndex(0).FindPropertyRelative("minimumValue").floatValue = 11;
            var modifiers = data.FindProperty("statModifiers"); modifiers.arraySize = 3;
            ItemStat[] modifierStats = { ItemStat.Vitality, ItemStat.Vitality, ItemStat.Strength };
            float[] values = { 2, 0.5f, 100 };
            for (int i = 0; i < 3; i++)
            {
                var m = modifiers.GetArrayElementAtIndex(i);
                m.FindPropertyRelative("stat").intValue = (int)modifierStats[i];
                m.FindPropertyRelative("operation").intValue = i == 1 ? 1 : 0;
                m.FindPropertyRelative("value").floatValue = values[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var cat = new SerializedObject(catalog); cat.FindProperty("items").arraySize = 1;
            cat.FindProperty("items").GetArrayElementAtIndex(0).objectReferenceValue = item; cat.ApplyModifiedPropertiesWithoutUndo();
            var ins = new SerializedObject(inv); ins.FindProperty("catalog").objectReferenceValue = catalog; ins.ApplyModifiedPropertiesWithoutUndo();
            actor.SetActive(true);
            Check(p.TryBeginSession("a", out _), "session starts");
            Check(!p.TryIncreaseAttribute(ItemStat.Vitality, "a", out _), "no free allocations");
            Check(p.TryAddExperience(1, "a", "xp", out _), "grant points");
            long revision = p.Revision;
            Check(!p.TryIncreaseAttribute(ItemStat.Level, "a", out _) && !p.TryIncreaseAttribute(ItemStat.Mind, "old", out _)
                && p.ProgressionPoints == 10 && p.Revision == revision, "invalid/stale allocation atomic");
            p.SetAuthority(false);
            Check(!p.TryIncreaseAttribute(ItemStat.Vitality, "a", out _), "authority enforced"); p.SetAuthority(true);
            bool reentrant = true;
            Action observer = () => reentrant = p.TryIncreaseAttribute(ItemStat.Mind, "a", out _);
            p.Changed += observer;
            Check(p.TryIncreaseAttribute(ItemStat.Vitality, "a", out _), "spend one");
            p.Changed -= observer;
            Check(!reentrant && p.ProgressionPoints == 9 && p.GetAllocatedPoints(ItemStat.Vitality) == 1, "single atomic spend");
            Check(stats.MaxHealth == 110 && stats.CurrentHealth == 100, "capacity increase does not heal");
            Check(a.TryGetUnallocatedBaseValue(ItemStat.Vitality, out float b) && b == 10
                && a.TryGetValue(ItemStat.Vitality, out float v) && v == 11, "base and final distinct");
            Check(inv.TryAdd(item) && !gear.TryEquip(0, EquipmentSlot.MainHand, out _), "gear cannot satisfy own requirement");
            Check(p.TryIncreaseAttribute(ItemStat.Strength, "a", out _) && gear.TryEquip(0, EquipmentSlot.MainHand, out _), "allocated strength unlocks equipment");
            Check(a.TryGetValue(ItemStat.Vitality, out v) && v == 19.5f && a.GetEquipmentModifier(ItemStat.Vitality) == 8.5f
                && stats.MaxHealth == 195, "flat and percent attributes drive resource maximum");
            Check(gear.TryUnequip(EquipmentSlot.MainHand, out _) && stats.MaxHealth == 110, "unequip removes modifiers");
            Check(p.TryIncreaseAttribute(ItemStat.Endurance, "a", out _) && p.TryIncreaseAttribute(ItemStat.Mind, "a", out _)
                && p.TryIncreaseAttribute(ItemStat.Dexterity, "a", out _) && stats.MaxStamina == 105 && stats.MaxMana == 105, "all five attributes allocate");
            Check(p.TryBeginSession("a", out _) && p.GetAllocatedPoints(ItemStat.Vitality) == 1, "same session idempotent");
            actor.SetActive(false);
            Check(p.TryBeginSession("b", out _), "reset while inactive"); actor.SetActive(true);
            Check(p.ProgressionPoints == 0 && p.GetAllocatedPoints(ItemStat.Vitality) == 0
                && stats.MaxHealth == 100 && stats.MaxStamina == 100 && stats.MaxMana == 100, "new match clears allocation and maxima");
            stats.ApplyDamage(1000);
            Check(p.TryAddExperience(1, "b", "xp", out _) && p.TryIncreaseAttribute(ItemStat.Vitality, "b", out _)
                && stats.IsDead && stats.CurrentHealth == 0, "allocation never revives");
            Check(!EditorUtility.scriptCompilationFailed, "Editor compilation");
            result.Log("PASS: session attributes, atomic spending, authority, reentrancy, gear requirements/modifiers, resource capacity and match reset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(curve);
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }
}
