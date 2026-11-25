using System;
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Abilities
{
    // No client supplied effect values or hit reports. SessionSkills owns learning and cooldowns.
    [DisallowMultipleComponent, RequireComponent(typeof(SessionSkills), typeof(CharacterStats))]
    public sealed class AbilityCaster : MonoBehaviour
    {
        private sealed class Projectile
        {
            public AbilityDefinition Definition;
            public Vector3 Position, Direction;
            public float Remaining;
        }
        private readonly List<Projectile> projectiles = new();
        private CharacterStats stats;
        private SessionSkills skills;
        private AbilityDefinition casting, shieldDefinition;
        private CharacterStats castTarget;
        private Vector3 castOrigin, castDirection;
        private float lastHealth;
        private string session;
        public bool IsCasting => casting != null || CastRemaining > 0;
        public float CastRemaining { get; private set; }
        public float ShieldRemaining { get; private set; }
        public float ShieldCapacity { get; private set; }
        public bool IsResolvingProjectile { get; private set; }
        public event Action<AbilityDefinition, AbilityCue, Vector3, Vector3> Presentation;
        public bool HasAuthority => isActiveAndEnabled && skills != null && skills.HasAuthority && stats.CanMutate;
        private void Awake() { stats = GetComponent<CharacterStats>(); skills = GetComponent<SessionSkills>(); }
        private void OnEnable() { stats.HealthChanged += HealthChanged; stats.Died += OnDeath; lastHealth = stats.CurrentHealth; }
        private void OnDisable() { stats.HealthChanged -= HealthChanged; stats.Died -= OnDeath; Clear(); }
        private void OnDeath() => Clear();
        private void HealthChanged(float health, float maximum)
        {
            if (health < lastHealth && casting != null && Has(CastInterruption.Damage)) Interrupt();
            lastHealth = health;
        }
        private bool Has(CastInterruption flag) => (casting.Interruptions & flag) != 0;
        private float ManaCost(AbilityDefinition definition) =>
            definition.ManaCost * stats.SkillCostMultiplier(SkillEffect.SpellEfficiency);

        public bool CanStart(AbilityDefinition definition, CharacterStats target, out string reason)
        {
            reason = "";
            if (definition == null || !definition.IsValid) reason = "Ability data is invalid.";
            else if (IsCasting) reason = "Already casting.";
            else if (stats.IsDead) reason = "Cannot cast while dead.";
            else if (GetComponent<MeleeAttack>()?.IsBusy == true || GetComponent<CombatDefense>()?.ActionsBlocked == true
                || GetComponent<NetworkPlayerDodge>()?.Busy == true || GetComponent<HeroesOfDragonland.Player.PlayerDodge>()?.IsBusy == true)
                reason = "Finish your current action first.";
            else if (!ValidTarget(definition, target)) reason = "Requires a living ally or self within range and line of sight.";
            else if (stats.CurrentMana < ManaCost(definition)) reason = ManaRequired(stats.CurrentMana, ManaCost(definition));
            return reason.Length == 0;
        }
        internal static string ManaRequired(float current, float required) =>
            "Not enough mana · Need " + required.ToString("0.##") + ", have " + current.ToString("0.##") + ".";
        public bool TryStart(AbilityDefinition definition, CharacterStats target, out string reason)
        {
            reason = "Casting requires authority.";
            if (!HasAuthority || !CanStart(definition, target, out reason)) return false;
            if (session != skills.SessionId) { Clear(); session = skills.SessionId; }
            if (!stats.TrySpendMana(ManaCost(definition))) { reason = ManaRequired(stats.CurrentMana, ManaCost(definition)); return false; }
            casting = definition; castTarget = target != null ? target : stats;
            castOrigin = transform.position; castDirection = transform.forward.normalized; CastRemaining = definition.CastTime;
            Cue(definition, AbilityCue.CastStarted, transform.position, castDirection);
            if (CastRemaining == 0) Release();
            return true;
        }
        private bool SameWorld(CharacterStats target)
        {
            var own = GetComponent<NetworkObject>(); var other = target.GetComponent<NetworkObject>();
            return own == null ? other == null : other != null && other.IsValid && own.IsValid && other.Runner == own.Runner;
        }
        private bool ValidTarget(AbilityDefinition definition, CharacterStats target)
        {
            if (definition.Target == AbilityTarget.Forward) return target == null;
            target = target != null ? target : stats;
            if (!target.isActiveAndEnabled || target.IsDead || !SameWorld(target)) return false;
            if (definition.Target == AbilityTarget.Self) return target == stats;
            return (target == stats || TeamIdentity.AreAllies(TeamIdentity.Find(gameObject), TeamIdentity.Find(target.gameObject)))
                && Vector3.Distance(transform.position, target.transform.position) <= definition.Range && HasLineOfSight(target);
        }
        private bool HasLineOfSight(CharacterStats target)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 delta = target.transform.position + Vector3.up - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target.transform)) return false;
            return true;
        }
        // Server chooses the nearest injured ally in front; self-heal is the fallback.
        public CharacterStats SelectHealingTarget(AbilityDefinition definition)
        {
            CharacterStats best = null; float distance = definition.Range;
            foreach (var candidate in FindObjectsByType<CharacterStats>())
            {
                if (candidate == stats || candidate.CurrentHealth >= candidate.MaxHealth || !ValidTarget(definition, candidate)) continue;
                Vector3 delta = candidate.transform.position - transform.position;
                if (Vector3.Dot(transform.forward, delta.normalized) < .5f || delta.magnitude >= distance) continue;
                best = candidate; distance = delta.magnitude;
            }
            return best != null ? best : stats;
        }
        public void Tick(float deltaTime)
        {
            if (!HasAuthority || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            if (session != skills.SessionId) { Clear(); session = skills.SessionId; }
            if (stats.IsDead) { Clear(); return; }
            if (ShieldRemaining > 0)
            {
                ShieldRemaining = Mathf.Max(0, ShieldRemaining - deltaTime);
                if (ShieldRemaining == 0) EndShield();
            }
            // Existing projectiles advance before releases, so a new projectile starts at its launch point.
            for (int i = projectiles.Count - 1; i >= 0; i--) if (Step(projectiles[i], deltaTime)) projectiles.RemoveAt(i);
            if (casting == null) return;
            bool interrupted = Has(CastInterruption.Movement) && (transform.position - castOrigin).sqrMagnitude > .01f
                || Has(CastInterruption.Defense) && GetComponent<CombatDefense>()?.ActionsBlocked == true
                || Has(CastInterruption.Dodge) && (GetComponent<NetworkPlayerDodge>()?.Busy == true || GetComponent<HeroesOfDragonland.Player.PlayerDodge>()?.IsBusy == true);
            if (interrupted || !ValidTarget(casting, casting.Target == AbilityTarget.Forward ? null : castTarget)) { Interrupt(); return; }
            CastRemaining = Mathf.Max(0, CastRemaining - deltaTime);
            if (CastRemaining == 0) Release();
        }
        private void Release()
        {
            var definition = casting; var target = castTarget; casting = null; CastRemaining = 0;
            Vector3 origin = transform.position + Vector3.up;
            Cue(definition, AbilityCue.Released, origin, castDirection);
            switch (definition.Effect)
            {
                case AbilityEffect.FireProjectile:
                    projectiles.Add(new Projectile { Definition = definition, Position = origin, Direction = castDirection, Remaining = definition.Range }); break;
                case AbilityEffect.Heal:
                    float restored = target.Heal(definition.Magnitude);
                    if (restored > 0f) HeroesOfDragonland.Multiplayer.MatchSessionData.Credit(this,
                        new HeroesOfDragonland.Multiplayer.MatchStatistics { Healing = restored });
                    Cue(definition, AbilityCue.Impact, target.transform.position + Vector3.up, Vector3.up); break;
                case AbilityEffect.Shield:
                    if (ShieldRemaining > 0) EndShield();
                    shieldDefinition = definition; ShieldCapacity = definition.Magnitude; ShieldRemaining = definition.Duration; break;
            }
        }
        private bool Step(Projectile projectile, float dt)
        {
            float step = Mathf.Min(projectile.Remaining, projectile.Definition.ProjectileSpeed * dt);
            var hits = Physics.SphereCastAll(projectile.Position, projectile.Definition.ProjectileRadius, projectile.Direction, step, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                var receiver = hit.collider.GetComponentInParent<DamageReceiver>();
                bool confirmed = false;
                if (receiver != null)
                {
                    if (TeamIdentity.AreAllies(TeamIdentity.Find(gameObject), TeamIdentity.Find(receiver.gameObject))) continue;
                    IsResolvingProjectile = true;
                    try { receiver.TakeDamage(projectile.Definition.Magnitude, gameObject, "Fire Projectile",
                        new DamageImpact(ImpactWeapon.Magic, hit.point, projectile.Direction), out confirmed); }
                    finally { IsResolvingProjectile = false; }
                }
                bool networkContact = confirmed && receiver.GetComponent<HeroesOfDragonland.Presentation.Vfx.NetworkImpactVfx>() != null;
                Cue(projectile.Definition, networkContact ? AbilityCue.ConfirmedImpact : AbilityCue.Impact,
                    projectile.Position + projectile.Direction * hit.distance, projectile.Direction);
                return true;
            }
            projectile.Position += projectile.Direction * step; projectile.Remaining -= step;
            return projectile.Remaining <= 0;
        }
        public float Absorb(float amount)
        {
            if (!HasAuthority || ShieldRemaining <= 0 || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return amount;
            float absorbed = Mathf.Min(ShieldCapacity, amount); ShieldCapacity -= absorbed;
            if (ShieldCapacity <= 0) EndShield();
            return amount - absorbed;
        }
        private void EndShield()
        {
            var definition = shieldDefinition; ShieldCapacity = ShieldRemaining = 0; shieldDefinition = null;
            if (definition != null) Cue(definition, AbilityCue.ShieldEnded, transform.position, Vector3.up);
        }
        public void Interrupt()
        {
            if (casting == null) return;
            var definition = casting; casting = null; CastRemaining = 0;
            Cue(definition, AbilityCue.Interrupted, transform.position, castDirection);
        }
        internal void Clear() { Interrupt(); EndShield(); projectiles.Clear(); }
        internal void ApplySnapshot(float cast, float shield, float capacity)
        { if (!HasAuthority) { CastRemaining = cast; ShieldRemaining = shield; ShieldCapacity = capacity; } }
        internal void Cue(AbilityDefinition definition, AbilityCue cue, Vector3 position, Vector3 direction)
        {
            if (Presentation == null) return;
            foreach (Action<AbilityDefinition, AbilityCue, Vector3, Vector3> listener in Presentation.GetInvocationList())
                try { listener(definition, cue, position, direction); } catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
