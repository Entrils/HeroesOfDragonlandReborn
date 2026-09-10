using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(TeamIdentity))]
    public sealed class TeamDebug : MonoBehaviour
    {
        [ContextMenu("Teams/Join Team A")]
        private void JoinTeamA() => Join(TeamId.TeamA);
        [ContextMenu("Teams/Join Team B")]
        private void JoinTeamB() => Join(TeamId.TeamB);
        private void Join(TeamId team)
        {
            if (!Application.isPlaying || !(Application.isEditor || Debug.isDebugBuild)) return;
            var identity = GetComponent<TeamIdentity>();
            identity.TryConfigure(team, identity.Kind, identity.AllowFriendlyFire);
            Debug.Log(name + " joined " + team, this);
        }
    }
}
