using System;
using System.Collections.Generic;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.EquipmentSystem;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public sealed class MeleeAttack : MonoBehaviour, IMeleeImpactSource
    {
        [SerializeField] private MeleeAttackDefinition definition;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private LayerMask obstructionMask = ~0;
        private CharacterStats stats;
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private float elapsed;
        private Vector3 committedFacing;
        private float damageMultiplier = 1f;
        private MeleeAttackDefinition committedDefinition;
        private WeaponDefinition committedWeapon;
        private WeaponMove committedMove;
        private WeaponMoveset committedMoveset;
        private readonly AttackChainState chain = new AttackChainState();
        public int MoveIndex { get; private set; }
        public bool HasBufferedAttack => chain.HasBufferedAttack;
        public float RestartRemaining => chain.RestartRemaining;
        public float RecoveryDuration => Definition != null ? Definition.RecoveryDuration * Timing : 0;
        public float AttackElapsed => elapsed;
        public bool CanDodgeCancel => IsBusy && chain.CanDodgeCancel(elapsed, SwingDuration, RecoveryDuration);
        private float committedDamage, committedTiming = 1f;
        private Vector3 committedHitCenter, committedHitHalfExtents;
        private WeaponDefinition EquippedWeapon => GetComponent<Equipment>()?.EquippedWeapon;
        public WeaponDefinition CurrentWeapon => IsBusy ? committedWeapon : EquippedWeapon;
        public float SwingDuration => Definition != null ? Definition.AttackDuration * Timing : 0;
        public bool IsHeavy { get; private set; }
        private bool resolvingHit;
        public bool IsResolvingMeleeHit => resolvingHit && IsBusy;
        public float PoiseDamage => StaggerPower;
        public Vector3 ImpactOrigin => transform.position;
        public void InterruptByParry(float duration)
        {
            if (!stats.CanMutate || !IsResolvingMeleeHit || !IsBusy) return;
            Cancel(); GetComponent<CombatDefense>()?.OnParried(duration);
        }
        private float Timing => IsBusy ? committedTiming : 1f;
        public float StaggerPower => IsBusy && committedMove != null ? committedMove.StaggerPower
            : Definition == null ? 0f : Definition.StaggerPower * (IsHeavy ? Definition.HeavyStaggerMultiplier : 1f);
        public bool IsBusy { get; private set; }
        public MeleeAttackDefinition Definition => IsBusy ? committedDefinition
            : EquippedWeapon != null ? EquippedWeapon.Moveset != null ? EquippedWeapon.Moveset.GetMove(false, 0)?.Profile
                : EquippedWeapon.AttackAnimationSet?.CombatProfile : definition;
        public bool IsSwinging => IsBusy && elapsed < Definition.AttackDuration * Timing;
        public float SwingProgress => Definition != null ? Mathf.Clamp01(elapsed / (Definition.AttackDuration * Timing)) : 0f;
        public float MovementMultiplier => IsBusy ? committedMove?.MovementMultiplier ?? Definition.MovementMultiplier : 1f;
        public Vector3? FacingDirection => IsBusy ? committedFacing : (Vector3?)null;
        public event Action AttackStarted;
        public event Action AttackFinished;

        private void Awake() => stats = GetComponent<CharacterStats>();
        private void OnEnable()
        {
            var gear = GetComponent<Equipment>();
            if (gear != null) gear.Changed += EquipmentChanged;
            stats.Died += Cancel;
        }
        private void EquipmentChanged(EquipmentSlot slot, string previous, string current)
        { if (slot == EquipmentSlot.MainHand) chain.Invalidate(); }

        // Input/AI supply an intent. This component owns cost, timing and damage.
        public bool TryAttack(Vector3? facing = null)
            => RequestAttack(AttackInputKind.Light, facing);

        public bool TryHeavyAttack(Vector3? facing = null) => RequestAttack(AttackInputKind.Heavy, facing);
        public bool RequestAttack(AttackInputKind input, Vector3? facing = null)
        {
            if (!isActiveAndEnabled || !stats.CanMutate || stats.IsDead || !System.Enum.IsDefined(typeof(AttackInputKind), input)) return false;
            if (GetComponent<CombatDefense>() is { ActionsBlocked: true }) return false;
            Vector3 direction = facing ?? transform.forward;
            if (!Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z)) return false;
            if (IsBusy)
                return committedWeapon == EquippedWeapon && committedMoveset == committedWeapon?.Moveset
                    && chain.TryQueue(input, direction, elapsed, SwingDuration + RecoveryDuration);
            return TryBeginAttack(direction, 1, true, heavy: input == AttackInputKind.Heavy);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal void DiscardBufferedAttack() => chain.DiscardBuffer();
        internal bool TryCancelForDodge()
        {
            if (!stats.CanMutate || stats.IsDead || !CanDodgeCancel) return false;
            Cancel(); return true;
        }

        // Skill authority pays Mana. Geometry, timing, obstruction and hit deduplication
        // remain identical to ordinary melee attacks.
        internal bool TrySkillAttack(float multiplier, float manaCost) => TryBeginAttack(null, multiplier, false, manaCost);

        private bool TryBeginAttack(Vector3? facing, float multiplier, bool spendStamina, float manaCost = 0, bool heavy = false,
            bool continuation = false, int index = 0)
        {
            if (!isActiveAndEnabled || !stats.CanMutate || (IsBusy && !continuation) || (!continuation && !chain.CanStart)
                || Definition == null || stats.IsDead) return false;
            if (GetComponent<CombatDefense>() is { ActionsBlocked: true }) return false;
            var dodge = GetComponent<HeroesOfDragonland.Player.PlayerDodge>();
            if (dodge != null && dodge.IsBusy) return false;
            var networkDodge = GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>();
            if (networkDodge != null && networkDodge.Busy) return false;
            if (multiplier < 0 || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return false;
            var gear = GetComponent<Equipment>();
            if (gear != null && !gear.CanUseEquippedWeapon(out _)) return false;
            var weapon = EquippedWeapon;
            if (weapon != null && !weapon.TryValidate(out _)) return false;
            var moveset = weapon != null ? weapon.Moveset : null;
            if (continuation && (weapon != committedWeapon || moveset != committedMoveset || moveset == null)) return false;
            var move = moveset != null ? moveset.GetMove(heavy, index) : null;
            if (moveset != null && move == null) return false;
            var profile = move != null ? move.Profile : Definition;
            float staminaCost = move != null ? move.StaminaCost : weapon != null ? weapon.StaminaCost : profile.StaminaCost;
            if (spendStamina && !stats.TrySpendStamina(staminaCost * (move == null && heavy ? profile.HeavyStaminaMultiplier : 1f))) return false;
            if (!spendStamina && !stats.TrySpendMana(manaCost)) return false;
            if (continuation) Finish(false);
            committedDefinition = profile;
            committedWeapon = weapon;
            committedMove = move;
            committedMoveset = moveset;
            MoveIndex = index;
            committedTiming = move != null ? 1f / move.AttackSpeed
                : (heavy ? profile.HeavyTimingMultiplier : 1f) / (weapon != null ? weapon.AttackSpeed : 1f);
            committedHitCenter = profile.HitCenter;
            committedHitHalfExtents = profile.HitHalfExtents;
            if (weapon != null)
            {
                float reach = move != null ? move.Range : weapon.Range;
                committedHitCenter.z = reach * 0.5f;
                committedHitHalfExtents.z = reach * 0.5f;
            }
            float baseDamage = weapon != null ? weapon.BaseDamage : profile.Damage;
            var equipment = GetComponent<Equipment>();
            committedDamage = equipment != null ? equipment.GetModifiedStat(ItemStat.AttackDamage, baseDamage) : baseDamage;
            var skills = GetComponent<HeroesOfDragonland.Skills.SessionSkills>();
            if (skills != null) committedDamage = skills.ModifyMeleeDamage(committedDamage);
            IsHeavy = heavy;
            damageMultiplier = multiplier * (move != null ? move.DamageMultiplier : heavy ? profile.HeavyDamageMultiplier : 1f);
            committedFacing = facing ?? transform.forward;
            committedFacing.y = 0f;
            committedFacing = committedFacing.sqrMagnitude > 0.001f ? committedFacing.normalized : transform.forward;
            elapsed = 0f;
            hitTargets.Clear();
            IsBusy = true;
            chain.Begin(moveset?.Combo, heavy, index, continuation, spendStamina);
            AttackStarted?.Invoke();
            return true;
        }

        // After player movement, so overlap uses the current character pose.
        private void LateUpdate() { if (!stats.UsesNetworkClock) TickAttack(Time.deltaTime); }

        // Manual ticking is useful for deterministic checks; don't also tick from another frame loop.
        public void TickAttack(float deltaTime)
        {
            if (!stats.CanMutate || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            chain.Advance(deltaTime);
            if (stats.IsDead) { Cancel(); return; }
            if (!IsBusy) return;
            float previous = elapsed;
            elapsed += deltaTime;
            // Test even when a long frame crosses the entire active window.
            if (previous < Definition.ActiveEndTime * Timing && elapsed >= Definition.ActiveStartTime * Timing)
            {
                resolvingHit = true;
                try { ResolveHits(); } finally { resolvingHit = false; }
            }
            if (!IsBusy) return; // A hit callback may force an interruption.
            if (chain.TryConsume(elapsed, SwingDuration, RecoveryDuration, out bool heavy, out int index, out Vector3 facing)
                && TryBeginAttack(facing, 1, true, heavy: heavy, continuation: true, index: index)) return;
            if (elapsed >= (Definition.AttackDuration + Definition.RecoveryDuration) * Timing)
            {
                Finish();
            }
        }

        private void ResolveHits()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 center = transform.TransformPoint(committedHitCenter);
            foreach (var collider in Physics.OverlapBox(center, committedHitHalfExtents,
                         transform.rotation, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsBusy || stats.IsDead) break; // A parry may interrupt this swing inside TakeDamage.
                if (collider.transform == transform || collider.transform.IsChildOf(transform)) continue;
                var receiver = collider.GetComponentInParent<IDamageable>();
                if (receiver == null || !receiver.IsAlive || hitTargets.Contains(receiver)) continue;
                var receiverComponent = receiver as Component;
                if (receiverComponent == null || receiverComponent.transform == transform
                    || receiverComponent.transform.IsChildOf(transform)) continue;
                Vector3 point = collider.ClosestPoint(origin);
                Vector3 ray = point - origin;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
                             obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform)
                        || hit.collider.GetComponentInParent<IDamageable>() == receiver) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                hitTargets.Add(receiver);
                receiver.TakeDamage(committedDamage * damageMultiplier, gameObject);
            }
        }

        private void OnDisable()
        {
            var gear = GetComponent<Equipment>();
            if (gear != null) gear.Changed -= EquipmentChanged;
            stats.Died -= Cancel;
            Cancel();
        }
        internal void Cancel()
        {
            Finish();
        }
        private void Finish(bool completeChain = true)
        {
            if (completeChain) chain.Finish();
            if (!IsBusy) return;
            IsBusy = false;
            hitTargets.Clear();
            AttackFinished?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (Definition == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Vector3 center = IsBusy ? committedHitCenter : Definition.HitCenter;
            Vector3 extents = IsBusy ? committedHitHalfExtents : Definition.HitHalfExtents;
            var weapon = CurrentWeapon;
            if (!IsBusy && weapon != null) { center.z = weapon.Range * .5f; extents.z = weapon.Range * .5f; }
            Gizmos.DrawWireCube(center, extents * 2f);
        }
    }
}
