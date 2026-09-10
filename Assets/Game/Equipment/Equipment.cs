using System;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.InventorySystem;
using UnityEngine;

namespace HeroesOfDragonland.EquipmentSystem
{
    [DisallowMultipleComponent, RequireComponent(typeof(Inventory))]
    public sealed class Equipment : MonoBehaviour
    {
        [Tooltip("Optional component implementing IEquipmentRequirementSource. Otherwise discovered on this player.")]
        [SerializeField] private MonoBehaviour requirementSource;
        private Inventory inventory;
        private readonly string[] equippedIds = new string[6];
        private bool transferring;
        public long Revision { get; private set; }
        // Slot, old definition ID, new definition ID; null means empty.
        public event Action<EquipmentSlot, string, string> Changed;
        private Inventory Inventory => inventory != null ? inventory : inventory = GetComponent<Inventory>();

        private static bool ValidSlot(EquipmentSlot slot) => slot >= EquipmentSlot.MainHand && slot <= EquipmentSlot.Legs;
        public string GetItemId(EquipmentSlot slot) => ValidSlot(slot) ? equippedIds[(int)slot - 1] : null;
        public ItemDefinition GetItem(EquipmentSlot slot)
            => Inventory.TryResolveItem(GetItemId(slot), out var item) ? item : null;

        public bool TryEquip(int inventorySlot, EquipmentSlot slot, out string reason)
        {
            if (!CanChange(slot, out reason)) return false;
            if (inventorySlot < 0 || inventorySlot >= Inventory.SlotCount) { reason = "Invalid inventory slot."; return false; }
            var source = Inventory.GetSlot(inventorySlot);
            if (source.IsEmpty || !Inventory.TryResolveItem(source.ItemId, out var item)) { reason = "No valid item in that inventory slot."; return false; }
            if (!item.TryValidate(out reason)) return false;
            if (item.EquipmentSlot != slot) { reason = "Item cannot be equipped in " + slot + "."; return false; }
            if (!MeetsRequirements(item, out reason)) return false;
            return Transfer(inventorySlot, slot, item.Id, out reason);
        }

        public bool TryUnequip(EquipmentSlot slot, out string reason)
        {
            if (!CanChange(slot, out reason)) return false;
            if (GetItemId(slot) == null) { reason = "Equipment slot is empty."; return false; }
            return Transfer(-1, slot, null, out reason);
        }

        private bool CanChange(EquipmentSlot slot, out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) reason = "Equipment is disabled.";
            else if (!Inventory.HasAuthority) reason = "Only the session authority may change equipment.";
            else if (transferring) reason = "An equipment transfer is already in progress.";
            else if (!ValidSlot(slot)) reason = "Invalid equipment slot.";
            return reason == null;
        }

        private bool MeetsRequirements(ItemDefinition item, out string reason)
        {
            var source = requirementSource != null ? requirementSource as IEquipmentRequirementSource
                : GetComponent<IEquipmentRequirementSource>();
            foreach (var requirement in item.EquipmentRequirements)
            {
                if (source == null || !source.TryGetBaseValue(requirement.Stat, out float value)
                    || float.IsNaN(value) || float.IsInfinity(value))
                { reason = "Missing attribute: " + requirement.Stat + "."; return false; }
                if (value < requirement.MinimumValue)
                { reason = "Requires " + requirement.Stat + " " + requirement.MinimumValue + " (base + allocated " + value + ")."; return false; }
            }
            reason = null;
            return true;
        }

        private bool Transfer(int inventorySlot, EquipmentSlot slot, string newId, out string reason)
        {
            string previous = GetItemId(slot);
            transferring = true;
            try
            {
                if (!Inventory.TryExchange(inventorySlot, previous, () => { equippedIds[(int)slot - 1] = newId; Revision++; }))
                { reason = "Inventory has no room for the unequipped item."; return false; }
                Changed?.Invoke(slot, previous, newId);
                reason = null;
                return true;
            }
            finally { transferring = false; }
        }

        // Recomputed from current gear: no accumulating bonuses or asset mutation.
        public float GetModifiedStat(ItemStat stat, float baseValue)
        {
            float flat = 0, percent = 0;
            foreach (string id in equippedIds)
            {
                if (id == null || !Inventory.TryResolveItem(id, out var item)) continue;
                foreach (var modifier in item.StatModifiers)
                {
                    if (modifier.Stat != stat) continue;
                    if (modifier.Operation == StatModifierOperation.Flat) flat += modifier.Value;
                    else if (modifier.Operation == StatModifierOperation.AdditivePercent) percent += modifier.Value;
                }
            }
            return Mathf.Max(0, (baseValue + flat) * (1 + percent));
        }
    }
}
