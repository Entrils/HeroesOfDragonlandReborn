using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Match;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.World
{
    // Stationary objective; movement/AI can be composed separately later.
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    [RequireComponent(typeof(TeamIdentity), typeof(DamageReceiver), typeof(Targetable))]
    public sealed class KingObjective : MonoBehaviour, IDamagePolicy
    {
        [Tooltip("King health and resources. The battlefield uses a 1000-health profile.")]
        [SerializeField] private CharacterStatsConfig configuration;
        private CharacterStats stats;
        private MatchManager owner;
        private TeamId registeredTeam;
        private bool defeated;
        public TeamId Team => GetComponent<TeamIdentity>().Team;
        public bool IsDefeated => defeated || Stats.IsDead;
        public CharacterStats Stats => stats != null ? stats : GetComponent<CharacterStats>();
        public event Action<KingObjective> Defeated;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (stats != null) return;
            stats = GetComponent<CharacterStats>();
            if (configuration != null) stats.ConfigureForSpawn(configuration);
            // Keep this subscription while disabled/deactivated so a death is never lost.
            stats.Died += OnDied;
        }

        internal bool CanBind(MatchManager match, TeamId expectedTeam, out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled || !GetComponent<DamageReceiver>().enabled || !GetComponent<Targetable>().enabled)
                reason = "King, DamageReceiver and Targetable must be active.";
            else if (configuration == null || configuration.MaxHealth <= 0 || float.IsNaN(configuration.MaxHealth)
                || float.IsInfinity(configuration.MaxHealth)) reason = "Assign a valid King stats profile.";
            else if (Team != expectedTeam || GetComponent<TeamIdentity>().Kind != CombatantKind.NPC)
                reason = "Each King must be an NPC assigned to its expected team.";
            else if (owner != null && owner != match) reason = "King already belongs to another match.";
            if (reason != null) return false;
            Initialize();
            if (IsDefeated) { reason = "Cannot start with a defeated King."; return false; }
            return true;
        }

        internal void Bind(MatchManager match, TeamId team) { owner = match; registeredTeam = team; }
        internal void Unbind(MatchManager match) { if (owner == match) owner = null; }

        public bool AllowsDamage(GameObject source)
        {
            if (!isActiveAndEnabled || owner == null || Team != registeredTeam) return false;
            var attacker = TeamIdentity.Find(source);
            // Kings reject allies even if the attacker enables friendly fire, plus neutral/environmental damage.
            return attacker != null && attacker.HasTeam && attacker.Team != registeredTeam
                && owner.AllowsObjectiveDamage(this);
        }

        private void OnDied()
        {
            if (defeated) return;
            defeated = true;
            if (Defeated == null) return;
            foreach (Action<KingObjective> handler in Defeated.GetInvocationList())
                try { handler(this); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private void OnDestroy() { if (stats != null) stats.Died -= OnDied; }
    }
}
