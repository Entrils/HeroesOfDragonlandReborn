using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Combat
{
    // Only confirmed server damage enters this ledger. It never accepts client claims.
    public sealed class DamageContributionLedger
    {
        public readonly struct Contribution
        {
            public readonly NetworkObject Actor;
            public readonly float Damage, LastHit;
            public Contribution(NetworkObject actor, float damage, float lastHit) { Actor = actor; Damage = damage; LastHit = lastHit; }
        }
        private readonly Dictionary<NetworkId, Contribution> entries = new();
        public void Record(NetworkObject actor, float damage, float now)
        {
            if (!NetworkParticipant.IsAdmitted(actor) || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            entries.TryGetValue(actor.Id, out var previous);
            entries[actor.Id] = new Contribution(actor, previous.Damage + damage, now);
        }
        public List<Contribution> Recent(float now, float window, float minimumDamage)
        {
            var result = new List<Contribution>();
            foreach (var entry in entries.Values)
                if (NetworkParticipant.IsAdmitted(entry.Actor) && now - entry.LastHit <= window && entry.Damage >= minimumDamage) result.Add(entry);
            return result;
        }
        public void Clear() => entries.Clear();
        public static bool IsEnemyPlayer(NetworkObject actor, NetworkObject victim)
            => NetworkParticipant.IsAdmitted(actor) && actor != victim && actor.Runner == victim.Runner
            && actor.GetComponent<NetworkTestPlayer>() is { } player && victim.GetComponent<NetworkTestPlayer>() is { } target
            && player.Team != target.Team && (player.Team == TeamId.TeamA || player.Team == TeamId.TeamB);
    }
}
