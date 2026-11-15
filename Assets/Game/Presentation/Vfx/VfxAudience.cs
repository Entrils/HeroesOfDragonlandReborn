using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    public static class VfxAudience
    {
        public static VfxPool Pool(NetworkRunner runner) => runner != null
            ? runner.GetComponentInParent<MultiplayerBootstrap>()?.GetComponent<VfxPool>() : null;
        public static TeamId Team(GameObject source)
        {
            if (source == null) return TeamId.Unassigned;
            var player = source.GetComponent<NetworkTestPlayer>();
            if (player != null && player.Object != null && player.Object.IsValid) return player.Team;
            var king = source.GetComponent<NetworkKingObjective>();
            if (king != null && king.Object != null && king.Object.IsValid) return king.Team;
            return TeamIdentity.Find(source)?.Team ?? TeamId.Unassigned;
        }
        public static VfxRelation Relation(NetworkRunner runner, TeamId sourceTeam, bool damaging = false, bool friendlyFire = false)
        {
            if (damaging && friendlyFire) return VfxRelation.Hostile;
            if (runner != null && runner.LocalPlayer != PlayerRef.None && runner.TryGetPlayerObject(runner.LocalPlayer, out var local) && local != null)
            {
                var team = Team(local.gameObject);
                if (team != TeamId.Unassigned && sourceTeam != TeamId.Unassigned)
                    return team == sourceTeam ? VfxRelation.Friendly : VfxRelation.Hostile;
            }
            return damaging ? VfxRelation.Hostile : VfxRelation.Neutral;
        }
    }
}
