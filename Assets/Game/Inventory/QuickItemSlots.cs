using System;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.InventorySystem
{
    public readonly struct QuickItemSelection
    {
        public int Index { get; }
        public int SlotCount { get; }
        public ItemDefinition Item { get; }
        public long Quantity { get; }

        internal QuickItemSelection(int index, int slotCount, ItemDefinition item, long quantity)
        {
            Index = index;
            SlotCount = slotCount;
            Item = item;
            Quantity = quantity;
        }
    }

    // Local hotbar state. Item IDs survive bag rearrangement, and the bag owns every quantity.
    public sealed class QuickItemSlots : IDisposable
    {
        public const int MaximumSlots = 12;
        private readonly Inventory inventory;
        private readonly string[] itemIds;
        private int selectedIndex;
        public int Count => itemIds.Length;
        public QuickItemSelection Selected { get; private set; }
        public event Action<QuickItemSelection> SelectionChanged;

        public QuickItemSlots(Inventory inventory, int count)
        {
            if (count < 1 || count > MaximumSlots) throw new ArgumentOutOfRangeException(nameof(count));
            this.inventory = inventory != null ? inventory : throw new ArgumentNullException(nameof(inventory));
            itemIds = new string[count];
            inventory.Changed += OnInventoryChanged;
            Refresh();
        }

        public bool Assign(int quickIndex, int inventoryIndex)
        {
            if (!IsQuickIndex(quickIndex) || inventoryIndex < 0 || inventoryIndex >= inventory.SlotCount)
                return false;
            string id = inventory.GetSlot(inventoryIndex).ItemId;
            if (!inventory.TryResolveItem(id, out var item) || item.Category != ItemCategory.Consumable
                || item.Consumable is not { IsValid: true }) return false;
            itemIds[quickIndex] = id;
            Refresh();
            return true;
        }

        public bool Clear(int index)
        {
            if (!IsQuickIndex(index) || itemIds[index] == null) return false;
            itemIds[index] = null;
            Refresh();
            return true;
        }

        public void SelectNext() => Select((selectedIndex + 1) % Count);
        public void SelectPrevious() => Select((selectedIndex + Count - 1) % Count);

        public bool Select(int index)
        {
            if (!IsQuickIndex(index)) return false;
            selectedIndex = index;
            Refresh();
            return true;
        }

        internal bool TryFindSelectedInventoryIndex(out int index)
        {
            string id = itemIds[selectedIndex];
            if (id != null)
                for (int i = 0; i < inventory.SlotCount; i++)
                    if (inventory.GetSlot(i).ItemId == id)
                    { index = i; return true; }
            index = -1;
            return false;
        }

        private bool IsQuickIndex(int index) => index >= 0 && index < Count;

        private void OnInventoryChanged(InventorySnapshot _)
        {
            for (int i = 0; i < Count; i++)
                if (itemIds[i] != null && inventory.GetAmount(itemIds[i]) == 0) itemIds[i] = null;
            Refresh();
        }

        private void Refresh()
        {
            string id = itemIds[selectedIndex];
            inventory.TryResolveItem(id, out var item);
            var next = new QuickItemSelection(selectedIndex, Count, item, id == null ? 0 : inventory.GetAmount(id));
            bool changed = Selected.Index != next.Index || Selected.Item != next.Item
                || Selected.Quantity != next.Quantity || Selected.SlotCount != next.SlotCount;
            Selected = next;
            if (changed) SelectionChanged?.Invoke(next);
        }

        public void Dispose() => inventory.Changed -= OnInventoryChanged;
    }
}
