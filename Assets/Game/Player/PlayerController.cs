using UnityEngine;
using UnityEngine.InputSystem;
using HeroesOfDragonland.Combat;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ThirdPersonCamera followCamera;
        private PlayerMovement movement;
        private TargetLock targetLock;
        private MeleeAttack meleeAttack;
        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction attackAction;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            targetLock = GetComponent<TargetLock>();
            meleeAttack = GetComponent<MeleeAttack>();
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
            jumpAction = runtimeActions.FindAction("Player/Jump", true);
            attackAction = runtimeActions.FindAction("Player/Attack", true);
        }

        private void OnEnable()
        {
            if (runtimeActions == null) return;
            moveAction.Enable();
            lookAction.Enable();
            jumpAction.Enable();
            attackAction.Enable();
            SetCursorCaptured(true);
        }

        private void Update()
        {
            bool wasCaptured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);
            else if (Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorCaptured(true);

            bool captured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (targetLock != null) targetLock.Tick(captured);
            followCamera.SetFocusPoint(targetLock != null && targetLock.CurrentTarget != null
                ? targetLock.CurrentTarget.AimPosition : (Vector3?)null);
            Vector2 look = captured ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            followCamera.ApplyLook(look, lookAction.activeControl?.device is Pointer, Time.deltaTime);
            Vector2 input = captured ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Quaternion yaw = Quaternion.Euler(0f, followCamera.transform.eulerAngles.y, 0f);
            Vector3 direction = yaw * new Vector3(input.x, 0f, input.y);
            Vector3? facing = targetLock != null ? targetLock.FacingDirection : null;
            if (meleeAttack != null && captured && wasCaptured && attackAction.WasPressedThisFrame())
                meleeAttack.TryAttack(facing);
            bool attacking = meleeAttack != null && meleeAttack.IsBusy;
            movement.Tick(direction * (attacking ? meleeAttack.MovementMultiplier : 1f),
                captured && !attacking && jumpAction.WasPressedThisFrame(), Time.deltaTime,
                attacking ? meleeAttack.FacingDirection : facing);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            jumpAction?.Disable();
            attackAction?.Disable();
            if (targetLock != null) targetLock.Unlock();
            if (followCamera != null) followCamera.SetFocusPoint(null);
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
