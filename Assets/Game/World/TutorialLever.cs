using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.InventorySystem;
using UnityEngine;

namespace HeroesOfDragonland.World
{
    public sealed class TutorialLever : MonoBehaviour, IInteractable
    {
        public bool Used { get; private set; }
        public Vector3 InteractionPoint => transform.position;
        public bool TryInteract(Inventory player, out string message)
        {
            Used = player != null || Used;
            message = Used ? "Training chest unlocked." : "Move closer to the lever.";
            return Used;
        }
    }
}
