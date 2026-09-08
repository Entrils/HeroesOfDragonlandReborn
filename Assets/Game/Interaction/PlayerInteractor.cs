using HeroesOfDragonland.InventorySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Interaction
{
    [DisallowMultipleComponent, RequireComponent(typeof(Inventory))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float range = 3f;
        [SerializeField] private InputAction interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
        private void Awake() => interact.AddBinding("<Gamepad>/buttonNorth");
        private void OnEnable() => interact.Enable();
        private void OnDisable() => interact.Disable();
        private void OnDestroy() => interact.Dispose();
        public void Tick(bool acceptInput)
        {
            if (isActiveAndEnabled && acceptInput && interact.WasPressedThisFrame())
            {
                TryInteract(out string message);
                Debug.Log(message, this);
            }
        }
        public bool TryInteract(out string message)
        {
            IInteractable nearest = null;
            float best = float.PositiveInfinity;
            foreach (var collider in Physics.OverlapSphere(transform.position + Vector3.up, range,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var candidate = collider.GetComponentInParent<IInteractable>();
                if (candidate == null || !(candidate is Component component)) continue;
                float distance = (candidate.InteractionPoint - (transform.position + Vector3.up)).sqrMagnitude;
                if (distance >= best || !InteractionReach.CanReach(transform, component.transform, candidate.InteractionPoint, range)) continue;
                best = distance;
                nearest = candidate;
            }
            if (nearest != null) return nearest.TryInteract(GetComponent<Inventory>(), out message);
            message = "No reachable interactable nearby.";
            return false;
        }
    }
}
