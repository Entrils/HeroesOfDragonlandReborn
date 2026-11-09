using System;
using System.Collections.Generic;
using HeroesOfDragonland.Stats;
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
        public bool IsBusy { get; private set; }
        public MeleeAttackDefinition Definition => definition;
        public bool IsSwinging => IsBusy && elapsed < definition.AttackDuration;
        public float SwingProgress => definition != null ? Mathf.Clamp01(elapsed / definition.AttackDuration) : 0f;
        public float MovementMultiplier => IsBusy ? definition.MovementMultiplier : 1f;
        public Vector3? FacingDirection => IsBusy ? committedFacing : (Vector3?)null;
        public event Action AttackStarted;
        public event Action AttackFinished;

        private void Awake() => stats = GetComponent<CharacterStats>();

        // Input/AI supply an intent. This component owns cost, timing and damage.
        public bool TryAttack(Vector3? facing = null)
            => TryBeginAttack(facing, 1f, true);

        // Skill authority pays Mana. Geometry, timing, obstruction and hit deduplication
        // remain identical to ordinary melee attacks.
        internal bool TrySkillAttack(float multiplier, float manaCost) => TryBeginAttack(null, multiplier, false, manaCost);

        private bool TryBeginAttack(Vector3? facing, float multiplier, bool spendStamina, float manaCost = 0)
        {
            if (!isActiveAndEnabled || !stats.CanMutate || IsBusy || definition == null || stats.IsDead) return false;
            var dodge = GetComponent<HeroesOfDragonland.Player.PlayerDodge>();
            if (dodge != null && dodge.IsBusy) return false;
            var networkDodge = GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>();
            if (networkDodge != null && networkDodge.Busy) return false;
            if (multiplier < 0 || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return false;
            if (spendStamina && !stats.TrySpendStamina(definition.StaminaCost)) return false;
            if (!spendStamina && !stats.TrySpendMana(manaCost)) return false;
            damageMultiplier = multiplier;
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
            if (previous < definition.ActiveEndTime && elapsed >= definition.ActiveStartTime)
                ResolveHits();
            if (elapsed >= definition.AttackDuration + definition.RecoveryDuration) Cancel();
        }

        private void ResolveHits()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 center = transform.TransformPoint(definition.HitCenter);
            foreach (var collider in Physics.OverlapBox(center, definition.HitHalfExtents,
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
                var equipment = GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>();
                float damage = equipment != null
                    ? equipment.GetModifiedStat(HeroesOfDragonland.Items.ItemStat.AttackDamage, definition.Damage)
                    : definition.Damage;
                var skills = GetComponent<HeroesOfDragonland.Skills.SessionSkills>();
                if (skills != null) damage = skills.ModifyMeleeDamage(damage);
                receiver.TakeDamage(damage * damageMultiplier, gameObject);
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
            if (definition == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(definition.HitCenter, definition.HitHalfExtents * 2f);
        }
    }
}
