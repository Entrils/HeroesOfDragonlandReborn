using UnityEngine;

namespace HeroesOfDragonland.Stats
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Character Stats")]
    public sealed class CharacterStatsConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float staminaRegenPerSecond = 20f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 1f;
        [SerializeField, Min(0f)] private float maxMana = 100f;
        [SerializeField, Min(0f)] private float manaRegenPerSecond = 5f;
        [SerializeField, Min(0f)] private float manaRegenDelay = 2f;

        [SerializeField, Min(1)] private float staggerResistance = 50f;
        public float StaggerResistance => Mathf.Max(1, staggerResistance);
        public float MaxHealth => maxHealth;
        public float MaxStamina => maxStamina;
        public float StaminaRegenPerSecond => staminaRegenPerSecond;
        public float StaminaRegenDelay => staminaRegenDelay;
        public float MaxMana => maxMana;
        public float ManaRegenPerSecond => manaRegenPerSecond;
        public float ManaRegenDelay => manaRegenDelay;
    }
}
