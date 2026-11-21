using UnityEngine;

namespace HeroesOfDragonland.Items
{
    public enum ConsumableEffect { Health, Stamina, Mana, Defense, Damage }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Consumable Definition")]
    public sealed class ConsumableDefinition : ScriptableObject
    {
        [SerializeField] private ConsumableEffect effect;
        [Tooltip("Resource points, or a fractional damage modifier (0.25 means 25%).")]
        [SerializeField, Min(0)] private float amount;
        [SerializeField, Range(0, 300)] private float duration;
        [SerializeField, Range(0, 300)] private float cooldown = 8;

        public ConsumableEffect Effect => effect;
        public float Amount => amount;
        public float Duration => duration;
        public float Cooldown => cooldown;
        public bool IsBuff => effect == ConsumableEffect.Defense || effect == ConsumableEffect.Damage;
        public bool IsValid => System.Enum.IsDefined(typeof(ConsumableEffect), effect)
            && float.IsFinite(amount) && amount > 0 && amount <= (IsBuff ? 1 : 100000)
            && duration >= 0 && duration <= 300 && (IsBuff ? duration > 0 : duration == 0)
            && cooldown >= 0 && cooldown <= 300;
    }
}
