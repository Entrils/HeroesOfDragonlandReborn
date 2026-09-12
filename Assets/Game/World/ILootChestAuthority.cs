using HeroesOfDragonland.InventorySystem;

namespace HeroesOfDragonland.World
{
    public interface ILootChestAuthority
    {
        bool CanMutateChest { get; }
        bool AllowsClaimant(Inventory inventory);
    }
}
