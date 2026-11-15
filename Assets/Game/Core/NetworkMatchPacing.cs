using Fusion;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkMatch))]
    public sealed class NetworkMatchPacing : NetworkBehaviour
    {
        [Networked] public float TeamAAverageLevel { get; private set; }
        [Networked] public float TeamBAverageLevel { get; private set; }
        private NetworkMatch match;
        private void Awake() => match = GetComponent<NetworkMatch>();
        public static NetworkMatch MatchFor(NetworkRunner runner) => runner != null ? runner.GetComponentInParent<NetworkFortressSpawner>()?.Match : null;
        public static MatchBalanceConfig BalanceFor(NetworkRunner runner) => MatchFor(runner)?.Balance;
        public override void FixedUpdateNetwork()
        {
            if (!match.IsServerOwner) return;
            float a = 0, b = 0; int countA = 0, countB = 0;
            foreach (var actor in Runner.GetComponentInParent<MultiplayerBootstrap>().Avatars)
            {
                if (!NetworkParticipant.IsAdmitted(actor)) continue;
                var progression = actor.GetComponent<SessionProgression>();
                if (progression == null || !progression.IsInitialized) continue;
                var team = actor.GetComponent<NetworkTestPlayer>().Team;
                if (team == TeamId.TeamA) { a += progression.Level; countA++; }
                if (team == TeamId.TeamB) { b += progression.Level; countB++; }
            }
            TeamAAverageLevel = countA > 0 ? a / countA : 0;
            TeamBAverageLevel = countB > 0 ? b / countB : 0;
        }
        public float CampMultiplier(TeamId team)
        {
            if (match.Balance == null || TeamAAverageLevel <= 0 || TeamBAverageLevel <= 0) return 1;
            return team == TeamId.TeamA ? match.Balance.ComebackMultiplier(TeamAAverageLevel, TeamBAverageLevel)
                : team == TeamId.TeamB ? match.Balance.ComebackMultiplier(TeamBAverageLevel, TeamAAverageLevel) : 1;
        }
    }
}
