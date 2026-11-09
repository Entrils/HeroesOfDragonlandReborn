using System;
using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkInventory), typeof(Equipment))]
    public sealed class NetworkEquipment : NetworkBehaviour, IEquipmentChangePolicy
    {
        [Networked, Capacity(6)] public NetworkArray<NetworkString<_32>> EquippedIds => default;
        [Networked] public long SnapshotRevision { get; private set; }
        private Equipment equipment;
        private NetworkInventory inventory;
        private NetworkPlayerCombat combat;
        private NetworkPlayerDodge dodge;
        public bool CanChangeEquipment => isActiveAndEnabled && inventory.CanMutateInventory
            && (combat == null || (!combat.IsBusy && !combat.ActionsBlocked)) && (dodge == null || !dodge.Busy);
        public event Action<bool, string> RequestResolved;

        private void Awake()
        {
            equipment = GetComponent<Equipment>();
            inventory = GetComponent<NetworkInventory>();
            combat = GetComponent<NetworkPlayerCombat>();
            dodge = GetComponent<NetworkPlayerDodge>();
        }
        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            equipment.Changed += Publish;
            Publish(EquipmentSlot.None, null, null);
        }
        private void Publish(EquipmentSlot slot, string previous, string current)
        {
            if (!Runner.IsServer || !HasStateAuthority) return;
            for (int i = 0; i < 6; i++) EquippedIds.Set(i, equipment.GetItemId((EquipmentSlot)(i + 1)) ?? "");
            SnapshotRevision = equipment.Revision + 1;
        }
        public override void Render()
        {
            if (HasStateAuthority || SnapshotRevision <= equipment.Revision) return;
            var ids = new string[6];
            for (int i = 0; i < ids.Length; i++)
            {
                string id = EquippedIds[i].ToString();
                ids[i] = string.IsNullOrEmpty(id) ? null : id;
            }
            equipment.ApplyNetworkSnapshot(SnapshotRevision, ids);
        }
        public bool RequestEquip(int index, EquipmentSlot slot) => Request(index, slot, false);
        public bool RequestUnequip(EquipmentSlot slot) => Request(-1, slot, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Automation can send a stale request through the same validated RPC path.
        internal void RequestStaleForValidation(int index, EquipmentSlot slot, long oldInventory, long oldEquipment)
        {
            if (inventory.CanRequest) RPC_Change(index, (int)slot, false, oldInventory, oldEquipment);
        }
#endif
        private bool Request(int index, EquipmentSlot slot, bool unequip)
        {
            if (!isActiveAndEnabled || !inventory.CanRequest) return false;
            var localInventory = GetComponent<HeroesOfDragonland.InventorySystem.Inventory>();
            RPC_Change(index, (int)slot, unequip,
                HasStateAuthority ? inventory.SnapshotRevision : localInventory.Revision,
                HasStateAuthority ? SnapshotRevision : equipment.Revision);
            return true;
        }
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_Change(int index, int slot, bool unequip, long inventoryRevision, long equipmentRevision, RpcInfo info = default)
        {
            if (!inventory.AdmitRequest(info.Source)) return;
            string reason = "Invalid or stale equipment request";
            bool accepted = false;
            if (CanChangeEquipment && inventoryRevision == inventory.SnapshotRevision && equipmentRevision == SnapshotRevision)
                accepted = unequip ? equipment.TryUnequip((EquipmentSlot)slot, out reason)
                    : equipment.TryEquip(index, (EquipmentSlot)slot, out reason);
            RPC_Result(accepted, accepted ? "Equipment changed" : reason);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_Result(bool accepted, string reason) => RequestResolved?.Invoke(accepted, reason);
        public override void Despawned(NetworkRunner runner, bool hasState) => equipment.Changed -= Publish;
    }
}
