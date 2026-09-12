using System;
using Fusion;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public struct NetworkInventorySlot : INetworkStruct
    {
        public NetworkString<_32> ItemId;
        public int Amount;
    }

    [DisallowMultipleComponent, RequireComponent(typeof(Inventory), typeof(NetworkCharacterStats))]
    public sealed class NetworkInventory : NetworkBehaviour, IInventoryAuthority
    {
        public const int Capacity = 32;
        [Networked, Capacity(Capacity)] public NetworkArray<NetworkInventorySlot> Slots => default;
        // Revision + 1 gives the initial empty inventory a nonzero snapshot revision.
        [Networked] public long SnapshotRevision { get; private set; }
        [Networked] public int SlotCount { get; private set; }
        private Inventory inventory;
        private CharacterStats stats;
        private int nextRequestTick;
        public bool IsAdmitted => Object != null && Object.IsValid && Runner.IsServer && HasStateAuthority
            && Runner.TryGetPlayerObject(Object.InputAuthority, out var avatar) && avatar == Object;
        public bool CanMutateInventory => isActiveAndEnabled && IsAdmitted && !stats.IsDead;
        public bool CanRequest => isActiveAndEnabled && Object != null && Object.IsValid && HasInputAuthority;
        public event Action<bool, string> RemoveResolved;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            stats = GetComponent<CharacterStats>();
        }
        public override void Spawned()
        {
            if (inventory.SlotCount > Capacity) throw new InvalidOperationException("Network inventory exceeds 32 slots.");
            if (HasStateAuthority)
            {
                // Filter this entire behaviour, including quantities and revision, before publishing.
                ReplicateToAll(false);
                ReplicateTo(Object.InputAuthority, true);
                inventory.Changed += Publish;
                Publish(inventory.CaptureSnapshot());
            }
        }
        private void Publish(InventorySnapshot snapshot)
        {
            if (!Runner.IsServer || !HasStateAuthority) return;
            SlotCount = snapshot.Slots.Count;
            for (int i = 0; i < SlotCount; i++)
                Slots.Set(i, new NetworkInventorySlot { ItemId = snapshot.Slots[i].ItemId ?? "", Amount = snapshot.Slots[i].Amount });
            SnapshotRevision = snapshot.Revision + 1;
        }
        public override void Render()
        {
            if (HasStateAuthority || !HasInputAuthority || SnapshotRevision <= inventory.Revision || SlotCount != inventory.SlotCount) return;
            var slots = new InventorySlot[SlotCount];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Slots[i].Amount == 0 ? default : new InventorySlot(Slots[i].ItemId.ToString(), Slots[i].Amount);
            inventory.ApplyNetworkSnapshot(new InventorySnapshot(SnapshotRevision, slots));
        }
        // Trusted server game systems (loot/rewards) call these. Deliberately no Add RPC.
        public bool TryAddItem(string id, int amount = 1) => CanMutateInventory && inventory.TryAdd(id, amount);
        public bool TryRemoveItem(string id, int amount = 1) => CanMutateInventory && inventory.TryRemove(id, amount);

        // Shared per-avatar request budget for inventory and equipment RPCs.
        internal bool AdmitRequest(PlayerRef sender)
        {
            if (!IsAdmitted || sender != Object.InputAuthority || Runner.Tick.Raw < nextRequestTick) return false;
            nextRequestTick = Runner.Tick.Raw + Mathf.Max(1, Mathf.CeilToInt(0.1f / Runner.DeltaTime));
            return true;
        }
        public bool RequestRemove(int index, int amount)
        {
            if (!CanRequest) return false;
            RPC_Remove(index, amount, HasStateAuthority ? SnapshotRevision : inventory.Revision);
            return true;
        }
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_Remove(int index, int amount, long expectedRevision, RpcInfo info = default)
        {
            if (!AdmitRequest(info.Source)) return;
            bool accepted = CanMutateInventory && expectedRevision == SnapshotRevision && inventory.TryRemoveAt(index, amount);
            RPC_RemoveResult(accepted, accepted ? "Removed" : "Invalid or stale remove request");
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_RemoveResult(bool accepted, string reason) => RemoveResolved?.Invoke(accepted, reason);
        public override void Despawned(NetworkRunner runner, bool hasState) => inventory.Changed -= Publish;
    }
}
