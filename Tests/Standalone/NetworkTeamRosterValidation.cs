using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;

internal static class NetworkTeamRosterValidation
{
    private static int checks;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        checks++;
    }
    public static void Main()
    {
        // Every departure slot, followed by replacement and duplicate callbacks.
        for (int departure = 1; departure <= 6; departure++)
        {
            var roster = new NetworkTeamRoster();
            Check(roster.Count == 0, "dedicated server consumes no player slot");
            TeamId original = TeamId.Unassigned;
            for (int id = 1; id <= 6; id++)
            {
                Check(roster.TryAssign(id, out var team), "admit six unique connections");
                if (id == departure) original = team;
                Check(team == (id % 2 == 1 ? TeamId.TeamA : TeamId.TeamB), "server balances teams");
                Check(roster.TryAssign(id, out var again) && again == team && roster.Count == id,
                    "duplicate callback does not allocate twice");
            }
            Check(roster.CountTeam(TeamId.TeamA) == 3 && roster.CountTeam(TeamId.TeamB) == 3, "3v3 cap");
            Check(!roster.TryAssign(7, out var rejected) && rejected == TeamId.Unassigned, "seventh rejected");
            Check(roster.Remove(departure) && !roster.Remove(departure), "disconnect idempotent");
            Check(roster.TryAssign(7, out var replacement) && replacement == original, "vacated team filled");
            Check(roster.Count == 6 && roster.CountTeam(original) == 3, "replacement respects capacity");
            roster.Clear();
            Check(roster.Count == 0 && roster.TryAssign(100, out var fresh) && fresh == TeamId.TeamA,
                "new session starts clean");
        }
        Console.WriteLine("PASS: " + checks + " roster checks. No transport/runtime claim.");
    }
}
