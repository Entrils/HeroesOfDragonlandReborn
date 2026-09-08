using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.InventorySystem
{
    [Serializable]
    public struct InventorySlot
    {
        [SerializeField] private string itemId;
        [SerializeField] private int amount;
        public string ItemId => itemId;
        public int Amount => amount;
        public bool IsEmpty => amount == 0 && string.IsNullOrEmpty(itemId);
        public InventorySlot(string itemId, int amount) { this.itemId = itemId; this.amount = amount; }
    }

    // Transport-neutral value data. No Unity object references or player-global state.
    [Serializable]
    public sealed class InventorySnapshot
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private long revision;
        [SerializeField] private InventorySlot[] slots;
        public int SchemaVersion => schemaVersion;
        public long Revision => revision;
        public IReadOnlyList<InventorySlot> Slots => Array.AsReadOnly(slots ?? Array.Empty<InventorySlot>());
        public InventorySnapshot(long revision, InventorySlot[] slots)
        {
            this.revision = revision;
            this.slots = (InventorySlot[])slots.Clone();
        }
    }
}
