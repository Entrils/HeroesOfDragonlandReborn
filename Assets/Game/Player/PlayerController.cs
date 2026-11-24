using UnityEngine;
using UnityEngine.InputSystem;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;

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
        private PlayerDodge dodge;
        private HeroesOfDragonland.Interaction.PlayerInteractor interactor;
        private InputAction dodgeAction;
        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction attackAction;
        private Equipment equipment;
        internal BowShot LocalBow { get; private set; }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            targetLock = GetComponent<TargetLock>();
            meleeAttack = GetComponent<MeleeAttack>();
            equipment = GetComponent<Equipment>();
            LocalBow = new BowShot(gameObject);
            if (GetComponent<BowPresentation>() == null) gameObject.AddComponent<BowPresentation>();
            LocalBow.Impact += point => GetComponent<BowPresentation>()?.PresentImpact(point, null, Teams.TeamId.Unassigned);
            dodge = GetComponent<PlayerDodge>();
            interactor = GetComponent<HeroesOfDragonland.Interaction.PlayerInteractor>();
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
            dodgeAction = runtimeActions.FindAction("Player/Dodge", true);
        }

        private void OnEnable()
        {
            if (runtimeActions == null) return;
            moveAction.Enable();
            lookAction.Enable();
            jumpAction.Enable();
            attackAction.Enable();
            dodgeAction.Enable();
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
            if (!captured || !wasCaptured) meleeAttack?.DiscardBufferedAttack();
            if (targetLock != null) targetLock.Tick(captured);
            followCamera.SetFocusPoint(targetLock != null && targetLock.CurrentTarget != null
                ? targetLock.CurrentTarget.AimPosition : (Vector3?)null);
            Vector2 look = captured ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            followCamera.ApplyLook(look, lookAction.activeControl?.device is Pointer, Time.deltaTime);
            Vector2 input = captured ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Quaternion yaw = Quaternion.Euler(0f, followCamera.transform.eulerAngles.y, 0f);
            Vector3 direction = yaw * new Vector3(input.x, 0f, input.y);
            Vector3? facing = targetLock != null ? targetLock.FacingDirection : null;
            var defense = GetComponent<CombatDefense>();
            var weapon = equipment != null ? equipment.EquippedWeapon : null;
            bool usingBow = weapon != null && weapon.Archetype == WeaponArchetype.Bow;
            if (!LocalBow.Drawing && captured && wasCaptured && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                GetComponent<ParryDefense>()?.TryStart(facing ?? transform.forward);
            defense?.SetBlockIntent(!usingBow && captured && wasCaptured && Mouse.current != null && Mouse.current.rightButton.isPressed,
                LocalBow.Drawing || (meleeAttack != null && meleeAttack.IsBusy) || (dodge != null && dodge.IsBusy));
            if (usingBow)
            {
                var aim = BowAim.Read(followCamera.GetComponent<Camera>(), transform, weapon.Range);
                if (captured && targetLock?.CurrentTarget == null && Mouse.current?.rightButton.isPressed == true)
                    facing = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
                LocalBow.UpdateAim(aim, targetLock?.CurrentTarget);
                if (captured && wasCaptured && attackAction.WasPressedThisFrame()
                    && !(defense?.ActionsBlocked ?? false) && !(meleeAttack?.IsBusy ?? false) && !(dodge?.IsBusy ?? false)
                    && !dodgeAction.WasPressedThisFrame())
                    LocalBow.TryStart(aim, targetLock?.CurrentTarget);
            }
            LocalBow.Tick(Time.deltaTime, !captured || !usingBow || (defense?.ActionsBlocked ?? false));
            if (defense != null && defense.IsStaggered)
            {
                movement.Tick(Vector3.zero, false, Time.deltaTime, facing);
                return;
            }
            if (!usingBow && meleeAttack != null && captured && wasCaptured && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                meleeAttack.TryHeavyAttack(facing);
            if (dodge != null && !LocalBow.Drawing && captured && wasCaptured && dodgeAction.WasPressedThisFrame())
                dodge.TryDodge(direction, facing);
            if (dodge != null && dodge.IsBusy)
            {
                dodge.TickDodge(Time.deltaTime, facing);
                return;
            }
            if (!usingBow && meleeAttack != null && captured && wasCaptured && attackAction.WasPressedThisFrame())
                meleeAttack.TryAttack(facing);
            if (GetComponent<ParryDefense>() is { IsBusy: true } parry) facing = parry.Facing;
            bool attacking = meleeAttack != null && meleeAttack.IsBusy;
            if (interactor != null) interactor.Tick(captured && wasCaptured && !attacking && !(defense != null && defense.ActionsBlocked));
            movement.Tick(direction * (LocalBow.Drawing ? .55f : attacking ? meleeAttack.MovementMultiplier : 1f)
                * (defense != null ? defense.MovementMultiplier : 1f),
                captured && !attacking && !LocalBow.Drawing && !(defense != null && defense.ActionsBlocked) && jumpAction.WasPressedThisFrame(), Time.deltaTime,
                LocalBow.Drawing ? LocalBow.Facing : attacking ? meleeAttack.FacingDirection : facing);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            LocalBow?.Cancel();
            moveAction?.Disable();
            lookAction?.Disable();
            jumpAction?.Disable();
            attackAction?.Disable();
            dodgeAction?.Disable();
            if (dodge != null) dodge.Cancel();
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
