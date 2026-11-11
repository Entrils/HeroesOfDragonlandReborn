using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    // Live hit-resolution context. A client/source identity or a busy windup is insufficient.
    public interface IMeleeImpactSource
    {
        bool IsResolvingMeleeHit { get; }
        float PoiseDamage { get; }
        Vector3 ImpactOrigin { get; }
        void InterruptByParry(float duration);
    }
}
