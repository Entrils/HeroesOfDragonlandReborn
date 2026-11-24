using System;
using HeroesOfDragonland.Multiplayer;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var settings = new RankedRatingSettings(100, 10, 30, 2);
        IRankedRatingModel model = new FixedStepRankedRating();
        var rank = model.Apply(RankedPlayerData.New, true, settings);
        if (rank.Rating != 110 || rank.Wins != 1 || rank.MatchesPlayed != 1 || !rank.Provisional)
            throw new InvalidOperationException("First ranked win failed.");
        rank = model.Apply(rank, false, settings);
        if (rank.Rating != 80 || rank.Losses != 1 || rank.MatchesPlayed != 2 || rank.Provisional)
            throw new InvalidOperationException("Provisional exit after ranked loss failed.");
        rank.Rating = 5;
        if (model.Apply(rank, false, settings).Rating != 0)
            throw new InvalidOperationException("Rating floor failed.");
        if (!RankedMatchRules.IsRated(true, true, 6, 3, 3)
            || RankedMatchRules.IsRated(false, true, 6, 3, 3)
            || RankedMatchRules.IsRated(true, false, 6, 3, 3)
            || RankedMatchRules.IsRated(true, true, 5, 3, 3)
            || RankedMatchRules.IsRated(true, true, 6, 2, 3))
            throw new InvalidOperationException("Ranked eligibility accepted an invalid result.");
        result.Log("PASS: fixed-step rating, floor, provisional status, and completed full-human ranked eligibility.");
    }
}
