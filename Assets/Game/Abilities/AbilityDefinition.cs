using System;
using UnityEngine;

namespace HeroesOfDragonland.Abilities
{
    public enum AbilityEffect { FireProjectile, Heal, Shield }
    public enum AbilityTarget { Forward, SelfOrAlly, Self }
    [Flags] public enum CastInterruption { None = 0, Movement = 1, Damage = 2, Defense = 4, Dodge = 8 }
    public enum AbilityCue { CastStarted, Released, Interrupted, Impact, ShieldEnded }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [SerializeField] private AbilityEffect effect;
        [SerializeField] private AbilityTarget target;
        [SerializeField, Min(0)] private float manaCost = 20, cooldown = 5, castTime = .6f;
        [SerializeField, Min(0)] private float range = 20, magnitude = 30, duration = 5;
        [SerializeField, Min(.01f)] private float projectileSpeed = 15, projectileRadius = .2f;
        [SerializeField] private CastInterruption interruptions = CastInterruption.Movement | CastInterruption.Damage | CastInterruption.Defense | CastInterruption.Dodge;
        [SerializeField] private string animationTrigger;
        [SerializeField] private GameObject castVfx, releaseVfx, impactVfx;
        public AbilityEffect Effect => effect;
        public AbilityTarget Target => target;
        public float ManaCost => manaCost;
        public float Cooldown => cooldown;
        public float CastTime => castTime;
        public float Range => range;
        public float Magnitude => magnitude;
        public float Duration => duration;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRadius => projectileRadius;
        public CastInterruption Interruptions => interruptions;
        public string AnimationTrigger => animationTrigger;
        public GameObject CastVfx => castVfx;
        public GameObject ReleaseVfx => releaseVfx;
        public GameObject ImpactVfx => impactVfx;
        public bool IsValid => Enum.IsDefined(typeof(AbilityEffect), effect) && Enum.IsDefined(typeof(AbilityTarget), target)
            && (interruptions & ~(CastInterruption.Movement | CastInterruption.Damage | CastInterruption.Defense | CastInterruption.Dodge)) == 0
            && Finite(manaCost) && Finite(cooldown) && Finite(castTime) && Finite(range) && range > 0
            && Finite(magnitude) && magnitude > 0 && Finite(duration) && duration > 0
            && Finite(projectileSpeed) && projectileSpeed > 0 && Finite(projectileRadius) && projectileRadius > 0
            && target == (effect == AbilityEffect.FireProjectile ? AbilityTarget.Forward : effect == AbilityEffect.Heal ? AbilityTarget.SelfOrAlly : AbilityTarget.Self);
        private static bool Finite(float value) => value >= 0 && !float.IsInfinity(value);
    }
}
