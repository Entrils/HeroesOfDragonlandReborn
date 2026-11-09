using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Items.Editor;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.UI;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); checks++; }
    public void Execute(ExecutionResult result)
    {
        Check(Application.isPlaying, "Play Mode"); ItemDefinitionValidation.ValidateAll();
        var temporary = new List<UnityEngine.Object>();
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
            foreach (string name in new[] { "Sword", "Greatsword", "Spear", "Staff" })
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + name + ".asset");
                Check(weapon != null && weapon.TryValidate(out _), name + " complete weapon asset");
                Check(weapon.Archetype.ToString() == name && weapon.WeaponPrefab.GetComponentsInChildren<Collider>().Length == 0, name + " correct archetype and visual-only prefab");
            }
            var actor = new GameObject("Weapon combat validation"); temporary.Add(actor); actor.SetActive(false);
            actor.transform.position = new Vector3(1000, 0, 1000);
            var inventory = actor.AddComponent<Inventory>(); var so = new SerializedObject(inventory); so.FindProperty("catalog").objectReferenceValue = catalog; so.ApplyModifiedPropertiesWithoutUndo();
            var attributes = actor.AddComponent<CharacterAttributes>(); var equipment = actor.AddComponent<Equipment>();
            var melee = actor.AddComponent<MeleeAttack>();
            var fallback = AssetDatabase.LoadAssetAtPath<MeleeAttackDefinition>("Assets/Game/Data/LightAttack.asset");
            so = new SerializedObject(melee); so.FindProperty("definition").objectReferenceValue = fallback; so.ApplyModifiedPropertiesWithoutUndo();
            actor.SetActive(true); var stats = actor.GetComponent<CharacterStats>();
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(target);
            target.transform.position = actor.transform.position + new Vector3(0, 1, 1.2f);
            var receiver = target.AddComponent<DamageReceiver>(); var targetStats = target.GetComponent<CharacterStats>();
            Check(melee.TryAttack(), "unarmed fallback starts"); melee.TickAttack(10); stats.ResetToFull(); targetStats.ResetToFull();
            var greatsword = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/Greatsword.asset");
            Check(inventory.TryAdd(greatsword), "greatsword seed"); long revision = equipment.Revision;
            Check(!equipment.TryEquip(0, EquipmentSlot.MainHand, out var reason) && reason.Contains("Strength") && equipment.Revision == revision, "weapon requirement fails atomically");
            string text = InventoryItemText.Describe(greatsword, 1, equipment, out bool met);
            Check(!met && text.Contains("MISSING") && text.Contains("Base damage"), "UI weapon stats and unmet requirements");
            so = new SerializedObject(attributes); var values = so.FindProperty("baseAttributes");
            for (int i = 0; i < values.arraySize; i++) values.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = 30;
            so.ApplyModifiedPropertiesWithoutUndo(); inventory.Clear();
            foreach (string name in new[] { "IronSword", "Greatsword", "Spear", "Staff" })
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/" + name + ".asset"); var weapon = item.Weapon;
                Check(inventory.TryAdd(item) && equipment.TryEquip(0, EquipmentSlot.MainHand, out reason), name + " equips");
                target.transform.position = actor.transform.position + new Vector3(0, 1, 1.2f); Physics.SyncTransforms();
                Check(melee.TryAttack() && melee.CurrentWeapon == weapon && melee.Definition == weapon.AttackAnimationSet.CombatProfile, name + " selects equipped profile");
                Check(Mathf.Approximately(stats.CurrentStamina, stats.MaxStamina - weapon.StaminaCost), name + " weapon stamina");
                Check(Mathf.Approximately(melee.SwingDuration, melee.Definition.AttackDuration / weapon.AttackSpeed), name + " weapon attack speed");
                float damage = equipment.GetModifiedStat(ItemStat.AttackDamage, weapon.BaseDamage);
                melee.TickAttack(melee.Definition.ActiveStartTime / weapon.AttackSpeed + .01f);
                Check(Mathf.Approximately(targetStats.CurrentHealth, targetStats.MaxHealth - damage), name + " equipped damage plus legacy bonuses");
                melee.TickAttack(10); stats.ResetToFull(); targetStats.ResetToFull();
                Check(melee.TryHeavyAttack(), name + " heavy starts");
                Check(Mathf.Approximately(stats.CurrentStamina, stats.MaxStamina - weapon.StaminaCost * melee.Definition.HeavyStaminaMultiplier), name + " heavy weapon cost");
                Check(Mathf.Approximately(melee.SwingDuration, melee.Definition.AttackDuration * melee.Definition.HeavyTimingMultiplier / weapon.AttackSpeed), name + " heavy weapon speed");
                melee.TickAttack(10);
                Check(Mathf.Approximately(targetStats.CurrentHealth, targetStats.MaxHealth - damage * melee.Definition.HeavyDamageMultiplier), name + " heavy weapon damage");
                stats.ResetToFull(); targetStats.ResetToFull();
                Check(equipment.TryUnequip(EquipmentSlot.MainHand, out reason), name + " unequips"); inventory.Clear();
            }
            var spear = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/Spear.asset");
            inventory.TryAdd(spear); Check(equipment.TryEquip(0, EquipmentSlot.MainHand, out reason), "spear equips for reach");
            target.transform.position = actor.transform.position + new Vector3(0, 1, 2.8f); Physics.SyncTransforms();
            Check(melee.TryAttack(), "spear reach attack"); melee.TickAttack(10);
            Check(targetStats.CurrentHealth < targetStats.MaxHealth, "spear reaches beyond fallback hit box");
            stats.ResetToFull(); targetStats.ResetToFull();
            target.transform.position = actor.transform.position + new Vector3(0, 1, spear.Weapon.Range + 1); Physics.SyncTransforms();
            Check(melee.TryAttack(), "out of range attack"); melee.TickAttack(10);
            Check(targetStats.CurrentHealth == targetStats.MaxHealth, "weapon range rejects distant target"); stats.ResetToFull();
            target.transform.position = actor.transform.position + new Vector3(0, 1, 1.2f); Physics.SyncTransforms();
            Check(melee.TryAttack(), "snapshot swing starts");
            Check(equipment.TryUnequip(EquipmentSlot.MainHand, out reason), "offline equipment mutation during swing fixture");
            Check(melee.CurrentWeapon == spear.Weapon, "swing pins weapon after unequip"); melee.TickAttack(10);
            Check(Mathf.Approximately(targetStats.CurrentHealth, targetStats.MaxHealth - spear.Weapon.BaseDamage), "swing pins weapon damage after unequip");
            stats.ResetToFull(); targetStats.ResetToFull(); inventory.Clear();
            inventory.TryAdd(greatsword); equipment.TryEquip(0, EquipmentSlot.MainHand, out reason);
            so = new SerializedObject(attributes); values = so.FindProperty("baseAttributes");
            values.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = 1; so.ApplyModifiedPropertiesWithoutUndo();
            float before = stats.CurrentStamina;
            Check(!melee.TryAttack() && stats.CurrentStamina == before, "combat rechecks requirements before payment");
            // Nonzero scaling placeholders remain inert; equipped weapons work without a player attack profile.
            var sampleWeapon = UnityEngine.Object.Instantiate(spear.Weapon); temporary.Add(sampleWeapon);
            so = new SerializedObject(sampleWeapon); so.FindProperty("baseDamage").floatValue = 7; so.FindProperty("staminaCost").floatValue = 9;
            so.FindProperty("attackSpeed").floatValue = 2; so.FindProperty("statRequirements").arraySize = 0;
            so.FindProperty("scalingPlaceholders").GetArrayElementAtIndex(0).FindPropertyRelative("coefficient").floatValue = 999;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sampleItem = UnityEngine.Object.Instantiate(spear); temporary.Add(sampleItem);
            so = new SerializedObject(sampleItem); so.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
            so.FindProperty("weapon").objectReferenceValue = sampleWeapon; so.FindProperty("equipmentRequirements").arraySize = 0; so.FindProperty("statModifiers").arraySize = 0; so.ApplyModifiedPropertiesWithoutUndo();
            var sampleCatalog = ScriptableObject.CreateInstance<ItemCatalog>(); temporary.Add(sampleCatalog);
            so = new SerializedObject(sampleCatalog); so.FindProperty("items").arraySize = 1; so.FindProperty("items").GetArrayElementAtIndex(0).objectReferenceValue = sampleItem; so.ApplyModifiedPropertiesWithoutUndo();
            var sampleActor = new GameObject("Weapon-only parameters"); temporary.Add(sampleActor); sampleActor.SetActive(false); sampleActor.transform.position = actor.transform.position;
            var sampleInventory = sampleActor.AddComponent<Inventory>(); so = new SerializedObject(sampleInventory); so.FindProperty("catalog").objectReferenceValue = sampleCatalog; so.ApplyModifiedPropertiesWithoutUndo();
            var sampleEquipment = sampleActor.AddComponent<Equipment>(); var sampleAttack = sampleActor.AddComponent<MeleeAttack>(); sampleActor.SetActive(true);
            Check(sampleInventory.TryAdd(sampleItem) && sampleEquipment.TryEquip(0, EquipmentSlot.MainHand, out reason), "isolated weapon equips");
            Check(sampleAttack.TryAttack() && sampleAttack.Definition != null, "weapon supplies profile without player attack configuration");
            Check(Mathf.Approximately(sampleActor.GetComponent<CharacterStats>().CurrentStamina, 91) && Mathf.Approximately(sampleAttack.SwingDuration, .275f), "weapon overrides legacy stamina and timing");
            Physics.SyncTransforms(); sampleAttack.TickAttack(10);
            Check(Mathf.Approximately(targetStats.CurrentHealth, targetStats.MaxHealth - 7), "weapon base damage overrides legacy profile and ignores scaling placeholder");
            sampleActor.GetComponent<CharacterStats>().ResetToFull();
            so = new SerializedObject(sampleWeapon); so.FindProperty("attackSpeed").floatValue = float.NaN; so.ApplyModifiedPropertiesWithoutUndo();
            Check(!sampleAttack.TryAttack() && sampleActor.GetComponent<CharacterStats>().CurrentStamina == 100, "invalid weapon speed rejected before payment");
            inventory.SetAuthority(false); Check(!equipment.TryUnequip(EquipmentSlot.MainHand, out reason), "replica cannot change weapon");
            actor.AddComponent(typeof(CharacterStats).Assembly.GetType("HeroesOfDragonland.Multiplayer.NetworkCharacterStats"));
            Check(!melee.TryHeavyAttack() && stats.CurrentStamina == before, "non authoritative weapon attack rejected");
            Check(!EditorUtility.scriptCompilationFailed, "project compilation");
            result.Log("PASS weapon combat: " + checks + " checks");
        }
        finally { foreach (var obj in temporary) if (obj != null) UnityEngine.Object.DestroyImmediate(obj); }
    }
}
