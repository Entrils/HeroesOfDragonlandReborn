using System;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.InventorySystem
{
    // Per-owner session state; all writes commit atomically, then publish one snapshot.
    public sealed class InventoryState
    {
        public const int MaximumSlots = 256;
        private readonly ItemCatalog catalog;
        private InventorySlot[] slots;
        public int SlotCount => slots.Length;
        public long Revision { get; private set; }
        public event Action<InventorySnapshot> Changed;
        public InventorySlot GetSlot(int index) => slots[index];
        public bool HasEmptySlot => Array.Exists(slots, slot => slot.IsEmpty);
        // Full means no room even in an existing partial stack. Use CanAdd for a specific item.
        public bool IsFull
        {
            get
            {
                foreach (var slot in slots)
                    if (slot.IsEmpty || (Resolve(slot.ItemId, out var item) && slot.Amount < item.MaximumStackSize)) return false;
                return true;
            }
        }

        public InventoryState(ItemCatalog catalog, int slotCount)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!catalog.TryValidate(out string error)) throw new ArgumentException(error, nameof(catalog));
            if (slotCount < 1 || slotCount > MaximumSlots) throw new ArgumentOutOfRangeException(nameof(slotCount));
            this.catalog = catalog;
            slots = new InventorySlot[slotCount];
        }

        private bool Resolve(string id, out ItemDefinition item)
            => catalog.TryGet(id, out item) && item.TryValidate(out _);

        public long GetAmount(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            long total = 0;
            foreach (var slot in slots) if (slot.ItemId == id) total += slot.Amount;
            return total;
        }

        public long GetCapacity(string id)
        {
            if (!Resolve(id, out var item)) return 0;
            long capacity = 0;
            foreach (var slot in slots)
                if (slot.IsEmpty) capacity += item.MaximumStackSize;
                else if (slot.ItemId == id) capacity += item.MaximumStackSize - slot.Amount;
            return capacity;
        }

        public bool CanAdd(string id, int amount = 1) => amount > 0 && GetCapacity(id) >= amount;

        public bool TryAdd(string id, int amount)
        {
            if (!CanAdd(id, amount) || !Resolve(id, out var item)) return false;
            int remaining = amount;
            // Fill existing stacks first, then empty slots, in stable slot order.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < slots.Length && remaining > 0; i++)
                {
                    var slot = slots[i];
                    if (pass == 0 ? slot.IsEmpty || slot.ItemId != id : !slot.IsEmpty) continue;
                    int added = Math.Min(remaining, item.MaximumStackSize - slot.Amount);
                    slots[i] = new InventorySlot(id, slot.Amount + added);
                    remaining -= added;
                }
            Publish(Revision + 1);
            return true;
        }

        public bool TryRemove(string id, int amount)
        {
            if (amount <= 0 || GetAmount(id) < amount) return false;
            int remaining = amount;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].ItemId != id) continue;
                int removed = Math.Min(remaining, slots[i].Amount);
                int left = slots[i].Amount - removed;
                slots[i] = left == 0 ? default : new InventorySlot(id, left);
                remaining -= removed;
            }
            Publish(Revision + 1);
            return true;
        }

        public bool Clear()
        {
            if (Array.TrueForAll(slots, slot => slot.IsEmpty)) return false;
            Array.Clear(slots, 0, slots.Length);
            Publish(Revision + 1);
            return true;
        }

        public bool TryRemoveAt(int index, int amount)
        {
            if (index < 0 || index >= slots.Length || amount <= 0 || slots[index].Amount < amount) return false;
            var old = slots[index];
            slots[index] = old.Amount == amount ? default : new InventorySlot(old.ItemId, old.Amount - amount);
            Publish(Revision + 1);
            return true;
        }

        public InventorySnapshot CaptureSnapshot() => new InventorySnapshot(Revision, slots);

        // Chest reward transfer: preserve overflow, commit both owners before notifying.
        internal bool TryDeposit(InventorySlot[] rewards, Action<InventorySlot[]> commitRemainder)
        {
            var staged = new InventoryState(catalog, SlotCount);
            staged.slots = (InventorySlot[])slots.Clone();
            var remaining = new System.Collections.Generic.List<InventorySlot>();
            bool changed = false;
            foreach (var reward in rewards)
            {
                if (reward.Amount <= 0 || !Resolve(reward.ItemId, out _)) return false;
                int accepted = (int)Math.Min(reward.Amount, staged.GetCapacity(reward.ItemId));
                if (accepted > 0) { staged.TryAdd(reward.ItemId, accepted); changed = true; }
                if (accepted < reward.Amount) remaining.Add(new InventorySlot(reward.ItemId, reward.Amount - accepted));
            }
            if (!changed) return false;
            slots = staged.slots;
            commitRemainder(remaining.ToArray());
            Publish(Revision + 1);
            return true;
        }

        // Stage equipment transfers on a private copy. Commit both owners before events.
        // sourceIndex=-1 means unequip only; returnedId=null means an empty equipment slot.
        internal bool TryExchange(int sourceIndex, string returnedId, Action commitEquipment)
        {
            if (sourceIndex < -1 || sourceIndex >= slots.Length) return false;
            var staged = new InventoryState(catalog, SlotCount);
            staged.slots = (InventorySlot[])slots.Clone();
            if (sourceIndex >= 0)
            {
                var source = staged.slots[sourceIndex];
                if (source.IsEmpty) return false;
                staged.slots[sourceIndex] = source.Amount == 1 ? default : new InventorySlot(source.ItemId, source.Amount - 1);
            }
            if (returnedId != null && !staged.TryAdd(returnedId, 1)) return false;
            slots = staged.slots;
            commitEquipment();
            Publish(Revision + 1);
            return true;
        }

        // Only the future trusted replication adapter should call this on replicas.
        // Validate the whole payload before replacing any state; reject stale updates.
        public bool TryApplySnapshot(InventorySnapshot snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.Revision <= Revision
                || snapshot.Revision == long.MaxValue || snapshot.Slots.Count != SlotCount) return false;
            var replacement = new InventorySlot[SlotCount];
            for (int i = 0; i < replacement.Length; i++)
            {
                var slot = snapshot.Slots[i];
                if (!slot.IsEmpty && (slot.Amount <= 0 || !Resolve(slot.ItemId, out var item)
                    || slot.Amount > item.MaximumStackSize)) return false;
                replacement[i] = slot;
            }
            slots = replacement;
            Publish(snapshot.Revision);
            return true;
        }

        private void Publish(long revision)
        {
            Revision = revision;
            Changed?.Invoke(CaptureSnapshot());
        }
    }
}
