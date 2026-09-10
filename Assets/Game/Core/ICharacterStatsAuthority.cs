namespace HeroesOfDragonland.Stats
{
    // Optional authority boundary. Actors without a provider retain offline behavior.
    public interface ICharacterStatsAuthority
    {
        bool CanMutateStats { get; }
    }
}
