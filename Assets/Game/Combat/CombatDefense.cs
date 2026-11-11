using UnityEngine;
using HeroesOfDragonland.Stats;
using System;

namespace HeroesOfDragonland.Combat
{
    // Only the stats authority advances timers or resolves defense. Clients receive snapshots.
    public enum DefenseInterrupt { None, PoiseBreak, GuardBreak, Parried }

    [DisallowMultipleComponent, RequireComponent(typeof(CharacterStats), typeof(CharacterPoise))]
    public sealed class CombatDefense : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float blockedDamageMultiplier = 0.25f;
        [SerializeField, Min(0)] private float blockStaminaPerDamage = 1f;
        [SerializeField, Min(0)] private float blockStaminaPerSecond = 10f;
        [SerializeField, Min(0.05f)] private float staggerDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float guardBreakDuration = 1f;
        [SerializeField, Min(0.1f)] private float recoveryImmunity = 1f;
        private CharacterStats stats;
        private CharacterPoise poise;
        private float remaining, immunity;
        private bool releaseRequired;
        public bool IsBlocking { get; private set; }
        public bool IsStaggered => remaining > 0;
        public bool ActionsBlocked => IsBlocking || IsStaggered || GetComponent<ParryDefense>() is { IsBusy: true };
        public float MovementMultiplier => IsStaggered ? 0f : GetComponent<ParryDefense>() is { IsBusy: true } parry
            ? parry.Definition != null ? parry.Definition.MovementMultiplier : 0 : IsBlocking ? 0.4f : 1f;
        public int GuardBreakSequence { get; private set; }
        public int StaggerSequence { get; private set; }
        public DefenseInterrupt LastInterrupt { get; private set; }
        public float StaggerRemaining => remaining;
        public float ImmunityRemaining => immunity;
        public float StaggerDuration => Safe(staggerDuration, .05f, .45f);
        public float GuardBreakDuration => Safe(guardBreakDuration, .05f, 1);
        public float RecoveryImmunity => Safe(recoveryImmunity, .1f, 1);
        public float BlockedDamageMultiplier => Mathf.Clamp01(Safe(blockedDamageMultiplier, 0, .25f));
        public float BlockStaminaPerDamage => Safe(blockStaminaPerDamage, 0, 1);
        public float BlockStaminaPerSecond => Safe(blockStaminaPerSecond, 0, 10);
        private static float Safe(float value, float minimum, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);
        public event Action<DefenseInterrupt> Interrupted;
        private void Awake() { stats = GetComponent<CharacterStats>(); poise = GetComponent<CharacterPoise>(); }
        private void OnEnable() => stats.StaminaChanged += StaminaChanged;
        private void OnDisable() { stats.StaminaChanged -= StaminaChanged; ResetState(); }
        private void StaminaChanged(float current, float maximum) { if (stats.CanMutate && IsBlocking && current <= 0) BreakGuard(); }
        public void SetBlockIntent(bool held, bool actionBusy)
        {
            if (!stats.CanMutate) return;
            if (!held) releaseRequired = false;
            IsBlocking = isActiveAndEnabled && held && !releaseRequired && !actionBusy && !IsStaggered
                && !(GetComponent<ParryDefense>() is { IsBusy: true })
                && !stats.IsDead && stats.CurrentStamina > 0;
        }
        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled || !stats.CanMutate || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            remaining = Mathf.Max(0, remaining - deltaTime);
            immunity = Mathf.Max(0, immunity - deltaTime);
            if (stats.IsDead) { ResetState(); return; }
            poise?.Tick(deltaTime);
            GetComponent<ParryDefense>()?.Tick(deltaTime);
            if (IsBlocking) SpendGuardStamina(BlockStaminaPerSecond * deltaTime);
        }
        public float BlockDamage(float amount)
        {
            if (!stats.CanMutate || !IsBlocking) return amount;
            if (amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return 0;
            SpendGuardStamina(amount * BlockStaminaPerDamage);
            return amount * BlockedDamageMultiplier;
        }
        private void SpendGuardStamina(float cost)
        {
            stats.TrySpendStamina(Mathf.Min(stats.CurrentStamina, Mathf.Max(0, cost)));
            if (IsBlocking && stats.CurrentStamina <= 0) BreakGuard();
        }
        private void BreakGuard()
        {
            if (!stats.CanMutate || !IsBlocking) return;
            IsBlocking = false;
            releaseRequired = true;
            GuardBreakSequence++;
            // During recovery immunity exhaustion still drops guard, but cannot chain a stun.
            Interrupt(GuardBreakDuration, DefenseInterrupt.GuardBreak);
        }
        public void ApplyStagger(float power)
            => ApplyPoiseDamage(power); // Compatibility with existing server probes/abilities.
        public void ApplyPoiseDamage(float power)
        {
            if (!isActiveAndEnabled || !stats.CanMutate || stats.IsDead || immunity > 0) return;
            if (poise != null && poise.ApplyDamage(power)) Interrupt(StaggerDuration, DefenseInterrupt.PoiseBreak);
        }
        internal void OnParried(float duration)
        {
            if (!stats.CanMutate || stats.IsDead || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            // Protection prevents repeated stun extensions; a successfully parried swing still stops.
            CancelCommittedActions();
            Interrupt(duration, DefenseInterrupt.Parried);
        }
        private void Interrupt(float duration, DefenseInterrupt reason)
        {
            if (immunity > 0 || remaining > 0) return;
            remaining = Mathf.Max(0.05f, duration);
            immunity = remaining + RecoveryImmunity;
            LastInterrupt = reason; StaggerSequence++;
            poise?.ResetForSpawn();
            IsBlocking = false;
            CancelCommittedActions();
            Interrupted?.Invoke(reason);
        }
        private void CancelCommittedActions()
        {
            GetComponent<MeleeAttack>()?.Cancel();
            GetComponent<HeroesOfDragonland.AI.EnemyMeleeAttack>()?.Cancel();
            GetComponent<HeroesOfDragonland.Player.PlayerDodge>()?.Cancel();
            GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>()?.CancelForRespawn();
            GetComponent<HeroesOfDragonland.AI.EnemyNavigation>()?.Stop();
            GetComponent<ParryDefense>()?.Cancel();
        }
        internal void ResetState()
        {
            IsBlocking = false; remaining = immunity = 0; releaseRequired = false; LastInterrupt = DefenseInterrupt.None;
            poise?.ResetForSpawn(); GetComponent<ParryDefense>()?.ResetForSpawn();
        }
    }
}
