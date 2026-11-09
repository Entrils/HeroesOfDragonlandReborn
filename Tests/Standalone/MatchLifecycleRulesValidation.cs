using System;
using HeroesOfDragonland.Multiplayer;

internal static class MatchLifecycleRulesValidation
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        checks++;
    }
    public static void Main()
    {
        var allowed = new bool[6,6];
        allowed[0,1] = allowed[1,2] = allowed[2,3] = allowed[2,5] = true;
        allowed[3,4] = allowed[3,5] = allowed[4,5] = allowed[5,1] = true;
        for (int from = 0; from < 6; from++)
            for (int to = 0; to < 6; to++)
                Check(MatchLifecycleRules.CanTransition((MatchPhase)from, (MatchPhase)to) == allowed[from,to], "legal transition matrix");
        for (int minimum = 2; minimum <= 6; minimum++)
            for (int a = 0; a <= 3; a++)
                for (int b = 0; b <= 3; b++)
                    Check(MatchLifecycleRules.HasEnoughPlayers(a,b,minimum) == (a != 0 && b != 0 && a+b >= minimum), "roster readiness");
        var roster = new NetworkTeamRoster();
        for (int i = 1; i <= 6; i++)
        {
            Check(roster.TryAssign(i, out var before), "initial allocation");
            Check(roster.TryAssign(i, out var after) && before == after, "round recreation preserves allocation");
        }
        Check(roster.Count == 6 && roster.CountTeam(HeroesOfDragonland.Teams.TeamId.TeamA) == 3, "capacity unchanged after repeated assignment");
        Check(roster.Remove(2) && !roster.TryGetTeam(2, out _), "disconnect releases allocation");
        Console.WriteLine("PASS: " + checks + " lifecycle transitions, readiness and persistent-roster checks.");
    }
}
