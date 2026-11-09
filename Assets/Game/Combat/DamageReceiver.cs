using System;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats), typeof(CombatDefense))]
    public sealed class DamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] private bool deactivateOnDeath;
        private CharacterStats stats;
        public bool IsAlive => isActiveAndEnabled && stats != null && !stats.IsDead;
        public event Action<float, GameObject> DamageTaken;

        private void Awake() => stats = GetComponent<CharacterStats>();
        private void OnEnable() => stats.Died += HandleDeath;
        private void OnDisable() => stats.Died -= HandleDeath;

        public float TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive || !HeroesOfDragonland.Teams.TeamIdentity.CanDamage(source, gameObject)) return 0f;
            foreach (var policy in GetComponents<IDamagePolicy>())
                if (!policy.AllowsDamage(source)) return 0f;
            if (!stats.CanMutate || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return 0;
            foreach (var provider in GetComponents<IInvulnerabilityProvider>())
                if (provider.IsInvulnerable) return 0;
            var defense = GetComponent<CombatDefense>();
            bool blocked = defense != null && defense.IsBlocking;
            if (blocked) amount = defense.BlockDamage(amount);
            float applied = stats.ApplyDamage(amount);
            if (applied > 0 && !blocked && defense != null)
            {
                var melee = source != null ? source.GetComponent<MeleeAttack>() : null;
                var enemy = source != null ? source.GetComponent<HeroesOfDragonland.AI.EnemyMeleeAttack>() : null;
                float power = melee != null && melee.IsBusy ? melee.StaggerPower
                    : enemy != null && enemy.ResolvingHit ? enemy.StaggerPower : 0;
                defense.ApplyStagger(power);
            }
            if (applied > 0f) DamageTaken?.Invoke(applied, source);
            return applied;
        }

        private void HandleDeath()
        {
            GetComponent<CombatDefense>()?.ResetState();
            if (deactivateOnDeath) gameObject.SetActive(false);
        }
    }
}
