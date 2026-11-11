using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Parry Definition")]
    public sealed class ParryDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private float startup = .04f;
        [SerializeField, Min(.01f)] private float window = .18f;
        [SerializeField, Min(.01f)] private float recovery = .35f;
        [Tooltip("Measured from attempt start; must cover startup, window and recovery.")]
        [SerializeField, Min(.01f)] private float cooldown = .8f;
        [SerializeField, Min(0)] private float staminaCost = 15;
        [SerializeField, Range(1, 180)] private float arc = 120;
        [SerializeField, Min(.05f)] private float attackerStagger = .65f;
        [SerializeField, Range(0, 1)] private float movementMultiplier = .3f;
        public float Startup => startup;
        public float Window => window;
        public float Recovery => recovery;
        public float Duration => startup + window + recovery;
        public float Cooldown => cooldown;
        public float StaminaCost => staminaCost;
        public float Arc => arc;
        public float AttackerStagger => attackerStagger;
        public float MovementMultiplier => movementMultiplier;
        public bool IsValid => Finite(startup, 0) && Finite(window, .01f) && Finite(recovery, .01f)
            && Finite(cooldown, Duration) && Finite(staminaCost, 0) && Finite(attackerStagger, .05f)
            && arc >= 1 && arc <= 180 && movementMultiplier >= 0 && movementMultiplier <= 1;
        private static bool Finite(float value, float minimum) => value >= minimum && !float.IsInfinity(value);
    }
}
