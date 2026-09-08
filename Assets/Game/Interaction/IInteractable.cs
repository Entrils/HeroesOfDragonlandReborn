using HeroesOfDragonland.InventorySystem;
using UnityEngine;

namespace HeroesOfDragonland.Interaction
{
    public interface IInteractable
    {
        Vector3 InteractionPoint { get; }
        bool TryInteract(Inventory player, out string message);
    }
}
