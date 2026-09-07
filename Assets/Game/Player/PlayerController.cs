using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ThirdPersonCamera followCamera;
        private PlayerMovement movement;
        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction lookAction;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            if (inputActions == null || followCamera == null)
            {
                Debug.LogError("PlayerController requires Input Actions and Follow Camera references.", this);
                enabled = false;
                return;
            }
            // Own a copy so enabling this player never changes the shared asset's state.
            runtimeActions = Instantiate(inputActions);
            moveAction = runtimeActions.FindAction("Player/Move", true);
            lookAction = runtimeActions.FindAction("Player/Look", true);
        }

        private void OnEnable()
        {
            if (runtimeActions == null) return;
            moveAction.Enable();
            lookAction.Enable();
            SetCursorCaptured(true);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);
            else if (Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorCaptured(true);

            bool captured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            Vector2 look = captured ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            followCamera.ApplyLook(look, lookAction.activeControl?.device is Pointer, Time.deltaTime);
            movement.Tick(captured ? moveAction.ReadValue<Vector2>() : Vector2.zero,
                followCamera.transform, Time.deltaTime);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
