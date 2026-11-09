namespace HeroesOfDragonland.Multiplayer
{
    public enum MatchPhase { Boot, Lobby, Starting, Playing, Finished, ReturningToLobby }

    // Shared by the server coordinator and deterministic transition checks.
    public static class MatchLifecycleRules
    {
        public static bool CanTransition(MatchPhase from, MatchPhase to) => from switch
        {
            MatchPhase.Boot => to == MatchPhase.Lobby,
            MatchPhase.Lobby => to == MatchPhase.Starting,
            MatchPhase.Starting => to == MatchPhase.Playing || to == MatchPhase.ReturningToLobby,
            MatchPhase.Playing => to == MatchPhase.Finished || to == MatchPhase.ReturningToLobby,
            MatchPhase.Finished => to == MatchPhase.ReturningToLobby,
            MatchPhase.ReturningToLobby => to == MatchPhase.Lobby,
            _ => false
        };

        public static bool HasEnoughPlayers(int teamA, int teamB, int minimum)
            => teamA > 0 && teamB > 0 && teamA + teamB >= minimum;
    }
}
