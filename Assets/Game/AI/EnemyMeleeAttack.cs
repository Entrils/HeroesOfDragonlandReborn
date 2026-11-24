using System;
using System.Collections.Generic;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterStats), typeof(EnemyPerception))]
    public sealed class EnemyMeleeAttack : MonoBehaviour, IMeleeImpactSource
    {
        private EnemyDefinition definition;
        private EnemyAttackSet committedSet;
        private EnemyAttackSet AvailableSet => GetComponent<BossEncounter>() is { } boss && definition.Boss != null ? boss.AttackSet : definition.AttackSet;
        public EnemyAttackSet ActiveSet => IsBusy ? committedSet : AvailableSet;
        private EnemyThreatTarget target;
        private bool projectileReleased;
        private Vector3 projectilePosition;
        private float projectileDistance;
        private float elapsed;
        private float cooldown;
        private Vector3 facing;
        private Vector3 origin;
        private int nextAttack;
        public int AttackIndex { get; private set; }
        public Vector3 AttackOrigin => origin;
        public Vector3 AttackForward => facing;
        public bool UsesAttackSet => definition != null && ActiveSet != null && ActiveSet.Count > 0;
        public float Windup => UsesAttackSet ? ActiveSet.Get(AttackIndex).Windup : definition.AttackWindup;
        public float StaggerPower => definition != null ? definition.StaggerPower : 0;
        public bool IsBusy { get; private set; }
        public float AttackElapsed => elapsed;
        public bool ProjectileInFlight => IsBusy && projectileReleased;
        public Vector3 ProjectilePosition => projectilePosition;
        public bool ResolvingHit { get; private set; }
        public bool IsResolvingMeleeHit => ResolvingHit && IsBusy && !definition.IsRanged;
        public float PoiseDamage => StaggerPower;
        public Vector3 ImpactOrigin => origin;
        public void InterruptByParry(float duration)
        {
            if (!EnemyAuthority.CanSimulate(this) || !GetComponent<CharacterStats>().CanMutate || !IsResolvingMeleeHit) return;
            Cancel(); GetComponent<CombatDefense>()?.OnParried(duration);
        }
        public float CooldownRemaining => cooldown;
        public event Action AttackStarted;
        public event Action AttackFinished;
        public void Configure(EnemyDefinition value) { definition = value; committedSet = null; }
        public void ResetPattern() { if (!IsBusy) { nextAttack = 0; cooldown = 0; } }

        public bool TryStart(EnemyThreatTarget value)
        {
            if (!EnemyAuthority.CanSimulate(this) || !GetComponent<CharacterStats>().CanMutate
                || !isActiveAndEnabled || definition == null || IsBusy || cooldown > 0
                || GetComponent<CharacterStats>().IsDead || !CanHit(value)) return false;
            if (GetComponent<CombatDefense>() is { ActionsBlocked: true } || GetComponent<BossEncounter>() is { Phase: BossPhase.Transition }) return false;
            if (definition.Archetype == EnemyArchetype.Mage && !GetComponent<CharacterStats>().TrySpendMana(definition.SpellManaCost)) return false;
            projectileReleased = false;
            projectileDistance = 0;
            target = value;
            origin = transform.position;
            facing = value.NavigationPosition - origin; facing.y = 0;
            facing = facing.sqrMagnitude < .001f ? transform.forward : facing.normalized;
            if (definition.IsRanged) facing = (value.AimPosition - (origin + Vector3.up)).normalized;
            committedSet = AvailableSet;
            AttackIndex = UsesAttackSet ? nextAttack++ % ActiveSet.Count : 0;
            elapsed = 0;
            cooldown = UsesAttackSet ? Windup + ActiveSet.Get(AttackIndex).Recovery : definition.AttackCooldown;
            IsBusy = true;
            AttackStarted?.Invoke();
            return true;
        }

        // Driven exclusively by EnemyBrain. A long frame still resolves at most one hit.
        public void Tick(float deltaTime)
        {
            if (!EnemyAuthority.CanSimulate(this) || !GetComponent<CharacterStats>().CanMutate
                || !isActiveAndEnabled || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            cooldown = Mathf.Max(0, cooldown - deltaTime);
            if (!IsBusy) return;
            if (GetComponent<CharacterStats>().IsDead) { Cancel(); return; }
            elapsed += deltaTime;
            if (elapsed < Windup) return;
            if (definition.IsRanged)
            {
                TickProjectile(Mathf.Min(deltaTime, elapsed - Windup));
                return;
            }
            if (UsesAttackSet)
            {
                var move = ActiveSet.Get(AttackIndex);
                var hit = new HashSet<EnemyThreatTarget>();
                ResolvingHit = true;
                try
                {
                    foreach (var collider in Physics.OverlapSphere(origin, move.Radius + 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (!IsBusy || GetComponent<CharacterStats>().IsDead) break;
                        var victim = collider.GetComponentInParent<EnemyThreatTarget>();
                        if (victim == null || !hit.Add(victim) || !move.Contains(origin, facing, victim.NavigationPosition)
                            || !GetComponent<EnemyPerception>().CanSee(victim)) continue;
                        victim.Receiver.TakeDamage(move.Damage, gameObject, move.DisplayName);
                    }
                }
                finally { ResolvingHit = false; }
            }
            else if (CanHit(target) && Vector3.Angle(facing, target.NavigationPosition - transform.position) <= 60)
            {
                var receiver = target.Receiver;
                ResolvingHit = true;
                try { receiver.TakeDamage(definition.Damage, gameObject, "Melee attack"); }
                finally { ResolvingHit = false; }
            }
            Cancel();
        }
        private void TickProjectile(float dt)
        {
            if (!projectileReleased)
            {
                projectileReleased = true;
                projectilePosition = origin + Vector3.up;
                // Commit to the advertised aim; projectiles never track a dodging target.
                // facing was fixed at attack acceptance.
            }
            if (dt <= 0) return;
            float step = Mathf.Min(definition.ProjectileSpeed * dt, definition.AttackRange - projectileDistance);
            var hits = Physics.SphereCastAll(projectilePosition, .15f, facing, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                var victim = hit.collider.GetComponentInParent<EnemyThreatTarget>();
                if (victim != null && GetComponent<EnemyPerception>().CanTarget(victim))
                {
                    ResolvingHit = true;
                    try { victim.Receiver.TakeDamage(definition.Damage, gameObject, definition.Archetype == EnemyArchetype.Archer ? "Arrow" : "Amethyst Bolt"); }
                    finally { ResolvingHit = false; }
                }
                Cancel(); return;
            }
            projectilePosition += facing * step;
            projectileDistance += step;
            if (projectileDistance >= definition.AttackRange) Cancel();
        }
        private bool CanHit(EnemyThreatTarget value) => value != null && value.IsAlive
            && Vector3.Distance(transform.position, value.NavigationPosition) <= definition.AttackRange
            && GetComponent<EnemyPerception>().CanSee(value);
        public void Cancel()
        {
            if (!IsBusy) return;
            IsBusy = false;
            target = null;
            AttackFinished?.Invoke();
        }
        private void OnDisable() => Cancel();
    }
}
