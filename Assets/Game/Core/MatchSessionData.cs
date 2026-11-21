using System.Collections.Generic;
using Fusion;

namespace HeroesOfDragonland.Multiplayer
{
    // Authority-owned transient counters, scoped to one round and keyed by its avatar.
    internal sealed class MatchSessionData
    {
        internal struct Counters
        {
            internal int Kills, Deaths, Camps, Kings, Bosses;
        }
        internal enum Metric { Kill, Death, Camp, King, Boss }
        private readonly Dictionary<NetworkId, Counters> players = new();
        internal string RoundId { get; private set; }
        internal void Begin(string id) { players.Clear(); RoundId = id; }
        internal Counters For(NetworkId actor) => players.TryGetValue(actor, out var counters) ? counters : default;
        internal void Add(NetworkId actor, Metric metric)
        {
            var counters = For(actor);
            switch (metric)
            {
                case Metric.Kill: counters.Kills++; break;
                case Metric.Death: counters.Deaths++; break;
                case Metric.Camp: counters.Camps++; break;
                case Metric.King: counters.Kings++; break;
                case Metric.Boss: counters.Bosses++; break;
            }
            players[actor] = counters;
        }

        internal static void Credit(NetworkObject avatar, Metric metric)
        {
            if (avatar != null && avatar.IsValid && avatar.Runner.IsServer)
                NetworkMatchPacing.MatchFor(avatar.Runner)?.CreditProfile(avatar, metric);
        }
    }
}
