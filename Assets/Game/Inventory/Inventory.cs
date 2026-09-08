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
        [Tooltip("Local session defaults to authority. A future networking adapter must set replicas to false.")]
        [SerializeField] private bool hasAuthority = true;
        private InventoryState state;
        public bool HasAuthority => hasAuthority;
        public ItemCatalog Catalog => catalog;
        internal bool TryDeposit(InventorySlot[] rewards, Action<InventorySlot[]> commitRemainder)
            => hasAuthority && State.TryDeposit(rewards, commitRemainder);
        public bool TryResolveItem(string id, out ItemDefinition item) => catalog.TryGet(id, out item);
        internal bool TryExchange(int sourceIndex, string returnedId, Action commitEquipment)
            => hasAuthority && State.TryExchange(sourceIndex, returnedId, commitEquipment);
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
        public bool TryAdd(string id, int amount = 1) => hasAuthority && State.TryAdd(id, amount);
        public bool TryAdd(ItemDefinition item, int amount = 1) => item != null && TryAdd(item.Id, amount);
        public bool TryRemove(string id, int amount = 1) => hasAuthority && State.TryRemove(id, amount);
        public bool TryRemove(ItemDefinition item, int amount = 1) => item != null && TryRemove(item.Id, amount);
        public bool Clear() => hasAuthority && State.Clear();
        public InventorySnapshot CaptureSnapshot() => State.CaptureSnapshot();
        public bool TryApplySnapshot(InventorySnapshot snapshot) => !hasAuthority && State.TryApplySnapshot(snapshot);
    }
}
