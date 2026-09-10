using System;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    // Explicit values are part of the save/network contract. Never renumber entries.
    public enum ItemCategory { Weapon = 0, Armor = 1, Consumable = 2, Misc = 3 }
    public enum ItemRarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }
    public enum ItemStat
    {
        MaxHealth = 0, MaxStamina = 1, MaxMana = 2,
        Strength = 3, Dexterity = 4, Intelligence = 5, Armor = 6, AttackDamage = 7, Level = 8,
        Vitality = 9, Endurance = 10, Mind = 11
    }
    public enum StatModifierOperation { Flat = 0, AdditivePercent = 1 }

    [Serializable]
    public struct EquipmentRequirement
    {
        [SerializeField] private ItemStat stat;
        [SerializeField, Min(0f)] private float minimumValue;
        public ItemStat Stat => stat;
        public float MinimumValue => minimumValue;
        public bool IsValid => Enum.IsDefined(typeof(ItemStat), stat)
            && minimumValue >= 0f && !float.IsInfinity(minimumValue);
    }

    [Serializable]
    public struct ItemStatModifier
    {
        [SerializeField] private ItemStat stat;
        [SerializeField] private StatModifierOperation operation;
        [Tooltip("Flat amount, or fractional percent: 0.1 means +10%. Negative values are penalties.")]
        [SerializeField] private float value;
        public ItemStat Stat => stat;
        public StatModifierOperation Operation => operation;
        public float Value => value;
        public bool IsValid => Enum.IsDefined(typeof(ItemStat), stat)
            && Enum.IsDefined(typeof(StatModifierOperation), operation)
            && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
