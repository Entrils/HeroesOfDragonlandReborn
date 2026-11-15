using Fusion;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject), typeof(NetworkCharacterStats), typeof(FortressGate))]
    public sealed class NetworkFortressGate : NetworkBehaviour, IDamagePolicy
    {
        [Networked] public NetworkId MatchId { get; private set; }
        [Networked] public NetworkBool IsOpen { get; private set; }
        [Networked] public int DestructionCount { get; private set; }
        private FortressGate gate;
        private CharacterStats stats;
        public TeamId Team => gate.Team;
        public bool CanMutateGate => isActiveAndEnabled && Object != null && Object.IsValid && Runner.IsServer
            && HasStateAuthority && Object.InputAuthority == PlayerRef.None && NetworkMatch.AllowsGameplay(Runner)
            && Runner.TryFindObject(MatchId, out var match) && match.GetComponent<NetworkMatch>() != null
            && Runner.GetComponentInParent<FortressAssaultSpawner>() is { } owner && owner.Owns(this);
        private void Awake() { gate = GetComponent<FortressGate>(); stats = GetComponent<CharacterStats>(); }
        internal void Initialize(NetworkId match) { if (Runner.IsServer) MatchId = match; }
        public override void Spawned()
        {
            if (HasStateAuthority && Runner.IsServer) stats.Died += OnDeath;
            else if (IsOpen) gate.ApplyOpen(true);
        }
        public bool AllowsDamage(GameObject source)
        {
            if (!CanMutateGate || IsOpen || stats.IsDead || source == null) return false;
            var combat = source.GetComponent<NetworkPlayerCombat>(); var player = source.GetComponent<NetworkTestPlayer>();
            var health = source.GetComponent<CharacterStats>();
            return player != null && player.Runner == Runner && player.Team != Team
                && (player.Team == TeamId.TeamA || player.Team == TeamId.TeamB)
                && health != null && !health.IsDead && combat != null && combat.IsAdmitted()
                && (combat.ResolvingAttack || source.GetComponent<NetworkAbilities>()?.AllowsProjectileDamage(Runner) == true);
        }
        private void OnDeath()
        {
            if (!CanMutateGate || IsOpen || !stats.IsDead) return;
            IsOpen = true; DestructionCount = 1; gate.ApplyOpen(true);
        }
        public override void Render() { if (IsOpen) gate.ApplyOpen(true); }
        public override void Despawned(NetworkRunner runner, bool hasState) => stats.Died -= OnDeath;
    }
}
