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
    public sealed class MeleeAttack : MonoBehaviour
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
        private float committedDamage, committedTiming = 1f;
        private Vector3 committedHitCenter, committedHitHalfExtents;
        private WeaponDefinition EquippedWeapon => GetComponent<Equipment>()?.EquippedWeapon;
        public WeaponDefinition CurrentWeapon => IsBusy ? committedWeapon : EquippedWeapon;
        public float SwingDuration => Definition != null ? Definition.AttackDuration * Timing : 0;
        public bool IsHeavy { get; private set; }
        private float Timing => IsBusy ? committedTiming : 1f;
        public float StaggerPower => Definition == null ? 0f : Definition.StaggerPower * (IsHeavy ? Definition.HeavyStaggerMultiplier : 1f);
        public bool IsBusy { get; private set; }
        public MeleeAttackDefinition Definition => IsBusy ? committedDefinition
            : EquippedWeapon != null ? EquippedWeapon.AttackAnimationSet?.CombatProfile : definition;
        public bool IsSwinging => IsBusy && elapsed < Definition.AttackDuration * Timing;
        public float SwingProgress => Definition != null ? Mathf.Clamp01(elapsed / (Definition.AttackDuration * Timing)) : 0f;
        public float MovementMultiplier => IsBusy ? Definition.MovementMultiplier : 1f;
        public Vector3? FacingDirection => IsBusy ? committedFacing : (Vector3?)null;
        public event Action AttackStarted;
        public event Action AttackFinished;

        private void Awake() => stats = GetComponent<CharacterStats>();

        // Input/AI supply an intent. This component owns cost, timing and damage.
        public bool TryAttack(Vector3? facing = null)
            => TryBeginAttack(facing, 1f, true);

        public bool TryHeavyAttack(Vector3? facing = null) => TryBeginAttack(facing, 1f, true, heavy: true);

        // Skill authority pays Mana. Geometry, timing, obstruction and hit deduplication
        // remain identical to ordinary melee attacks.
        internal bool TrySkillAttack(float multiplier, float manaCost) => TryBeginAttack(null, multiplier, false, manaCost);

        private bool TryBeginAttack(Vector3? facing, float multiplier, bool spendStamina, float manaCost = 0, bool heavy = false)
        {
            if (!isActiveAndEnabled || !stats.CanMutate || IsBusy || Definition == null || stats.IsDead) return false;
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
            var profile = Definition;
            float staminaCost = weapon != null ? weapon.StaminaCost : profile.StaminaCost;
            if (spendStamina && !stats.TrySpendStamina(staminaCost * (heavy ? profile.HeavyStaminaMultiplier : 1f))) return false;
            if (!spendStamina && !stats.TrySpendMana(manaCost)) return false;
            committedDefinition = profile;
            committedWeapon = weapon;
            committedTiming = (heavy ? profile.HeavyTimingMultiplier : 1f) / (weapon != null ? weapon.AttackSpeed : 1f);
            committedHitCenter = profile.HitCenter;
            committedHitHalfExtents = profile.HitHalfExtents;
            if (weapon != null)
            {
                committedHitCenter.z = weapon.Range * 0.5f;
                committedHitHalfExtents.z = weapon.Range * 0.5f;
            }
            float baseDamage = weapon != null ? weapon.BaseDamage : profile.Damage;
            var equipment = GetComponent<Equipment>();
            committedDamage = equipment != null ? equipment.GetModifiedStat(ItemStat.AttackDamage, baseDamage) : baseDamage;
            var skills = GetComponent<HeroesOfDragonland.Skills.SessionSkills>();
            if (skills != null) committedDamage = skills.ModifyMeleeDamage(committedDamage);
            IsHeavy = heavy;
            damageMultiplier = multiplier * (heavy ? profile.HeavyDamageMultiplier : 1f);
            committedFacing = facing ?? transform.forward;
            committedFacing.y = 0f;
            committedFacing = committedFacing.sqrMagnitude > 0.001f ? committedFacing.normalized : transform.forward;
            elapsed = 0f;
            hitTargets.Clear();
            IsBusy = true;
            AttackStarted?.Invoke();
            return true;
        }

        // After player movement, so overlap uses the current character pose.
        private void LateUpdate() { if (!stats.UsesNetworkClock) TickAttack(Time.deltaTime); }

        // Manual ticking is useful for deterministic checks; don't also tick from another frame loop.
        public void TickAttack(float deltaTime)
        {
            if (!stats.CanMutate || !IsBusy || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            if (stats.IsDead) { Cancel(); return; }
            float previous = elapsed;
            elapsed += deltaTime;
            // Test even when a long frame crosses the entire active window.
            if (previous < Definition.ActiveEndTime * Timing && elapsed >= Definition.ActiveStartTime * Timing)
                ResolveHits();
            if (elapsed >= (Definition.AttackDuration + Definition.RecoveryDuration) * Timing) Cancel();
        }

        private void ResolveHits()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 center = transform.TransformPoint(committedHitCenter);
            foreach (var collider in Physics.OverlapBox(center, committedHitHalfExtents,
                         transform.rotation, hitMask, QueryTriggerInteraction.Ignore))
            {
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

        private void OnDisable() => Cancel();
        internal void Cancel()
        {
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
