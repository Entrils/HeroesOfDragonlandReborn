using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Multiplayer
{
    // Authority-owned transient counters keyed by the round avatar, which survives a reconnect.
    internal sealed class MatchSessionData
    {
        private readonly Dictionary<NetworkId, MatchPlayerResult> players = new();
        internal string RoundId { get; private set; }
        internal void Begin(string id) { players.Clear(); RoundId = id; }

        internal void Include(NetworkObject avatar)
        {
            if (avatar == null || !avatar.IsValid || players.ContainsKey(avatar.Id)
                || !avatar.TryGetComponent<NetworkTestPlayer>(out var actor)
                || actor.ParticipantNumber <= 0 || actor.Team is not (TeamId.TeamA or TeamId.TeamB)) return;
            players.Add(avatar.Id, new MatchPlayerResult
            {
                ParticipantNumber = actor.ParticipantNumber,
                Team = actor.Team,
                IsBot = actor.IsBot,
                Statistics = new MatchStatistics { HighestLevel = 1 }
            });
        }

        internal MatchPlayerResult For(NetworkId actor) => players.TryGetValue(actor, out var row) ? row : default;
        internal MatchPlayerResult[] Snapshot() => players.Values.OrderBy(row => row.Team).ThenBy(row => row.ParticipantNumber).ToArray();

        internal void Add(NetworkObject avatar, MatchStatistics delta)
        {
            Include(avatar);
            if (!players.TryGetValue(avatar.Id, out var row)) return;
            row.Statistics.Add(delta);
            players[avatar.Id] = row;
        }

        internal static void Credit(NetworkObject avatar, MatchStatistics delta)
        {
            if (avatar != null && avatar.IsValid && avatar.Runner.IsServer)
                NetworkMatchPacing.MatchFor(avatar.Runner)?.CreditMatch(avatar, delta);
        }

        internal static void Credit(UnityEngine.Component avatar, MatchStatistics delta)
        {
            if (avatar.TryGetComponent<NetworkObject>(out var actor)) Credit(actor, delta);
        }
    }
}
