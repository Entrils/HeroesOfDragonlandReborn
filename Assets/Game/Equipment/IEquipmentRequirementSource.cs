using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.EquipmentSystem
{
    // Requirements use innate base plus allocated session points, excluding gear.
    public interface IEquipmentRequirementSource
    {
        bool TryGetBaseValue(ItemStat stat, out float value);
    }
}
