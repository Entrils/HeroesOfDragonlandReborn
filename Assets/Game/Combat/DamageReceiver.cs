using System;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    public enum DamageImpactCue { MeleeHit, Blocked, Parried, GuardBreak, OtherDamage }
    public enum ImpactWeapon { Unarmed, Sword, Greatsword, Spear, Staff, Bow, Magic }
    public readonly struct DamageImpact
    {
        public readonly ImpactWeapon Weapon;
        public readonly Vector3 Point, Direction;
        public readonly bool Heavy;
        public DamageImpact(ImpactWeapon weapon, Vector3 point, Vector3 direction, bool heavy = false)
        { Weapon = weapon; Point = point; Direction = direction; Heavy = heavy; }
    }
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
        public event Action<DamageImpactCue, GameObject, DamageImpact> ContactResolved;
        public static event Action<DamageReceiver, DamageImpactCue, GameObject, DamageImpact> LocalImpactResolved;
        private void PresentImpact(DamageImpactCue cue, GameObject source, DamageImpact? contact)
        {
            var impact = contact ?? new DamageImpact(ImpactWeapon.Unarmed, transform.position + Vector3.up * .8f,
                source != null ? (transform.position - source.transform.position).normalized : Vector3.zero);
            if (ImpactResolved != null && cue != DamageImpactCue.OtherDamage
                && (impact.Weapon is not (ImpactWeapon.Bow or ImpactWeapon.Magic) || cue != DamageImpactCue.MeleeHit))
                foreach (Action<DamageImpactCue, GameObject> listener in ImpactResolved.GetInvocationList())
                    try { listener(cue == DamageImpactCue.GuardBreak ? DamageImpactCue.Blocked : cue, source); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
            if (ContactResolved != null)
                foreach (Action<DamageImpactCue, GameObject, DamageImpact> listener in ContactResolved.GetInvocationList())
                    try { listener(cue, source, impact); } catch (Exception exception) { Debug.LogException(exception, this); }
            if (!stats.UsesNetworkClock && LocalImpactResolved != null)
                foreach (Action<DamageReceiver, DamageImpactCue, GameObject, DamageImpact> listener in LocalImpactResolved.GetInvocationList())
                    try { listener(this, cue, source, impact); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void Awake() => stats = GetComponent<CharacterStats>();
        private void OnEnable() => stats.Died += HandleDeath;
        private void OnDisable() => stats.Died -= HandleDeath;

        public float TakeDamage(float amount, GameObject source) => TakeDamage(amount, source, null);

        internal float TakeDamage(float amount, GameObject source, string action, DamageImpact? contact = null)
            => TakeDamage(amount, source, action, contact, out _);

        internal float TakeDamage(float amount, GameObject source, string action, DamageImpact? contact, out bool confirmed)
        {
            confirmed = false;
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
            { confirmed = true; PresentImpact(DamageImpactCue.Parried, source, contact); return 0; }
            bool blocked = defense != null && defense.IsBlocking;
            if (source != null && source.TryGetComponent<NetworkInventory>(out var attackerInventory))
                amount *= attackerInventory.OutgoingPotionMultiplier;
            if (TryGetComponent<NetworkInventory>(out var defenderInventory))
                amount *= defenderInventory.IncomingPotionMultiplier;
            if (!float.IsFinite(amount) || amount < 0f) return 0f;
            int previousGuardBreak = defense != null ? defense.GuardBreakSequence : 0;
            if (blocked) amount = defense.BlockDamage(amount);
            bool guardBroken = blocked && defense.GuardBreakSequence != previousGuardBreak;
            float applied = stats.ApplyDamage(amount, source, action);
            if (applied > 0 && !blocked && defense != null)
            {
                if (impact != null) defense.ApplyPoiseDamage(impact.PoiseDamage);
            }
            if (applied > 0f) DamageTaken?.Invoke(applied, source);
            confirmed = blocked || applied > 0;
            if (blocked) PresentImpact(guardBroken ? DamageImpactCue.GuardBreak : DamageImpactCue.Blocked, source, contact);
            else if (applied > 0)
                PresentImpact(impact != null || contact.HasValue ? DamageImpactCue.MeleeHit : DamageImpactCue.OtherDamage, source, contact);
            return applied;
        }

        private void HandleDeath()
        {
            GetComponent<CombatDefense>()?.ResetState();
            if (deactivateOnDeath) gameObject.SetActive(false);
        }
    }
}
