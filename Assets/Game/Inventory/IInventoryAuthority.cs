namespace HeroesOfDragonland.InventorySystem
{
    // Installed network adapters override the offline Inspector flag.
    public interface IInventoryAuthority
    {
        bool CanMutateInventory { get; }
    }
}
