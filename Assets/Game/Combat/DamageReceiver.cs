using System;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    public enum DamageImpactCue { MeleeHit, Blocked, Parried }
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats), typeof(CombatDefense))]
    public sealed class DamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] private bool deactivateOnDeath;
        private CharacterStats stats;
        public bool IsAlive => isActiveAndEnabled && stats != null && !stats.IsDead;
        public event Action<float, GameObject> DamageTaken;
        // Accepted outcomes only, including a fully blocked hit. Observers cannot alter damage.
        public event Action<DamageImpactCue, GameObject> ImpactResolved;
        private void PresentImpact(DamageImpactCue cue, GameObject source)
        {
            if (ImpactResolved == null) return;
            foreach (Action<DamageImpactCue, GameObject> listener in ImpactResolved.GetInvocationList())
                try { listener(cue, source); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

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
            IMeleeImpactSource impact = null;
            if (source != null)
                foreach (var candidate in source.GetComponents<IMeleeImpactSource>())
                    if (candidate.IsResolvingMeleeHit) { impact = candidate; break; }
            if (GetComponent<ParryDefense>() is { } parry && parry.TryResolve(impact))
            { PresentImpact(DamageImpactCue.Parried, source); return 0; }
            bool blocked = defense != null && defense.IsBlocking;
            if (blocked) amount = defense.BlockDamage(amount);
            float applied = stats.ApplyDamage(amount);
            if (applied > 0 && !blocked && defense != null)
            {
                if (impact != null) defense.ApplyPoiseDamage(impact.PoiseDamage);
            }
            if (applied > 0f) DamageTaken?.Invoke(applied, source);
            if (blocked) PresentImpact(DamageImpactCue.Blocked, source);
            else if (applied > 0 && impact != null) PresentImpact(DamageImpactCue.MeleeHit, source);
            return applied;
        }

        private void HandleDeath()
        {
            GetComponent<CombatDefense>()?.ResetState();
            if (deactivateOnDeath) gameObject.SetActive(false);
        }
    }
}
