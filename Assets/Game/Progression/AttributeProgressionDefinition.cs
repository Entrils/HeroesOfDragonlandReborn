using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Progression
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Attribute Progression")]
    public sealed class AttributeProgressionDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private float vitality = 10;
        [SerializeField, Min(0)] private float endurance = 10;
        [SerializeField, Min(0)] private float mind = 10;
        [SerializeField, Min(0)] private float strength = 10;
        [SerializeField, Min(0)] private float dexterity = 10;
        [SerializeField, Min(0)] private float healthPerVitality = 10;
        [SerializeField, Min(0)] private float staminaPerEndurance = 5;
        [SerializeField, Min(0)] private float manaPerMind = 5;
        private static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
        public float GetBaseValue(ItemStat stat)
        {
            switch (stat)
            {
                case ItemStat.Vitality: return Safe(vitality);
                case ItemStat.Endurance: return Safe(endurance);
                case ItemStat.Mind: return Safe(mind);
                case ItemStat.Strength: return Safe(strength);
                case ItemStat.Dexterity: return Safe(dexterity);
                default: return 0;
            }
        }
        public float GetResourcePerPoint(ItemStat stat)
        {
            switch (stat)
            {
                case ItemStat.Vitality: return Safe(healthPerVitality);
                case ItemStat.Endurance: return Safe(staminaPerEndurance);
                case ItemStat.Mind: return Safe(manaPerMind);
                default: return 0;
            }
        }
    }
}
