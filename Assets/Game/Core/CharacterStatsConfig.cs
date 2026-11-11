using UnityEngine;
using UnityEngine.Serialization;

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

        [FormerlySerializedAs("staggerResistance"), SerializeField, Min(1)] private float poise = 50f;
        [SerializeField, Min(0)] private float poiseRegenDelay = 2f;
        [SerializeField, Min(0)] private float poiseRegenPerSecond = 20f;
        public float Poise => Safe(poise, 1);
        public float PoiseRegenDelay => Safe(poiseRegenDelay, 0);
        public float PoiseRegenPerSecond => Safe(poiseRegenPerSecond, 0);
        public float StaggerResistance => Poise; // Existing content/code compatibility.
        private static float Safe(float value, float minimum) => float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Max(minimum, value);
        public float MaxHealth => maxHealth;
        public float MaxStamina => maxStamina;
        public float StaminaRegenPerSecond => staminaRegenPerSecond;
        public float StaminaRegenDelay => staminaRegenDelay;
        public float MaxMana => maxMana;
        public float ManaRegenPerSecond => manaRegenPerSecond;
        public float ManaRegenDelay => manaRegenDelay;
    }
}
