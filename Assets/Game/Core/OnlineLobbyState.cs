using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Multiplayer
{
    // Server-side lobby intent. Transport owns connections and the team roster;
    // readiness is invalidated independently whenever lobby membership changes.
    internal sealed class OnlineLobbyState
    {
        private readonly HashSet<PlayerRef> ready = new();
        private readonly HashSet<PlayerRef> observed = new();
        private int bots = -1;
        internal void Observe(IEnumerable<PlayerRef> connections, int botCount)
        {
            if (observed.SetEquals(connections) && bots == botCount) return;
            observed.Clear();
            observed.UnionWith(connections);
            bots = botCount;
            ClearReady();
        }
        internal bool IsReady(PlayerRef player) => ready.Contains(player);
        internal void SetReady(PlayerRef player, bool value)
        {
            if (value) ready.Add(player);
            else ready.Remove(player);
        }
        internal void ClearReady() => ready.Clear();
    }

    public readonly struct OnlineMatchResult
    {
        public string Room { get; }
        public int Round { get; }
        public TeamId Winner { get; }
        public float PlayingSeconds { get; }
        internal OnlineMatchResult(string room, int round, TeamId winner, float playingSeconds)
        { Room = room; Round = round; Winner = winner; PlayingSeconds = playingSeconds; }
    }
}
