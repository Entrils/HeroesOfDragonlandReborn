using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkEnemy), typeof(TeamIdentity))]
    public sealed class NetworkFortressGuard : MonoBehaviour, IEnemyTargetPolicy, IDamagePolicy
    {
        [SerializeField] private TeamId team;
        public TeamId Team => team;
        private void Awake() => GetComponent<TeamIdentity>().TryConfigure(team, CombatantKind.NPC, false);
        public bool IsEnemy(NetworkTestPlayer player)
            => player != null && (team == TeamId.TeamA || team == TeamId.TeamB)
                && (player.Team == TeamId.TeamA || player.Team == TeamId.TeamB) && player.Team != team;
        public bool AllowsTarget(EnemyThreatTarget target)
            => isActiveAndEnabled && target != null && IsEnemy(target.GetComponent<NetworkTestPlayer>());
        public bool AllowsDamage(GameObject source)
            => isActiveAndEnabled && source != null && IsEnemy(source.GetComponent<NetworkTestPlayer>());
    }
}
