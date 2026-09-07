using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        float TakeDamage(float amount, GameObject source);
    }
}
