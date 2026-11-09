using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Melee Attack")]
    public sealed class MeleeAttackDefinition : ScriptableObject
    {
        [Tooltip("Unarmed fallback damage. An equipped weapon supplies its own base damage.")]
        [SerializeField, Min(0f)] private float damage = 25f;
        [Tooltip("Unarmed fallback cost. An equipped weapon supplies its own stamina cost.")]
        [SerializeField, Min(0f)] private float staminaCost = 25f;
        [SerializeField, Min(0.05f)] private float attackDuration = 0.55f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float activeStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float activeEnd = 0.65f;
        [SerializeField, Range(0f, 1f)] private float movementMultiplier = 0.25f;
        [SerializeField] private Vector3 hitCenter = new Vector3(0f, 1f, 1.2f);
        [SerializeField] private Vector3 hitHalfExtents = new Vector3(0.8f, 0.8f, 0.9f);

        [SerializeField, Min(0)] private float staggerPower = 20f;
        [SerializeField, Min(1.01f)] private float heavyDamageMultiplier = 1.8f;
        [SerializeField, Min(1.01f)] private float heavyStaminaMultiplier = 1.6f;
        [SerializeField, Min(1.01f)] private float heavyTimingMultiplier = 1.6f;
        [SerializeField, Min(1.01f)] private float heavyStaggerMultiplier = 3f;
        public float StaggerPower => Mathf.Max(0, staggerPower);
        public float HeavyDamageMultiplier => Mathf.Max(1.01f, heavyDamageMultiplier);
        public float HeavyStaminaMultiplier => Mathf.Max(1.01f, heavyStaminaMultiplier);
        public float HeavyTimingMultiplier => Mathf.Max(1.01f, heavyTimingMultiplier);
        public float HeavyStaggerMultiplier => Mathf.Max(1.01f, heavyStaggerMultiplier);
        public float Damage => damage;
        public float StaminaCost => staminaCost;
        public float AttackDuration => attackDuration;
        public float RecoveryDuration => recoveryDuration;
        public float ActiveStartTime => attackDuration * activeStart;
        public float ActiveEndTime => attackDuration * activeEnd;
        public float MovementMultiplier => movementMultiplier;
        public Vector3 HitCenter => hitCenter;
        public Vector3 HitHalfExtents => hitHalfExtents;

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            staminaCost = Mathf.Max(0f, staminaCost);
            attackDuration = Mathf.Max(0.05f, attackDuration);
            recoveryDuration = Mathf.Max(0f, recoveryDuration);
            activeStart = Mathf.Clamp(activeStart, 0f, 0.99f);
            activeEnd = Mathf.Clamp(activeEnd, activeStart + 0.01f, 1f);
            hitHalfExtents = new Vector3(Mathf.Max(0.01f, hitHalfExtents.x),
                Mathf.Max(0.01f, hitHalfExtents.y), Mathf.Max(0.01f, hitHalfExtents.z));
        }
    }
}
