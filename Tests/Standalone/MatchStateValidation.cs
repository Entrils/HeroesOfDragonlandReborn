using System;
using HeroesOfDragonland.Match;
using HeroesOfDragonland.Teams;

internal static class MatchStateValidation
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        checks++;
    }
    public static void Main()
    {
        foreach (var defeated in new[] { TeamId.TeamA, TeamId.TeamB })
        {
            var match = new MatchStateMachine();
            Check(match.State == MatchState.Waiting && match.Winner == TeamId.Unassigned, "waiting has no winner");
            Check(!match.TryFinish(defeated) && match.State == MatchState.Waiting, "waiting cannot finish");
            Check(match.TryStart() && match.State == MatchState.Playing, "start transition");
            Check(!match.TryStart(), "duplicate start rejected");
            Check(!match.TryFinish(TeamId.Unassigned) && !match.TryFinish((TeamId)99)
                && match.State == MatchState.Playing && match.Winner == TeamId.Unassigned, "invalid objective team leaves state unchanged");
            Check(match.TryFinish(defeated), "valid King death finishes");
            var winner = defeated == TeamId.TeamA ? TeamId.TeamB : TeamId.TeamA;
            Check(match.State == MatchState.Finished && match.Winner == winner, "opposing team wins");
            Check(!match.TryFinish(defeated) && !match.TryFinish(winner) && match.Winner == winner, "duplicate and second King death cannot change result");
            Check(!match.TryStart() && match.State == MatchState.Finished, "finished is terminal");
        }
        var first = new MatchStateMachine(); var second = new MatchStateMachine();
        first.TryStart(); first.TryFinish(TeamId.TeamB);
        Check(second.State == MatchState.Waiting && second.Winner == TeamId.Unassigned, "new matches do not inherit result");
        Console.WriteLine("PASS: " + checks + " match-state checks.");
    }
}
