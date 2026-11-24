using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Combat
{
    // Camp team rewards use the same per-hit time window as kill attribution.
    public sealed class DamageContributionLedger
    {
        private readonly CombatContributionHistory<NetworkObject> history = new();

        public void Record(NetworkObject actor, float damage, double now, float window)
        {
            if (NetworkParticipant.IsAdmitted(actor)) history.Record(actor, damage, now, window);
        }

        public List<CombatContributionHistory<NetworkObject>.Contribution> Recent(double now, float window, float minimumDamage)
            => history.Recent(now, window, minimumDamage, NetworkParticipant.IsAdmitted);

        public void Clear() => history.Clear();

        public static bool IsEnemyPlayer(NetworkObject actor, NetworkObject victim)
            => NetworkParticipant.IsAdmitted(actor) && actor != victim && actor.Runner == victim.Runner
            && actor.TryGetComponent<NetworkTestPlayer>(out var player) && victim.TryGetComponent<NetworkTestPlayer>(out var target)
            && player.Team != target.Team && (player.Team == TeamId.TeamA || player.Team == TeamId.TeamB);
    }
}
