using System;

namespace HeroesOfDragonland.Multiplayer
{
    [Serializable]
    public struct RankedRatingSettings
    {
        [UnityEngine.SerializeField] private int startingRating, winPoints, lossPoints, provisionalMatches;
        public RankedRatingSettings(int startingRating, int winPoints, int lossPoints, int provisionalMatches)
        {
            this.startingRating = startingRating;
            this.winPoints = winPoints;
            this.lossPoints = lossPoints;
            this.provisionalMatches = provisionalMatches;
        }

        public static RankedRatingSettings Default => new(1000, 25, 25, 5);
        public int StartingRating => startingRating;
        public int WinPoints => winPoints;
        public int LossPoints => lossPoints;
        public int ProvisionalMatches => provisionalMatches;
        public bool IsValid => startingRating >= 0 && winPoints >= 0 && lossPoints >= 0 && provisionalMatches >= 1;
    }

    [Serializable]
    public struct RankedPlayerData
    {
        public int Rating;
        public bool Provisional;
        public long MatchesPlayed, Wins, Losses;
        public static RankedPlayerData New => new() { Rating = RankedRatingSettings.Default.StartingRating, Provisional = true };
    }

    public interface IRankedRatingModel
    {
        RankedPlayerData Apply(RankedPlayerData previous, bool won, RankedRatingSettings settings);
    }

    public sealed class FixedStepRankedRating : IRankedRatingModel
    {
        public RankedPlayerData Apply(RankedPlayerData previous, bool won, RankedRatingSettings settings)
        {
            long rating = previous.MatchesPlayed == 0 ? settings.StartingRating : previous.Rating;
            rating += won ? settings.WinPoints : -settings.LossPoints;
            previous.Rating = (int)Math.Clamp(rating, 0, int.MaxValue);
            if (previous.MatchesPlayed < long.MaxValue) previous.MatchesPlayed++;
            if (won && previous.Wins < long.MaxValue) previous.Wins++;
            if (!won && previous.Losses < long.MaxValue) previous.Losses++;
            previous.Provisional = previous.MatchesPlayed < settings.ProvisionalMatches;
            return previous;
        }
    }

    public static class RankedMatchRules
    {
        public static bool IsRated(bool rankedQueue, bool completed, int readyHumanSlotsAtStart,
            int teamAHumansAtFinish, int teamBHumansAtFinish)
            => rankedQueue && completed && readyHumanSlotsAtStart == NetworkTeamRoster.PlayerCapacity
                && teamAHumansAtFinish == NetworkTeamRoster.TeamCapacity && teamBHumansAtFinish == NetworkTeamRoster.TeamCapacity;
    }
}
