using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Arrow Definition")]
    public sealed class ArrowDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private float damageMultiplier = 1;
        [SerializeField, Min(.01f)] private float speedMultiplier = 1;
        public float DamageMultiplier => damageMultiplier;
        public float SpeedMultiplier => speedMultiplier;
        public bool IsValid => float.IsFinite(damageMultiplier) && damageMultiplier >= 0
            && float.IsFinite(speedMultiplier) && speedMultiplier >= .01f;
    }

    // Implement on the shooter when an inventory-backed arrow supply is available.
    // Returning false stops the release without spawning a shot.
    public interface IArrowSupply
    {
        bool TryTakeArrow(ArrowDefinition basicArrow, out ArrowDefinition arrow);
    }
}
