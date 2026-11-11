using System;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [Serializable]
    public sealed class WeaponMove
    {
        [SerializeField] private MeleeAttackDefinition profile;
        [SerializeField] private AnimationClip animation;
        [SerializeField, Min(.05f)] private float attackSpeed = 1;
        [SerializeField, Min(0)] private float staminaCost = 25;
        [SerializeField, Min(.1f)] private float range = 2;
        [SerializeField, Range(0, 1)] private float movementMultiplier = .25f;
        [SerializeField, Min(0)] private float staggerPower = 20;
        [SerializeField, Min(0)] private float damageMultiplier = 1;
        public MeleeAttackDefinition Profile => profile;
        public AnimationClip Animation => animation;
        public float AttackSpeed => attackSpeed;
        public float StaminaCost => staminaCost;
        public float Range => range;
        public float MovementMultiplier => movementMultiplier;
        public float StaggerPower => staggerPower;
        public float DamageMultiplier => damageMultiplier;
        public bool IsValid => profile != null && Finite(attackSpeed, .05f) && Finite(staminaCost, 0)
            && Finite(range, .1f) && Finite(movementMultiplier, 0) && movementMultiplier <= 1
            && Finite(staggerPower, 0) && Finite(damageMultiplier, 0)
            && Finite(profile.AttackDuration, .05f) && Finite(profile.RecoveryDuration, 0)
            && Finite(profile.ActiveStartTime, 0) && profile.ActiveEndTime > profile.ActiveStartTime
            && profile.ActiveEndTime <= profile.AttackDuration
            && Finite(profile.HitHalfExtents.x, .01f) && Finite(profile.HitHalfExtents.y, .01f)
            && Finite(profile.HitHalfExtents.z, .01f)
            && Finite(Mathf.Abs(profile.HitCenter.x), 0) && Finite(Mathf.Abs(profile.HitCenter.y), 0)
            && (animation == null || animation.humanMotion);
        private static bool Finite(float value, float minimum) => value >= minimum && !float.IsInfinity(value);
    }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Weapon Moveset")]
    public sealed class WeaponMoveset : ScriptableObject
    {
        [Tooltip("Authoring label only. Combat contains no archetype switches.")]
        [SerializeField] private WeaponArchetype archetype;
        [SerializeField] private WeaponAttackAnimationSet animationSet;
        [SerializeField] private WeaponMove[] lightAttacks = Array.Empty<WeaponMove>();
        [SerializeField] private WeaponMove heavyAttack;
        [SerializeField] private WeaponComboDefinition combo;
        public WeaponComboDefinition Combo => combo;
        [Tooltip("Time after full recovery in which the next accepted light continues the sequence.")]
        [SerializeField, Min(0)] private float comboResetDelay = .8f;
        public WeaponArchetype Archetype => archetype;
        public WeaponAttackAnimationSet AnimationSet => animationSet;
        public int LightAttackCount => lightAttacks?.Length ?? 0;
        public float ComboResetDelay => comboResetDelay;
        public WeaponMove GetMove(bool heavy, int index) => heavy ? heavyAttack
            : index >= 0 && index < LightAttackCount ? lightAttacks[index] : null;
        public AnimationClip GetAnimation(bool heavy, int index) => GetMove(heavy, index)?.Animation
            ?? (heavy ? animationSet?.HeavyAttack : animationSet?.LightAttack);
        public bool TryValidate(out string error)
        {
            error = null;
            if (!Enum.IsDefined(typeof(WeaponArchetype), archetype) || LightAttackCount == 0 || LightAttackCount > 16
                || heavyAttack == null || !heavyAttack.IsValid || comboResetDelay < 0
                || float.IsNaN(comboResetDelay) || float.IsInfinity(comboResetDelay))
                error = "Moveset requires 1-16 valid lights, a heavy and finite reset delay.";
            else foreach (var move in lightAttacks)
                if (move == null || !move.IsValid) { error = "Moveset contains an invalid light attack."; break; }
            if (error == null && combo != null && !combo.TryValidate(LightAttackCount, out error)) return false;
            return error == null;
        }
    }
}
