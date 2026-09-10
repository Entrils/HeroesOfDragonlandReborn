using System;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;
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
        // Preserve existing scene overrides and legacy Intelligence/Level data.
        [SerializeField] private AttributeValue[] baseAttributes = {
            new AttributeValue(ItemStat.Strength, 10), new AttributeValue(ItemStat.Dexterity, 10),
            new AttributeValue(ItemStat.Intelligence, 10), new AttributeValue(ItemStat.Level, 1)
        };
        [SerializeField] private AttributeProgressionDefinition definition;
        private SessionProgression progression;
        private Equipment equipment;
        public event Action Changed;

        private void OnEnable()
        {
            progression = GetComponent<SessionProgression>();
            equipment = GetComponent<Equipment>();
            if (progression != null) progression.Changed += NotifyChanged;
            if (equipment != null) equipment.Changed += OnEquipmentChanged;
            NotifyChanged();
        }
        private void OnDisable()
        {
            if (progression != null) progression.Changed -= NotifyChanged;
            if (equipment != null) equipment.Changed -= OnEquipmentChanged;
        }
        private void OnEquipmentChanged(EquipmentSlot slot, string oldId, string newId) => NotifyChanged();
        private void NotifyChanged()
        {
            if (Changed == null) return;
            foreach (Action handler in Changed.GetInvocationList())
                try { handler(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

        public bool TryGetUnallocatedBaseValue(ItemStat stat, out float value)
        {
            value = 0;
            var session = GetComponent<SessionProgression>();
            if (stat == ItemStat.Level && session != null)
            { value = session.Level; return session.IsInitialized; }
            if (SessionProgression.IsProgressionAttribute(stat) && definition != null)
            { value = definition.GetBaseValue(stat); return true; }
            bool found = false;
            if (baseAttributes != null) foreach (var entry in baseAttributes)
            {
                if (entry.Stat != stat) continue;
                if (found || entry.Value < 0 || float.IsNaN(entry.Value) || float.IsInfinity(entry.Value)) return false;
                found = true;
                value = entry.Value;
            }
            // Existing scenes need no serialized migration for the three new attributes.
            if (!found && (stat == ItemStat.Vitality || stat == ItemStat.Endurance || stat == ItemStat.Mind))
            { value = 10; return true; }
            return found;
        }
        public int GetAllocatedPoints(ItemStat stat)
        {
            var session = GetComponent<SessionProgression>();
            return session != null ? session.GetAllocatedPoints(stat) : 0;
        }
        // Requirement value: innate base plus session investment, excluding gear.
        public bool TryGetBaseValue(ItemStat stat, out float value)
        {
            if (!TryGetUnallocatedBaseValue(stat, out value)) return false;
            value += GetAllocatedPoints(stat);
            return true;
        }
        public bool TryGetValue(ItemStat stat, out float value)
        {
            if (!TryGetBaseValue(stat, out value)) return false;
            if (stat == ItemStat.Level) return true;
            var gear = GetComponent<Equipment>();
            if (gear != null) value = gear.GetModifiedStat(stat, value);
            return true;
        }
        public float GetEquipmentModifier(ItemStat stat)
            => TryGetValue(stat, out float final) && TryGetBaseValue(stat, out float innate) ? final - innate : 0;

        public float CalculateResourceMaximum(ItemStat resource, float startingMaximum)
        {
            ItemStat attribute;
            switch (resource)
            {
                case ItemStat.MaxHealth: attribute = ItemStat.Vitality; break;
                case ItemStat.MaxStamina: attribute = ItemStat.Endurance; break;
                case ItemStat.MaxMana: attribute = ItemStat.Mind; break;
                default: return startingMaximum;
            }
            float result = startingMaximum;
            if (TryGetValue(attribute, out float final) && TryGetUnallocatedBaseValue(attribute, out float baseline))
                result += (final - baseline) * (definition != null ? definition.GetResourcePerPoint(attribute)
                    : attribute == ItemStat.Vitality ? 10f : 5f);
            var gear = GetComponent<Equipment>();
            if (gear != null) result = gear.GetModifiedStat(resource, result);
            if (float.IsNaN(result) || float.IsInfinity(result)) return startingMaximum;
            return Mathf.Max(resource == ItemStat.MaxHealth ? 1f : 0f, result);
        }
    }
}
