using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World
{
    [DisallowMultipleComponent, RequireComponent(typeof(DamageReceiver), typeof(TeamIdentity), typeof(BoxCollider))]
    public sealed class FortressGate : MonoBehaviour, IDamagePolicy
    {
        [SerializeField] private TeamId team;
        [SerializeField] private Transform gateBody;
        private CharacterStats stats;
        private Collider[] blockers;
        private NavMeshObstacle obstacle;
        public TeamId Team => team;
        public bool IsOpen { get; private set; }
        private void Awake()
        {
            stats = GetComponent<CharacterStats>(); blockers = GetComponentsInChildren<Collider>();
            obstacle = GetComponent<NavMeshObstacle>();
            GetComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC, false);
        }
        private void OnEnable() => stats.Died += OnDeath;
        private void OnDisable() => stats.Died -= OnDeath;
        private void OnDeath() { if (stats.CanMutate) ApplyOpen(true); }
        // A gate never accepts friendly fire, including sources that opt into it.
        public bool AllowsDamage(GameObject source)
        {
            var identity = TeamIdentity.Find(source);
            return !IsOpen && identity != null && identity.HasTeam && identity.Team != team
                && (team == TeamId.TeamA || team == TeamId.TeamB);
        }
        internal void ApplyOpen(bool value)
        {
            if (!value || IsOpen) return; // One-way for this round's gate instance.
            IsOpen = true;
            foreach (var blocker in blockers) if (blocker != null) blocker.enabled = false;
            if (obstacle != null) obstacle.enabled = false;
            if (gateBody != null) gateBody.gameObject.SetActive(false);
        }
    }
}
