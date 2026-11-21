using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [Tooltip("Shared cooldown in seconds between different potion effects. Zero disables it.")]
        [SerializeField, Range(0, 300)] private float globalPotionCooldown = 0.75f;
        public float GlobalPotionCooldown => globalPotionCooldown;
        public IReadOnlyList<ItemDefinition> Items => Array.AsReadOnly(items ?? Array.Empty<ItemDefinition>());

        // Resolve an ID received from a save/network payload against local content.
        // Unknown or ambiguous IDs fail closed; array order is never identity.
        public bool TryGet(string id, out ItemDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            foreach (var item in Items)
            {
                if (item == null || !string.Equals(item.Id, id, StringComparison.Ordinal)) continue;
                if (definition != null) { definition = null; return false; }
                definition = item;
            }
            return definition != null;
        }

        public bool TryValidate(out string error)
        {
            if (!(globalPotionCooldown >= 0 && globalPotionCooldown <= 300))
            { error = "Global potion cooldown must be between 0 and 300 seconds."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Items)
            {
                if (item == null) { error = "Catalog contains a missing item."; return false; }
                if (!item.TryValidate(out error)) { error = item.name + ": " + error; return false; }
                if (!ids.Add(item.Id)) { error = "Duplicate item ID: " + item.Id; return false; }
            }
            error = null;
            return true;
        }
    }
}
