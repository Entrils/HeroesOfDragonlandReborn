using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    // Simulation-time bookkeeping only; MeleeAttack revalidates legality and costs when consuming a request.
    public sealed class AttackChainState
    {
        private WeaponComboDefinition definition;
        private AttackChainRule rule;
        private float clock, expires, restartUntil;
        private int count;
        private AttackInputKind pending;
        private Vector3 pendingFacing;
        private bool buffered;
        public bool HasBufferedAttack => buffered && clock <= expires;
        public bool CanStart => clock >= restartUntil;
        public float RestartRemaining => Mathf.Max(0, restartUntil - clock);
        public void Advance(float deltaTime)
        {
            clock += deltaTime;
            if (clock > expires) buffered = false;
        }
        public void Begin(WeaponComboDefinition combo, bool heavy, int index, bool continuation, bool chainable)
        {
            definition = combo; rule = chainable ? combo?.GetRule(heavy, index) : null;
            count = continuation ? count + 1 : 1;
            buffered = false;
        }
        public bool TryQueue(AttackInputKind input, Vector3 facing, float elapsed, float totalDuration)
        {
            if (rule == null || definition == null || HasBufferedAttack || count >= definition.MaximumChainAttacks) return false;
            float normalized = elapsed / Mathf.Max(.001f, totalDuration);
            if (normalized < rule.BufferStart || normalized > rule.BufferEnd
                || (input == AttackInputKind.Light ? rule.NextLightIndex < 0 : !rule.AllowHeavy)) return false;
            pending = input; pendingFacing = facing; buffered = true; expires = clock + definition.BufferLifetime;
            return true;
        }
        public bool TryConsume(float elapsed, float swing, float recovery, out bool heavy, out int index, out Vector3 facing)
        {
            heavy = pending == AttackInputKind.Heavy; index = heavy ? 0 : rule?.NextLightIndex ?? -1; facing = pendingFacing;
            if (!HasBufferedAttack || rule == null || elapsed < swing + recovery * rule.MinimumRecovery) return false;
            if (definition == null || count >= definition.MaximumChainAttacks || (heavy ? !rule.AllowHeavy : index < 0))
            { buffered = false; return false; }
            buffered = false;
            return true;
        }
        public bool CanDodgeCancel(float elapsed, float swing, float recovery) => rule != null && rule.AllowDodgeCancel
            && elapsed >= swing + recovery * rule.DodgeCancelRecovery;
        public void DiscardBuffer() => buffered = false;
        public void Finish()
        {
            restartUntil = Mathf.Max(restartUntil, clock + (definition != null ? definition.RestartDelay : .15f));
            buffered = false; rule = null; count = 0;
        }
        public void Invalidate() { buffered = false; rule = null; }
    }
}
