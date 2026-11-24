using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Cosmetics
{
    [Serializable]
    public sealed class CosmeticCollectionData
    {
        [SerializeField] internal List<string> ownedIds = new();
        [SerializeField] internal List<string> equippedIds = new();
    }

    public sealed class CosmeticCollection
    {
        private readonly CosmeticCollectionData data;
        private readonly Action save;

        internal CosmeticCollection(CosmeticCollectionData data, Action save)
        {
            this.data = data;
            this.save = save;
            data.ownedIds ??= new List<string>();
            data.equippedIds ??= new List<string>();
        }

        public bool Owns(string id) => CosmeticCatalog.Find(id) is { } definition
            && (definition.StartsUnlocked || data.ownedIds.Contains(definition.Id));

        public CosmeticDefinition Equipped(CosmeticCategory category)
        {
            foreach (var id in data.equippedIds)
                if (CosmeticCatalog.Find(id) is { } definition && definition.Category == category && Owns(id))
                    return definition;
            return null;
        }

        // Entry point for trusted rewards or future account imports. No economy or inventory dependency.
        public bool Grant(string id)
        {
            if (CosmeticCatalog.Find(id) == null || Owns(id)) return false;
            data.ownedIds.Add(id);
            save();
            return true;
        }

        public bool Equip(string id)
        {
            var cosmetic = CosmeticCatalog.Find(id);
            if (cosmetic == null || !Owns(id)) return false;
            if (Equipped(cosmetic.Category)?.Id == id) return true;
            ClearSlot(cosmetic.Category);
            data.equippedIds.Add(id);
            save();
            return true;
        }

        public void Unequip(CosmeticCategory category)
        {
            if (ClearSlot(category)) save();
        }

        private bool ClearSlot(CosmeticCategory category) =>
            data.equippedIds.RemoveAll(id => CosmeticCatalog.Find(id)?.Category == category) != 0;

        public CosmeticVisualLoadout VisualLoadout => CosmeticVisualLoadout.From(this);
    }
}
