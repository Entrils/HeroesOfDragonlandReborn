using System;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.InventorySystem
{
    [DisallowMultipleComponent]
    public sealed class Inventory : MonoBehaviour
    {
        [SerializeField] private ItemCatalog catalog;
        [SerializeField, Range(1, InventoryState.MaximumSlots)] private int slotCount = 16;
        [Tooltip("Offline authority only. A network authority component overrides this flag.")]
        [SerializeField] private bool hasAuthority = true;
        private InventoryState state;
        public bool HasAuthority => GetComponent<IInventoryAuthority>()?.CanMutateInventory ?? hasAuthority;
        public ItemCatalog Catalog => catalog;
        internal bool TryDeposit(InventorySlot[] rewards, Action<InventorySlot[]> commitRemainder)
            => HasAuthority && State.TryDeposit(rewards, commitRemainder);
        public bool TryResolveItem(string id, out ItemDefinition item) => catalog.TryGet(id, out item);
        internal bool TryExchange(int sourceIndex, string returnedId, Action commitEquipment)
            => HasAuthority && State.TryExchange(sourceIndex, returnedId, commitEquipment);
        public int SlotCount => State.SlotCount;
        public bool IsFull => State.IsFull;
        public bool HasEmptySlot => State.HasEmptySlot;
        public long Revision => State.Revision;
        public event Action<InventorySnapshot> Changed;

        private InventoryState State
        {
            get
            {
                if (state == null)
                {
                    state = new InventoryState(catalog, slotCount);
                    state.Changed += snapshot => Changed?.Invoke(snapshot);
                }
                return state;
            }
        }

        private void Awake() { _ = State; }
        public void SetAuthority(bool authority) => hasAuthority = authority;
        public InventorySlot GetSlot(int index) => State.GetSlot(index);
        public long GetAmount(string id) => State.GetAmount(id);
        public bool CanAdd(string id, int amount = 1) => State.CanAdd(id, amount);
        public bool TryAdd(string id, int amount = 1) => HasAuthority && State.TryAdd(id, amount);
        public bool TryAdd(ItemDefinition item, int amount = 1) => item != null && TryAdd(item.Id, amount);
        public bool TryRemove(string id, int amount = 1) => HasAuthority && State.TryRemove(id, amount);
        public bool TryRemove(ItemDefinition item, int amount = 1) => item != null && TryRemove(item.Id, amount);
        public bool TryRemoveAt(int index, int amount) => HasAuthority && State.TryRemoveAt(index, amount);
        public bool Clear() => HasAuthority && State.Clear();
        public InventorySnapshot CaptureSnapshot() => State.CaptureSnapshot();
        public bool TryApplySnapshot(InventorySnapshot snapshot)
            => GetComponent<IInventoryAuthority>() == null && !hasAuthority && State.TryApplySnapshot(snapshot);
        internal bool ApplyNetworkSnapshot(InventorySnapshot snapshot)
            => !HasAuthority && State.TryApplySnapshot(snapshot);
    }
}
