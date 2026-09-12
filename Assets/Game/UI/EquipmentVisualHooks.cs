using System;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Presentation integrations subscribe here and resolve art from ItemDefinition.
    // No private inventory is needed; a late subscriber can query GetItem or Refresh.
    [DisallowMultipleComponent, RequireComponent(typeof(Equipment))]
    public sealed class EquipmentVisualHooks : MonoBehaviour
    {
        private Equipment equipment;
        public event Action<EquipmentSlot, ItemDefinition> VisualChanged;
        private void Awake() => equipment = GetComponent<Equipment>();
        private void OnEnable() { equipment.Changed += OnChanged; Refresh(); }
        private void OnDisable() => equipment.Changed -= OnChanged;
        private void OnChanged(EquipmentSlot slot, string previous, string current)
            => VisualChanged?.Invoke(slot, equipment.GetItem(slot));
        public ItemDefinition GetItem(EquipmentSlot slot) => equipment.GetItem(slot);
        public void Refresh()
        {
            for (int i = 1; i <= 6; i++) VisualChanged?.Invoke((EquipmentSlot)i, equipment.GetItem((EquipmentSlot)i));
        }
    }
}
