using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterStats), typeof(EnemyPerception))]
    public sealed class EnemyMeleeAttack : MonoBehaviour
    {
        private EnemyDefinition definition;
        private EnemyThreatTarget target;
        private float elapsed;
        private float cooldown;
        private Vector3 facing;
        public float StaggerPower => definition != null ? definition.StaggerPower : 0;
        public bool IsBusy { get; private set; }
        public bool ResolvingHit { get; private set; }
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
            facing = (value.NavigationPosition - transform.position).normalized;
            elapsed = 0;
            cooldown = definition.AttackCooldown;
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
            if (elapsed < definition.AttackWindup) return;
            if (CanHit(target) && Vector3.Angle(facing, target.NavigationPosition - transform.position) <= 60)
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
