namespace HeroesOfDragonland.AI
{
    public interface IEnemyTargetPolicy
    {
        bool AllowsTarget(EnemyThreatTarget target);
    }
}
