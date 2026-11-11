using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;
using HeroesOfDragonland.EquipmentSystem;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement), typeof(CharacterStats))]
    public sealed class PlayerDodge : MonoBehaviour, IInvulnerabilityProvider
    {
        [SerializeField] private DodgeDefinition definition;
        private WeightDodgeModifiers rollModifiers = WeightDodgeModifiers.Light;
        private float RollDuration => definition.Duration * Mathf.Max(0.01f, rollModifiers.duration);
        private float RollRecovery => definition.Recovery * Mathf.Max(0.01f, rollModifiers.recovery);
        private float RollDistance => definition.Distance * Mathf.Max(0.01f, rollModifiers.distance);
        private PlayerMovement movement;
        private CharacterStats stats;
        private MeleeAttack attack;
        private float elapsed;
        public bool IsBusy { get; private set; }
        public bool IsRolling => IsBusy && elapsed < RollDuration;
        public Vector3 Direction { get; private set; }
        public float Progress => definition != null ? Mathf.Clamp01(elapsed / RollDuration) : 0f;
        public bool IsInvulnerable => isActiveAndEnabled && IsRolling && !stats.IsDead
            && elapsed >= (definition.InvulnerabilityStartTime / definition.Duration * RollDuration) && elapsed < (definition.InvulnerabilityEndTime / definition.Duration * RollDuration);
        // Animation adapters can subscribe without owning locomotion or damage policy.
        public event Action DodgeStarted;
        public event Action DodgeFinished;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            stats = GetComponent<CharacterStats>();
            attack = GetComponent<MeleeAttack>();
        }

        public bool TryDodge(Vector3 movementDirection, Vector3? lockFacing = null)
        {
            if (!isActiveAndEnabled || IsBusy || definition == null || stats.IsDead
                || !movement.isActiveAndEnabled || !movement.IsGrounded || (attack != null && attack.IsBusy && !attack.CanDodgeCancel)) return false;
            if (GetComponent<CombatDefense>() is { ActionsBlocked: true }) return false;
            movementDirection.y = 0f;
            Vector3 fallback = lockFacing.HasValue ? -lockFacing.Value : transform.forward;
            fallback.y = 0f;
            Vector3 direction = movementDirection.sqrMagnitude > 0.0001f ? movementDirection : fallback;
            rollModifiers = GetComponent<Equipment>()?.DodgeModifiers ?? WeightDodgeModifiers.Light;
            if (!stats.TrySpendStamina(definition.StaminaCost * Mathf.Max(0.01f, rollModifiers.stamina))) return false;
            if (attack != null && attack.IsBusy && !attack.TryCancelForDodge()) return false;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            elapsed = 0f;
            IsBusy = true;
            DodgeStarted?.Invoke();
            return true;
        }

        // Coordinator calls this instead of normal locomotion while busy. Gravity and
        // collision stay in PlayerMovement; the committed roll direction never steers.
        public void TickDodge(float deltaTime, Vector3? lockFacing = null)
        {
            if (!IsBusy || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            if (stats.IsDead || !movement.isActiveAndEnabled) { Cancel(); return; }
            float rollTime = Mathf.Min(deltaTime, Mathf.Max(0f, RollDuration - elapsed));
            Vector3 velocity = Direction * (RollDistance / RollDuration) * (rollTime / deltaTime);
            movement.Tick(Vector3.zero, false, deltaTime, lockFacing ?? Direction, velocity);
            elapsed += deltaTime;
            if (elapsed >= RollDuration + RollRecovery) Cancel();
        }

        public void Cancel()
        {
            if (!IsBusy) return;
            IsBusy = false;
            DodgeFinished?.Invoke();
        }

        private void OnDisable() => Cancel();
    }
}
