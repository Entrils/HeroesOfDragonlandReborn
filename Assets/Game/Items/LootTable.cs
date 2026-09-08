using System;
using System.Collections.Generic;
using HeroesOfDragonland.InventorySystem;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Loot Table")]
    public sealed class LootTable : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField, Min(0)] private float weight;
            [SerializeField, Min(1)] private int minimumQuantity;
            [SerializeField, Min(1)] private int maximumQuantity;
            public ItemDefinition Item => item;
            public float Weight => weight;
            public int Minimum => minimumQuantity;
            public int Maximum => maximumQuantity;
        }
        [SerializeField, Range(0, 32)] private int minimumDrops = 2;
        [SerializeField, Range(0, 32)] private int maximumDrops = 4;
        [Tooltip("Common, Uncommon, Rare, Epic, Legendary multipliers. Entry weight is multiplied by its item's rarity weight.")]
        [SerializeField] private float[] rarityWeights = { 1f, 0.7f, 0.4f, 0.15f, 0.05f };
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryValidate(ItemCatalog catalog, out string reason)
        {
            reason = null;
            if (minimumDrops < 0 || maximumDrops < minimumDrops || maximumDrops > 32)
                reason = "Loot drop range must be ordered and between 0 and 32.";
            else if (rarityWeights == null || rarityWeights.Length != 5) reason = "Provide all five rarity weights.";
            if (reason != null) return false;
            foreach (float weight in rarityWeights)
                if (weight < 0 || float.IsNaN(weight) || float.IsInfinity(weight))
                { reason = "Rarity weights must be finite and nonnegative."; return false; }
            double total = 0;
            if (entries == null) { reason = "Missing loot entries."; return false; }
            foreach (var entry in entries)
            {
                if (entry.Item == null || !entry.Item.TryValidate(out _)) reason = "Missing or invalid loot item.";
                else if (entry.Weight < 0 || float.IsNaN(entry.Weight) || float.IsInfinity(entry.Weight)) reason = "Invalid drop weight.";
                else if (entry.Minimum < 1 || entry.Maximum < entry.Minimum || entry.Maximum > 100000) reason = "Quantity range must be ordered and between 1 and 100000.";
                else if (catalog != null && (!catalog.TryGet(entry.Item.Id, out var resolved) || resolved != entry.Item)) reason = "Loot item is absent from the recipient catalog.";
                if (reason != null) return false;
                total += (double)entry.Weight * rarityWeights[(int)entry.Item.Rarity];
            }
            if (maximumDrops > 0 && total <= 0) { reason = "Loot table needs a positive effective weight."; return false; }
            return true;
        }

        // Invoke only on the authority. Inject a seeded RNG for deterministic tests.
        // Independent draws with replacement; rarity comes from ItemDefinition.
        public bool TryRoll(System.Random random, out InventorySlot[] rewards, out string reason)
        {
            rewards = Array.Empty<InventorySlot>();
            if (random == null) { reason = "Missing loot random source."; return false; }
            if (!TryValidate(null, out reason)) return false;
            int count = random.Next(minimumDrops, maximumDrops + 1);
            var rolled = new List<InventorySlot>(count);
            double total = 0;
            foreach (var entry in entries) total += (double)entry.Weight * rarityWeights[(int)entry.Item.Rarity];
            for (int i = 0; i < count; i++)
            {
                double selection = random.NextDouble() * total;
                foreach (var entry in entries)
                {
                    double weight = (double)entry.Weight * rarityWeights[(int)entry.Item.Rarity];
                    if (weight <= 0) continue;
                    selection -= weight;
                    if (selection >= 0) continue;
                    rolled.Add(new InventorySlot(entry.Item.Id, random.Next(entry.Minimum, entry.Maximum + 1)));
                    break;
                }
            }
            rewards = rolled.ToArray();
            return true;
        }
    }
}
