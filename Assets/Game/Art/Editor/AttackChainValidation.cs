using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class AttackChainValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException("Attack chains: " + label); }
        public static void ValidateAssets()
        {
            WeaponMovesetValidation.ValidateAssets();
            foreach (WeaponArchetype kind in Enum.GetValues(typeof(WeaponArchetype)))
            {
                var m = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + kind + ".asset").Moveset;
                Check(m.Combo != null && m.Combo.TryValidate(m.LightAttackCount, out _), kind + " combo references/rules");
                Check(m.Combo.GetRule(false, m.LightAttackCount - 1).NextLightIndex == -1, kind + " terminal light");
            }
            var presses = new AttackPressSequence();
            Check(!presses.TryRead(0, 0, out _), "baseline consumes stale input");
            Check(presses.TryRead(1, 0, out var kindInput) && kindInput == AttackInputKind.Light, "fresh press");
            Check(!presses.TryRead(1, 0, out _) && !presses.TryRead(0, 0, out _), "held/replayed/missing input cannot repeat");
            Check(presses.TryRead(100, 1, out kindInput) && kindInput == AttackInputKind.Heavy
                && !presses.TryRead(100, 1, out _), "counter jump creates one intention with heavy priority");
            presses = new AttackPressSequence(); presses.TryRead(uint.MaxValue, 0, out _);
            Check(presses.TryRead(0, 0, out _), "counter wrap");
            Debug.Log("[AttackChainValidation] PASS four finite combo assets and press deduplication/replay/wrap.");
        }
        public static void Start()
        {
            Check(Application.isPlaying, "Play Mode required"); ValidateAssets(); Status = "RUNNING";
            UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>().StartCoroutine(Run());
        }
        private static IEnumerator Run()
        {
            var temporary = new List<UnityEngine.Object>();
            var actor = new GameObject("M43 isolated chain fixture"); actor.SetActive(false); temporary.Add(actor);
            var inventory = actor.AddComponent<Inventory>(); var equipment = actor.AddComponent<Equipment>();
            var melee = actor.AddComponent<MeleeAttack>(); var stats = actor.GetComponent<CharacterStats>();
            var defense = actor.AddComponent<CombatDefense>(); actor.transform.position = new Vector3(0, 1, 100);
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>(); temporary.Add(catalog);
            var items = new List<ItemDefinition>();
            try
            {
                foreach (WeaponArchetype kind in Enum.GetValues(typeof(WeaponArchetype)))
                {
                    var source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Data/Weapons/" + kind + ".asset");
                    var weapon = UnityEngine.Object.Instantiate(source); temporary.Add(weapon);
                    var moveset = UnityEngine.Object.Instantiate(source.Moveset); temporary.Add(moveset);
                    var combo = UnityEngine.Object.Instantiate(source.Moveset.Combo); temporary.Add(combo);
                    SetRef(moveset, "combo", combo); SetRef(weapon, "moveset", moveset);
                    var w = new SerializedObject(weapon); w.FindProperty("statRequirements").arraySize = 0; w.ApplyModifiedPropertiesWithoutUndo();
                    var item = ScriptableObject.CreateInstance<ItemDefinition>(); temporary.Add(item);
                    var d = new SerializedObject(item); d.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
                    d.FindProperty("displayName").stringValue = kind.ToString(); d.FindProperty("category").enumValueIndex = (int)ItemCategory.Weapon;
                    d.FindProperty("equipmentSlot").enumValueIndex = (int)EquipmentSlot.MainHand; d.FindProperty("weapon").objectReferenceValue = weapon;
                    d.ApplyModifiedPropertiesWithoutUndo(); items.Add(item);
                }
                var data = new SerializedObject(catalog); var list = data.FindProperty("items"); list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                data.ApplyModifiedPropertiesWithoutUndo(); SetRef(inventory, "catalog", catalog);
                actor.SetActive(true); foreach (var item in items) Check(inventory.TryAdd(item), "fixture inventory");
                int starts = 0; melee.AttackStarted += () => starts++;
                for (int kind = 0; kind < items.Count; kind++)
                {
                    Reset(melee, stats, defense); Check(equipment.TryEquip(kind, EquipmentSlot.MainHand, out _), "equip fixture");
                    var moveset = equipment.EquippedWeapon.Moveset; var combo = moveset.Combo;
                    Check(melee.TryAttack(), "light1 accepted");
                    int began = starts;
                    Check(!melee.TryAttack() && !melee.TryHeavyAttack() && !melee.CanDodgeCancel, "windup rejects presses/cancel");
                    float expected = stats.MaxStamina - moveset.GetMove(false, 0).StaminaCost;
                    for (int index = 0; index < moveset.LightAttackCount - 1; index++)
                    {
                        Window(melee, combo.GetRule(false, index));
                        Check(melee.TryAttack() && melee.HasBufferedAttack, "one next light buffered");
                        Check(!melee.TryAttack() && !melee.TryHeavyAttack(), "buffer cannot be overwritten or spam refreshed");
                        Check(Mathf.Approximately(stats.CurrentStamina, expected), "queue does not spend stamina");
                        float threshold = melee.SwingDuration + melee.RecoveryDuration * combo.GetRule(false, index).MinimumRecovery;
                        melee.TickAttack(Mathf.Max(.0001f, threshold - melee.AttackElapsed - .001f));
                        Check(starts == began + index, "mandatory recovery cannot be skipped");
                        // Give the next move its cost, without changing production resource logic.
                        stats.ResetToFull(); melee.TickAttack(.002f);
                        expected = stats.MaxStamina - moveset.GetMove(false, index + 1).StaminaCost;
                        Check(melee.MoveIndex == index + 1 && starts == began + index + 1
                            && Mathf.Approximately(stats.CurrentStamina, expected), "transition order and one server payment");
                    }
                    Window(melee, combo.GetRule(false, moveset.LightAttackCount - 1));
                    Check(!melee.TryAttack(), "finisher cannot wrap");
                    melee.TickAttack(melee.SwingDuration + melee.RecoveryDuration);
                    Check(!melee.IsBusy && !melee.TryAttack() && melee.RestartRemaining > 0, "mandatory restart lockout");
                    int finished = starts; melee.TickAttack(5);
                    Check(starts == finished, "no automatic/held restart"); stats.ResetToFull();
                    Check(melee.TryAttack() && melee.MoveIndex == 0, "fresh chain starts at light1");
                    Window(melee, combo.GetRule(false, 0));
                    bool heavyAllowed = combo.GetRule(false, 0).AllowHeavy;
                    Check(melee.TryHeavyAttack() == heavyAllowed, "optional light-heavy rule");
                    stats.ResetToFull(); melee.TickAttack(.4f);
                    if (heavyAllowed) Check(melee.IsHeavy && starts == finished + 2, "buffered heavy accepted");
                    Reset(melee, stats, defense);
                }
                int swordSlot = Enumerable.Range(0, inventory.SlotCount).First(i => inventory.GetSlot(i).ItemId == items[0].Id);
                Check(equipment.TryEquip(swordSlot, EquipmentSlot.MainHand, out _), "reequip sword");
                var sword = equipment.EquippedWeapon.Moveset; var rule = sword.Combo.GetRule(false, 0);
                // Cost is checked at consumption, not at queue time; recovery cannot auto-retry a rejected move.
                Check(melee.TryAttack(), "cost fixture"); Window(melee, rule); Check(melee.TryAttack(), "cost fixture buffer");
                stats.TrySpendStamina(stats.CurrentStamina); int beforeReject = starts; melee.TickAttack(.4f);
                Check(starts == beforeReject && !melee.HasBufferedAttack && stats.CurrentStamina == 0, "insufficient stamina drops queued move");
                stats.ResetToFull(); melee.TickAttack(2); Check(starts == beforeReject, "no delayed retry after replenishment");
                Reset(melee, stats, defense);
                var config = new SerializedObject(sword.Combo); config.FindProperty("bufferLifetime").floatValue = .01f; config.ApplyModifiedPropertiesWithoutUndo();
                Check(melee.TryAttack(), "expiry fixture"); Window(melee, rule); Check(melee.TryAttack(), "expiry queue");
                int beforeExpiry = starts; melee.TickAttack(.02f); Check(!melee.HasBufferedAttack, "buffer expires in simulation time");
                melee.TickAttack(2); Check(starts == beforeExpiry, "expired input cannot chain");
                config.FindProperty("bufferLifetime").floatValue = .6f; config.ApplyModifiedPropertiesWithoutUndo(); Reset(melee, stats, defense);
                Check(melee.TryAttack(), "stagger fixture"); Window(melee, rule); Check(melee.TryAttack(), "stagger queue");
                defense.ApplyStagger(10000); Check(!melee.IsBusy && !melee.HasBufferedAttack, "forced stagger clears action and buffer");
                Reset(melee, stats, defense);
                Check(melee.TryAttack(), "death fixture"); Window(melee, rule); Check(melee.TryAttack(), "death queue");
                stats.ApplyDamage(10000); Check(!melee.IsBusy && !melee.HasBufferedAttack, "death immediately clears buffer");
                Reset(melee, stats, defense);
                Check(melee.TryAttack(), "dodge timing fixture");
                melee.TickAttack(melee.SwingDuration + melee.RecoveryDuration * rule.DodgeCancelRecovery - .001f);
                Check(!melee.CanDodgeCancel, "early dodge cancel denied"); melee.TickAttack(.002f);
                Check(melee.CanDodgeCancel, "explicit late recovery dodge cancel allowed");
                Reset(melee, stats, defense);
                Check(melee.TryAttack(), "equipment fixture"); Window(melee, rule); Check(melee.TryAttack(), "equipment queue");
                Check(equipment.TryUnequip(EquipmentSlot.MainHand, out _) && !melee.HasBufferedAttack, "equipment change invalidates queued links");
                melee.TickAttack(2);
                // An intentionally cyclic authored graph is still bounded.
                var state = new AttackChainState();
                config.FindProperty("maximumChainAttacks").intValue = 1; config.ApplyModifiedPropertiesWithoutUndo();
                state.Begin(sword.Combo, false, 0, false, true);
                Check(!state.TryQueue(AttackInputKind.Light, Vector3.forward, .5f, 1), "chain hard cap");
                yield return null;
                Status = "PASS all four finite light chains, optional heavy, buffer boundaries/expiry/dedup, recovery, costs, restart cap and interruption/cancel rules.";
                Debug.Log("[AttackChainValidation] " + Status);
            }
            finally
            {
                foreach (var obj in temporary.AsEnumerable().Reverse()) if (obj != null) UnityEngine.Object.Destroy(obj);
                if (Status == "RUNNING") Status = "FAILED; see Console";
            }
        }
        private static void SetRef(UnityEngine.Object obj, string name, UnityEngine.Object value)
        { var data = new SerializedObject(obj); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Reset(MeleeAttack melee, CharacterStats stats, CombatDefense defense)
        {
            melee.enabled = false; melee.enabled = true; defense.enabled = false; defense.enabled = true;
            stats.ResetToFull(); melee.TickAttack(melee.RestartRemaining + .01f);
        }
        private static void Window(MeleeAttack melee, AttackChainRule rule)
        {
            float time = (melee.SwingDuration + melee.RecoveryDuration) * (rule.BufferStart + .01f);
            if (time > melee.AttackElapsed) melee.TickAttack(time - melee.AttackElapsed);
        }
    }
}
