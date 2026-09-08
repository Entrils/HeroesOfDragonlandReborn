using UnityEngine;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.EquipmentSystem
{
    [DisallowMultipleComponent, RequireComponent(typeof(Equipment))]
    public sealed class EquipmentDebug : MonoBehaviour
    {
        [SerializeField, Min(0)] private int inventorySlot;
        [SerializeField] private EquipmentSlot equipmentSlot = EquipmentSlot.MainHand;
        private bool CanRun => Application.isPlaying && (Application.isEditor || Debug.isDebugBuild);
        [ContextMenu("Equipment/Equip Inventory Slot")]
        private void Equip()
        {
            if (!CanRun) return;
            bool success = GetComponent<Equipment>().TryEquip(inventorySlot, equipmentSlot, out string reason);
            Debug.Log(success ? "Equipped item." : reason, this);
        }
        [ContextMenu("Equipment/Unequip Slot")]
        private void Unequip()
        {
            if (!CanRun) return;
            bool success = GetComponent<Equipment>().TryUnequip(equipmentSlot, out string reason);
            Debug.Log(success ? "Returned equipment to inventory." : reason, this);
        }
    }
}
