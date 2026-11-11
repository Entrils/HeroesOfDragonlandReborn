using System;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    public enum AttackInputKind { Light, Heavy }

    [Serializable]
    public sealed class AttackChainRule
    {
        [Tooltip("Light step index, or -1 to end the light chain.")]
        [SerializeField] private int nextLightIndex = -1;
        [SerializeField] private bool allowHeavy;
        [Tooltip("Normalized swing + recovery time. Only presses inside this interval can queue.")]
        [SerializeField, Range(0, 1)] private float bufferStart = .5f;
        [SerializeField, Range(0, 1)] private float bufferEnd = .95f;
        [Tooltip("Mandatory recovery fraction before a buffered transition. The full swing always completes.")]
        [SerializeField, Range(0, 1)] private float minimumRecovery = .5f;
        [SerializeField] private bool allowDodgeCancel;
        [SerializeField, Range(0, 1)] private float dodgeCancelRecovery = .85f;
        public int NextLightIndex => nextLightIndex;
        public bool AllowHeavy => allowHeavy;
        public float BufferStart => bufferStart;
        public float BufferEnd => bufferEnd;
        public float MinimumRecovery => minimumRecovery;
        public bool AllowDodgeCancel => allowDodgeCancel;
        public float DodgeCancelRecovery => dodgeCancelRecovery;
        internal bool IsValid(int count) => nextLightIndex >= -1 && nextLightIndex < count
            && Unit(bufferStart) && Unit(bufferEnd) && bufferEnd >= bufferStart
            && Unit(minimumRecovery) && Unit(dodgeCancelRecovery);
        private static bool Unit(float value) => value >= 0 && value <= 1;
    }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Weapon Combo Definition")]
    public sealed class WeaponComboDefinition : ScriptableObject
    {
        [SerializeField] private AttackChainRule[] lightSteps = Array.Empty<AttackChainRule>();
        [SerializeField] private AttackChainRule heavyStep = new AttackChainRule();
        [SerializeField, Min(.01f)] private float bufferLifetime = .6f;
        [SerializeField, Min(.01f)] private float restartDelay = .3f;
        [Tooltip("Hard bound even if authored links contain a cycle.")]
        [SerializeField, Range(1, 32)] private int maximumChainAttacks = 4;
        public float BufferLifetime => bufferLifetime;
        public float RestartDelay => restartDelay;
        public int MaximumChainAttacks => maximumChainAttacks;
        public AttackChainRule GetRule(bool heavy, int index) => heavy ? heavyStep
            : index >= 0 && index < lightSteps.Length ? lightSteps[index] : null;
        public bool TryValidate(int lightCount, out string error)
        {
            error = null;
            if (lightSteps == null || lightSteps.Length != lightCount || maximumChainAttacks < 1 || maximumChainAttacks > 32
                || !Positive(bufferLifetime) || !Positive(restartDelay) || heavyStep == null || !heavyStep.IsValid(lightCount)
                || heavyStep.NextLightIndex != -1 || heavyStep.AllowHeavy)
                error = "Combo requires matching light rules, a terminal heavy, finite positive timers and a bounded attack count.";
            else foreach (var rule in lightSteps)
                if (rule == null || !rule.IsValid(lightCount)) { error = "Invalid combo link/window/cancel rule."; break; }
            return error == null;
        }
        private static bool Positive(float value) => value >= .01f && !float.IsInfinity(value);
    }
}
