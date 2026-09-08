using System;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.EquipmentSystem
{
    [DisallowMultipleComponent]
    public sealed class CharacterAttributes : MonoBehaviour, IEquipmentRequirementSource
    {
        [Serializable]
        private struct AttributeValue
        {
            [SerializeField] private ItemStat stat;
            [SerializeField, Min(0f)] private float value;
            internal ItemStat Stat => stat;
            internal float Value => value;
            internal AttributeValue(ItemStat stat, float value) { this.stat = stat; this.value = value; }
        }
        [SerializeField] private AttributeValue[] baseAttributes = {
            new AttributeValue(ItemStat.Strength, 10), new AttributeValue(ItemStat.Dexterity, 10),
            new AttributeValue(ItemStat.Intelligence, 10), new AttributeValue(ItemStat.Level, 1)
        };

        public bool TryGetBaseValue(ItemStat stat, out float value)
        {
            value = 0;
            if (stat == ItemStat.Level && TryGetComponent<HeroesOfDragonland.Progression.SessionProgression>(out var progression))
            {
                value = progression.Level;
                return progression.IsInitialized;
            }
            bool found = false;
            if (baseAttributes == null) return false;
            foreach (var entry in baseAttributes)
            {
                if (entry.Stat != stat) continue;
                if (found || entry.Value < 0 || float.IsNaN(entry.Value) || float.IsInfinity(entry.Value)) return false;
                found = true;
                value = entry.Value;
            }
            return found;
        }

        public bool TryGetValue(ItemStat stat, out float value)
        {
            if (!TryGetBaseValue(stat, out value)) return false;
            if (stat == ItemStat.Level && GetComponent<HeroesOfDragonland.Progression.SessionProgression>() != null) return true;
            var equipment = GetComponent<Equipment>();
            if (equipment != null) value = equipment.GetModifiedStat(stat, value);
            return true;
        }
    }
}
