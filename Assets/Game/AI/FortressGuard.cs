using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    // EnemyBrain owns patrol, leash, loss timeout, return, death and attack timing.
    // Its spawn position anchors the guard area; EnemyDefinition configures its radii.
    [DisallowMultipleComponent, RequireComponent(typeof(TeamIdentity), typeof(EnemyBrain))]
    public sealed class FortressGuard : MonoBehaviour, IEnemyTargetPolicy
    {
        public bool AllowsTarget(EnemyThreatTarget target)
        {
            if (!isActiveAndEnabled || target == null) return false;
            var identity = TeamIdentity.Find(target.gameObject);
            return identity != null && identity.Kind == CombatantKind.Player
                && TeamIdentity.AreEnemies(GetComponent<TeamIdentity>(), identity);
        }
    }
}
