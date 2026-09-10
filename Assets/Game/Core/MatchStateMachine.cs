using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Match
{
    public enum MatchState { Waiting = 0, Playing = 1, Finished = 2 }

    // Pure session state. A new match gets a new instance; Finished is terminal.
    public sealed class MatchStateMachine
    {
        public MatchState State { get; private set; } = MatchState.Waiting;
        public TeamId Winner { get; private set; } = TeamId.Unassigned;

        public bool TryStart()
        {
            if (State != MatchState.Waiting) return false;
            State = MatchState.Playing;
            return true;
        }

        public bool TryFinish(TeamId defeatedTeam)
        {
            if (State != MatchState.Playing || (defeatedTeam != TeamId.TeamA && defeatedTeam != TeamId.TeamB)) return false;
            Winner = defeatedTeam == TeamId.TeamA ? TeamId.TeamB : TeamId.TeamA;
            State = MatchState.Finished;
            return true;
        }
    }
}
