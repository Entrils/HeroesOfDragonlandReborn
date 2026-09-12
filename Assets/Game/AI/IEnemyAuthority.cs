using UnityEngine;

namespace HeroesOfDragonland.AI
{
    public interface IEnemyAuthority
    {
        bool CanSimulateEnemy { get; }
    }

    internal static class EnemyAuthority
    {
        // Offline actors retain their manual test/brain controls. Network actors fail closed.
        internal static bool CanSimulate(Component actor)
            => actor.GetComponent<IEnemyAuthority>()?.CanSimulateEnemy ?? true;
    }
}
