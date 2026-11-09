using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkCharacterStats), typeof(DamageReceiver), typeof(TeamIdentity))]
    public sealed class NetworkKingObjective : NetworkBehaviour, IDamagePolicy
    {
        [SerializeField] private TeamId team;
        [Networked] public NetworkId MatchId { get; private set; }
        public TeamId Team => team;
        public CharacterStats Stats { get; private set; }
        private void Awake() => Stats = GetComponent<CharacterStats>();
        internal void Initialize(NetworkId matchId) { if (Runner.IsServer) MatchId = matchId; }
        public override void Spawned()
        {
            GetComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC, false);
            if (Runner.IsServer && HasStateAuthority) Stats.Died += OnDeath;
        }
        private NetworkMatch Match => Runner != null && Runner.TryFindObject(MatchId, out var obj) ? obj.GetComponent<NetworkMatch>() : null;
        public bool AllowsDamage(GameObject source)
        {
            if (!isActiveAndEnabled || Object == null || !Object.IsValid || !Runner.IsServer || !HasStateAuthority
                || Object.InputAuthority != PlayerRef.None || Stats.IsDead || source == null) return false;
            var match = Match;
            var attacker = source.GetComponent<NetworkPlayerCombat>();
            var player = source.GetComponent<NetworkTestPlayer>();
            return match != null && match.Owns(this) && match.CanPlay
                && attacker != null && attacker.Runner == Runner && attacker.IsAdmitted() && attacker.ResolvingAttack
                && !source.GetComponent<CharacterStats>().IsDead && player != null
                && (player.Team == TeamId.TeamA || player.Team == TeamId.TeamB) && player.Team != team;
        }
        private void OnDeath() => Match?.TryFinish(this);
        public override void Despawned(NetworkRunner runner, bool hasState) => Stats.Died -= OnDeath;
    }
}
