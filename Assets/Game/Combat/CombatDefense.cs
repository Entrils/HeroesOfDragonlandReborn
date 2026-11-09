using UnityEngine;
using HeroesOfDragonland.Stats;

namespace HeroesOfDragonland.Combat
{
    // Only the stats authority advances timers or resolves defense. Clients receive snapshots.
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterStats))]
    public sealed class CombatDefense : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float blockedDamageMultiplier = 0.25f;
        [SerializeField, Min(0)] private float blockStaminaPerDamage = 1f;
        [SerializeField, Min(0)] private float blockStaminaPerSecond = 10f;
        [SerializeField, Min(0.05f)] private float staggerDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float guardBreakDuration = 1f;
        [SerializeField, Min(0.1f)] private float recoveryImmunity = 1f;
        [SerializeField, Min(0)] private float staggerDecayPerSecond = 20f;
        private CharacterStats stats;
        private float remaining, immunity, buildup;
        private bool releaseRequired;
        public bool IsBlocking { get; private set; }
        public bool IsStaggered => remaining > 0;
        public bool ActionsBlocked => IsBlocking || IsStaggered;
        public float MovementMultiplier => IsStaggered ? 0f : IsBlocking ? 0.4f : 1f;
        public int GuardBreakSequence { get; private set; }
        private void Awake() => stats = GetComponent<CharacterStats>();
        private void OnDisable() => ResetState();
        public void SetBlockIntent(bool held, bool actionBusy)
        {
            if (!stats.CanMutate) return;
            if (!held) releaseRequired = false;
            IsBlocking = isActiveAndEnabled && held && !releaseRequired && !actionBusy && !IsStaggered
                && !stats.IsDead && stats.CurrentStamina > 0;
        }
        public void Tick(float deltaTime)
        {
            if (!stats.CanMutate || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            remaining = Mathf.Max(0, remaining - deltaTime);
            immunity = Mathf.Max(0, immunity - deltaTime);
            buildup = Mathf.Max(0, buildup - staggerDecayPerSecond * deltaTime);
            if (stats.IsDead) { ResetState(); return; }
            if (IsBlocking) SpendGuardStamina(blockStaminaPerSecond * deltaTime);
        }
        public float BlockDamage(float amount)
        {
            if (!stats.CanMutate || !IsBlocking) return amount;
            SpendGuardStamina(amount * Mathf.Max(0, blockStaminaPerDamage));
            return amount * Mathf.Clamp01(blockedDamageMultiplier);
        }
        private void SpendGuardStamina(float cost)
        {
            stats.TrySpendStamina(Mathf.Min(stats.CurrentStamina, Mathf.Max(0, cost)));
            if (stats.CurrentStamina > 0) return;
            IsBlocking = false;
            releaseRequired = true;
            GuardBreakSequence++;
            // During recovery immunity exhaustion still drops guard, but cannot chain a stun.
            Interrupt(guardBreakDuration);
        }
        public void ApplyStagger(float power)
        {
            if (!stats.CanMutate || stats.IsDead || immunity > 0 || power <= 0 || float.IsNaN(power) || float.IsInfinity(power)) return;
            buildup += power;
            if (buildup >= stats.StaggerResistance) Interrupt(staggerDuration);
        }
        private void Interrupt(float duration)
        {
            if (immunity > 0) return;
            remaining = Mathf.Max(0.05f, duration);
            immunity = remaining + Mathf.Max(0.1f, recoveryImmunity);
            buildup = 0;
            IsBlocking = false;
            GetComponent<MeleeAttack>()?.Cancel();
            GetComponent<HeroesOfDragonland.AI.EnemyMeleeAttack>()?.Cancel();
            GetComponent<HeroesOfDragonland.Player.PlayerDodge>()?.Cancel();
            GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>()?.CancelForRespawn();
            GetComponent<HeroesOfDragonland.AI.EnemyNavigation>()?.Stop();
        }
        internal void ResetState()
        {
            IsBlocking = false; remaining = immunity = buildup = 0; releaseRequired = false;
        }
    }
}
