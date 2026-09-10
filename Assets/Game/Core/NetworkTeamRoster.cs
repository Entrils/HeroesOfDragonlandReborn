using System.Collections.Generic;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Multiplayer
{
    // Server-owned connection roster. No client-selected team or persistent account identity.
    public sealed class NetworkTeamRoster
    {
        public const int TeamCapacity = 3;
        public const int PlayerCapacity = TeamCapacity * 2;
        private readonly Dictionary<int, TeamId> teams = new Dictionary<int, TeamId>();
        public int Count => teams.Count;

        public int CountTeam(TeamId team)
        {
            int count = 0;
            foreach (var value in teams.Values) if (value == team) count++;
            return count;
        }

        public bool TryAssign(int connectionId, out TeamId team)
        {
            if (teams.TryGetValue(connectionId, out team)) return true;
            team = TeamId.Unassigned;
            if (Count >= PlayerCapacity) return false;
            int a = CountTeam(TeamId.TeamA), b = CountTeam(TeamId.TeamB);
            team = a <= b && a < TeamCapacity ? TeamId.TeamA : TeamId.TeamB;
            if (CountTeam(team) >= TeamCapacity) { team = TeamId.Unassigned; return false; }
            teams.Add(connectionId, team);
            return true;
        }

        public bool Remove(int connectionId) => teams.Remove(connectionId);
        public void Clear() => teams.Clear();
    }
}
