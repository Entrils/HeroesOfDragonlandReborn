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
        private EnemyThreatTarget target;
        private float elapsed;
        private float cooldown;
        private Vector3 facing;
        private Vector3 origin;
        private int nextAttack;
        public int AttackIndex { get; private set; }
        public Vector3 AttackOrigin => origin;
        public Vector3 AttackForward => facing;
        public bool UsesAttackSet => definition != null && definition.AttackSet != null && definition.AttackSet.Count > 0;
        public float Windup => UsesAttackSet ? definition.AttackSet.Get(AttackIndex).Windup : definition.AttackWindup;
        public float StaggerPower => definition != null ? definition.StaggerPower : 0;
        public bool IsBusy { get; private set; }
        public bool ResolvingHit { get; private set; }
        public bool IsResolvingMeleeHit => ResolvingHit && IsBusy;
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
        public void Configure(EnemyDefinition value) => definition = value;

        public bool TryStart(EnemyThreatTarget value)
        {
            if (!EnemyAuthority.CanSimulate(this) || !GetComponent<CharacterStats>().CanMutate
                || !isActiveAndEnabled || definition == null || IsBusy || cooldown > 0
                || GetComponent<CharacterStats>().IsDead || !CanHit(value)) return false;
            if (GetComponent<CombatDefense>() is { ActionsBlocked: true }) return false;
            target = value;
            origin = transform.position;
            facing = value.NavigationPosition - origin; facing.y = 0;
            facing = facing.sqrMagnitude < .001f ? transform.forward : facing.normalized;
            AttackIndex = UsesAttackSet ? nextAttack++ % definition.AttackSet.Count : 0;
            elapsed = 0;
            cooldown = UsesAttackSet ? Windup + definition.AttackSet.Get(AttackIndex).Recovery : definition.AttackCooldown;
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
            if (UsesAttackSet)
            {
                var move = definition.AttackSet.Get(AttackIndex);
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
                        victim.Receiver.TakeDamage(move.Damage, gameObject);
                    }
                }
                finally { ResolvingHit = false; }
            }
            else if (CanHit(target) && Vector3.Angle(facing, target.NavigationPosition - transform.position) <= 60)
            {
                IDamageable receiver = target.Receiver;
                ResolvingHit = true;
                try { receiver.TakeDamage(definition.Damage, gameObject); }
                finally { ResolvingHit = false; }
            }
            Cancel();
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
