using System;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
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
            float applied = stats.ApplyDamage(amount);
            if (applied > 0f) DamageTaken?.Invoke(applied, source);
            return applied;
        }

        private void HandleDeath()
        {
            if (deactivateOnDeath) gameObject.SetActive(false);
        }
    }
}
