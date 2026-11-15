using System;
using System.Collections.Generic;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Art/Equipment Visual Catalog")]
    public sealed class EquipmentVisualCatalog : ScriptableObject
    {
        [Serializable] private sealed class ItemBinding
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField] private EquipmentVisualDefinition visual;
            internal ItemDefinition Item => item;
            internal EquipmentVisualDefinition Visual => visual;
        }
        [Serializable] private sealed class WeaponBinding
        {
            [SerializeField] private WeaponArchetype archetype;
            [SerializeField] private EquipmentVisualDefinition visual;
            internal WeaponArchetype Archetype => archetype;
            internal EquipmentVisualDefinition Visual => visual;
        }
        [SerializeField] private ItemBinding[] items = Array.Empty<ItemBinding>();
        [SerializeField] private WeaponBinding[] weapons = Array.Empty<WeaponBinding>();
        [SerializeField] private EquipmentVisualDefinition[] armorDefaults = Array.Empty<EquipmentVisualDefinition>();

        // Identity comes from the existing public item ID, never private inventory positions.
        public EquipmentVisualDefinition Resolve(ItemDefinition item)
        {
            if (item == null) return null;
            EquipmentVisualDefinition found = null;
            bool matched = false;
            foreach (var binding in items ?? Array.Empty<ItemBinding>())
            {
                if (binding?.Item == null || binding.Item.Id != item.Id) continue;
                if (matched) return null; // Ambiguous overrides fail closed.
                matched = true;
                found = binding.Visual;
            }
            if (matched) return found;
            if (item.EquipmentSlot == EquipmentSlot.MainHand && item.Weapon != null)
            {
                foreach (var binding in weapons ?? Array.Empty<WeaponBinding>())
                {
                    if (binding == null || binding.Archetype != item.Weapon.Archetype) continue;
                    if (matched) return null;
                    matched = true;
                    found = binding.Visual;
                }
            }
            else foreach (var definition in armorDefaults ?? Array.Empty<EquipmentVisualDefinition>())
            {
                if (definition == null || definition.Slot != item.EquipmentSlot) continue;
                if (matched) return null;
                matched = true;
                found = definition;
            }
            return found;
        }

        public bool TryValidate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var archetypes = new HashSet<WeaponArchetype>();
            var slots = new HashSet<EquipmentSlot>();
            reason = null;
            if (items == null || weapons == null || armorDefaults == null) { reason = "Visual catalog arrays must not be null."; return false; }
            foreach (var binding in items)
            {
                if (binding?.Item == null || binding.Visual == null || string.IsNullOrEmpty(binding.Item.Id) || !ids.Add(binding.Item.Id))
                    reason = "Missing or duplicate item visual binding.";
                else if (binding.Visual.Slot != binding.Item.EquipmentSlot) reason = "Item visual slot mismatch.";
                else if (!binding.Visual.TryValidate(out reason)) return false;
                if (reason != null) return false;
            }
            foreach (var binding in weapons)
            {
                if (binding == null || !Enum.IsDefined(typeof(WeaponArchetype), binding.Archetype) || !archetypes.Add(binding.Archetype)
                    || binding.Visual == null || binding.Visual.Slot != EquipmentSlot.MainHand) reason = "Invalid or duplicate weapon visual binding.";
                else if (!binding.Visual.TryValidate(out reason)) return false;
                if (reason != null) return false;
            }
            foreach (var definition in armorDefaults)
            {
                if (definition == null || definition.IsWeapon || !slots.Add(definition.Slot)) reason = "Invalid or duplicate armor default.";
                else if (!definition.TryValidate(out reason)) return false;
                if (reason != null) return false;
            }
            return true;
        }
    }
}
