using System;
using UnityEngine;

namespace HeroesOfDragonland.Teams
{
    [DisallowMultipleComponent]
    public sealed class TeamIdentity : MonoBehaviour
    {
        [SerializeField] private TeamId team;
        [SerializeField] private CombatantKind kind;
        [Tooltip("Allows this character's attacks to damage allies. Guard targeting still ignores allies.")]
        [SerializeField] private bool allowFriendlyFire;
        public TeamId Team => team;
        public CombatantKind Kind => kind;
        public bool HasTeam => team == TeamId.TeamA || team == TeamId.TeamB;
        public bool AllowFriendlyFire => allowFriendlyFire;
        public event Action Changed;

        // Match/spawn code supplies identity. Disabling this component does not bypass team rules.
        public bool TryConfigure(TeamId value, CombatantKind combatantKind, bool friendlyFire = false)
        {
            if (!Enum.IsDefined(typeof(TeamId), value) || !Enum.IsDefined(typeof(CombatantKind), combatantKind)) return false;
            if (team == value && kind == combatantKind && allowFriendlyFire == friendlyFire) return true;
            team = value; kind = combatantKind; allowFriendlyFire = friendlyFire;
            Changed?.Invoke();
            return true;
        }

        public static TeamIdentity Find(GameObject actor) => actor != null ? actor.GetComponentInParent<TeamIdentity>() : null;
        public static bool AreAllies(TeamIdentity a, TeamIdentity b)
            => a != null && b != null && a.HasTeam && b.HasTeam && a.Team == b.Team;
        public static bool AreEnemies(TeamIdentity a, TeamIdentity b)
            => a != null && b != null && a.HasTeam && b.HasTeam && a.Team != b.Team;
        public static bool CanDamage(GameObject source, GameObject target)
        {
            var attacker = Find(source);
            return !AreAllies(attacker, Find(target)) || attacker.AllowFriendlyFire;
        }
    }
}
