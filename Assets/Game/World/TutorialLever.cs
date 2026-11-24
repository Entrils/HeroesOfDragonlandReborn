using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.InventorySystem;
using UnityEngine;

namespace HeroesOfDragonland.World
{
    public sealed class TutorialLever : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform handle;
        private const float PullDuration = .5f;
        private Quaternion readyRotation;
        private float pulledAt;
        public bool Used { get; private set; }
        public bool PullComplete => Used && (handle == null || Time.time >= pulledAt + PullDuration);
        public Vector3 InteractionPoint => transform.position;

        private void Awake()
        {
            if (handle != null) readyRotation = handle.localRotation;
        }

        private void Update()
        {
            if (!Used || handle == null) return;
            float progress = Mathf.Clamp01((Time.time - pulledAt) / PullDuration);
            handle.localRotation = Quaternion.AngleAxis(Mathf.SmoothStep(0, 69, progress), Vector3.right)
                * readyRotation;
        }

        public bool TryInteract(Inventory player, out string message)
        {
            if (!Used && player != null)
            {
                Used = true;
                pulledAt = Time.time;
            }
            message = Used ? "Training chest unlocked." : "Move closer to the lever.";
            return Used;
        }
    }
}
