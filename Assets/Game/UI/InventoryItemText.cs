using System.Text;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.UI
{
    public static class InventoryItemText
    {
        public static string Describe(ItemDefinition item, int quantity, Equipment equipment, out bool requirementsMet)
        {
            requirementsMet = true;
            if (item == null) return "Empty slot";
            var text = new StringBuilder();
            text.AppendLine(item.DisplayName).AppendLine(item.Rarity + " | " + item.Category)
                .AppendLine("Quantity: " + quantity).AppendLine(item.Description);
            if (item.EquipmentSlot != EquipmentSlot.None) text.AppendLine("Slot: " + item.EquipmentSlot);
            text.AppendLine().AppendLine("Requirements (base + allocated):");
            if (item.EquipmentRequirements.Count == 0) text.AppendLine("None");
            foreach (var requirement in item.EquipmentRequirements)
            {
                float current = 0;
                bool known = equipment != null && equipment.TryGetRequirementValue(requirement.Stat, out current);
                bool met = known && current >= requirement.MinimumValue;
                requirementsMet &= met;
                text.Append(met ? "OK  " : "MISSING  ").Append(requirement.Stat).Append(": ")
                    .Append(known ? current.ToString("0.##") : "unknown").Append(" / ").Append(requirement.MinimumValue.ToString("0.##"));
                if (!met && known) text.Append(" (need +").Append((requirement.MinimumValue - current).ToString("0.##")).Append(')');
                text.AppendLine();
            }
            if (item.StatModifiers.Count > 0)
            {
                text.AppendLine().AppendLine("Effects when equipped:");
                foreach (var modifier in item.StatModifiers)
                {
                    float value = modifier.Operation == StatModifierOperation.AdditivePercent ? modifier.Value * 100 : modifier.Value;
                    text.Append(modifier.Stat).Append(' ').Append(value >= 0 ? "+" : "").Append(value.ToString("0.##"))
                        .AppendLine(modifier.Operation == StatModifierOperation.AdditivePercent ? "%" : "");
                }
            }
            if (item.Category == ItemCategory.Consumable) text.AppendLine().AppendLine("Use unavailable: consumable effects are not implemented yet.");
            return text.ToString();
        }
    }
}
