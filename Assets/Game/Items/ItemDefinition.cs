using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable identity assigned from the asset GUID by the editor importer. Preserve the .meta file.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemCategory category;
        [SerializeField] private ItemRarity rarity;
        [SerializeField] private EquipmentSlot equipmentSlot;
        [SerializeField, Min(1)] private int maximumStackSize = 1;
        [SerializeField, Min(0)] private int value;
        [SerializeField] private EquipmentRequirement[] equipmentRequirements = Array.Empty<EquipmentRequirement>();
        [SerializeField] private ItemStatModifier[] statModifiers = Array.Empty<ItemStatModifier>();

        [SerializeField] private WeaponDefinition weapon;
        public WeaponDefinition Weapon => weapon;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public ItemRarity Rarity => rarity;
        public EquipmentSlot EquipmentSlot => equipmentSlot;
        public int MaximumStackSize => maximumStackSize;
        public int Value => value;
        public IReadOnlyList<EquipmentRequirement> EquipmentRequirements
        {
            get
            {
                var merged = new List<EquipmentRequirement>(equipmentRequirements ?? Array.Empty<EquipmentRequirement>());
                if (weapon != null) foreach (var requirement in weapon.StatRequirements)
                {
                    int index = merged.FindIndex(value => value.Stat == requirement.Stat);
                    if (index < 0) merged.Add(requirement);
                    else if (requirement.MinimumValue > merged[index].MinimumValue) merged[index] = requirement;
                }
                return merged.AsReadOnly();
            }
        }
        public IReadOnlyList<ItemStatModifier> StatModifiers => Array.AsReadOnly(statModifiers ?? Array.Empty<ItemStatModifier>());

        public bool TryValidate(out string error)
        {
            if (!Guid.TryParseExact(id, "N", out _)) error = "ID must be a 32-character GUID.";
            else if (string.IsNullOrWhiteSpace(displayName)) error = "Display name is required.";
            else if (!Enum.IsDefined(typeof(ItemCategory), category)) error = "Unknown category.";
            else if (!Enum.IsDefined(typeof(ItemRarity), rarity)) error = "Unknown rarity.";
            else if (!Enum.IsDefined(typeof(EquipmentSlot), equipmentSlot)) error = "Unknown equipment slot.";
            else if (equipmentSlot != EquipmentSlot.None && category != ItemCategory.Weapon && category != ItemCategory.Armor)
                error = "Only weapons and armor can have equipment slots.";
            else if (category == ItemCategory.Weapon && (weapon == null || !weapon.TryValidate(out _)))
                error = "Weapon item requires a valid WeaponDefinition.";
            else if (category != ItemCategory.Weapon && weapon != null) error = "Only weapon items may reference a weapon definition.";
            else if (maximumStackSize < 1 || value < 0) error = "Stack size must be positive and value nonnegative.";
            else
            {
                foreach (var requirement in equipmentRequirements ?? Array.Empty<EquipmentRequirement>())
                    if (!requirement.IsValid) { error = "Invalid equipment requirement."; return false; }
                foreach (var modifier in StatModifiers)
                    if (!modifier.IsValid) { error = "Invalid stat modifier."; return false; }
                error = null;
                return true;
            }
            return false;
        }
    }
}
