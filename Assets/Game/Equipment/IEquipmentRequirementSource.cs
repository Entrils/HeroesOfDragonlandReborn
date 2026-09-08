using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.EquipmentSystem
{
    // Future progression systems can supply attributes without changing equipment.
    // Requirements deliberately use base values, never bonuses from gear itself.
    public interface IEquipmentRequirementSource
    {
        bool TryGetBaseValue(ItemStat stat, out float value);
    }
}
