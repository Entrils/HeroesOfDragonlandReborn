using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    // All attached policies must accept a hit before resources or events change.
    public interface IDamagePolicy
    {
        bool AllowsDamage(GameObject source);
    }
}
