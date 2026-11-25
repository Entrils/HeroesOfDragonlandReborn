using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.World
{
    // Binds the existing Souls-style camera to the local connection's avatar only.
    [DefaultExecutionOrder(180), DisallowMultipleComponent, RequireComponent(typeof(Camera), typeof(ThirdPersonCamera))]
    public sealed class BattlefieldCamera : MonoBehaviour
    {
        [SerializeField] private MultiplayerBootstrap bootstrap;
        [SerializeField] private Transform spectatorScenery;
        private ThirdPersonCamera follow;
        private Camera view;
        private BattlefieldSpectator spectator;
        private Transform localTarget;
        private LocalGameplayInputGate gate;
        private bool captured;
        private bool wasPlaying;
        public bool HasLocalTarget => localTarget != null;
        public bool IsCaptured => captured;
        private void Awake()
        {
            follow = GetComponent<ThirdPersonCamera>();
            view = GetComponent<Camera>();
            spectator = GetComponent<BattlefieldSpectator>();
            if (spectator == null) spectator = gameObject.AddComponent<BattlefieldSpectator>();
            spectator.Configure(view, spectatorScenery);
            view.orthographic = false;
            view.fieldOfView = 60;
            view.nearClipPlane = .1f;
        }
        private void Update()
        {
            var runner = bootstrap != null ? bootstrap.Runner : null;
            Transform next = null;
            if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var actor) && actor != null && actor.IsValid)
                next = actor.transform;
            if (next != localTarget)
            {
                localTarget = next;
                follow.enabled = next != null;
                follow.SetTarget(next, new Vector3(0, .6f, 0));
            }
            var match = bootstrap != null ? bootstrap.GetComponent<NetworkFortressSpawner>()?.Match : null;
            bool activeMatch = localTarget != null && match != null && match.Object != null && match.Object.IsValid && match.CanPlay;
            bool spectating = activeMatch && localTarget.TryGetComponent<NetworkCharacterStats>(out var health) && health.Dead;
            if (spectating && !spectator.Active)
            {
                localTarget.GetComponent<TargetLock>()?.Unlock();
                bootstrap.GetComponent<HeroesOfDragonland.UI.Minimap.TacticalMapSource>()?.Refresh();
            }
            spectator.UpdateView(bootstrap, localTarget != null ? localTarget.GetComponent<NetworkTestPlayer>() : null, spectating);
            follow.enabled = localTarget != null && !spectating;
            bool playing = activeMatch && !spectating;
            gate = bootstrap != null ? bootstrap.GetComponent<LocalGameplayInputGate>() : null;
            if (playing && !wasPlaying) Capture(true);
            if (!playing && wasPlaying) Capture(false);
            wasPlaying = playing;
            if (Application.isFocused && Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) Capture(false);
                else if (playing && Keyboard.current.enterKey.wasPressedThisFrame) Capture(true);
            }
            gate?.SetBlocked(this, spectating || (playing && !captured));
            bool acceptLook = captured && Application.isFocused && (gate == null || !gate.IsBlocked);
            if (gate != null && gate.IsBlocked && captured) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (captured && Application.isFocused) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (localTarget == null || spectating) return;
            var targeting = localTarget.GetComponent<TargetLock>();
            follow.SetFocusTarget(targeting != null ? targeting.CurrentTarget : null);
            follow.ApplyLook(acceptLook && Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero, true, Time.deltaTime);
        }
        public Vector2 ToWorldMovement(Vector2 input)
        {
            var world = Quaternion.Euler(0, follow != null ? follow.InputYaw : transform.eulerAngles.y, 0) * new Vector3(input.x, 0, input.y);
            return new Vector2(world.x, world.z);
        }
        private void Capture(bool value)
        {
            captured = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }
        private void OnDisable()
        {
            spectator?.StopViewing();
            gate?.SetBlocked(this, false);
            if (follow != null) { follow.SetTarget(null, Vector3.zero); follow.enabled = false; }
            localTarget = null;
            Capture(false);
        }
    }
}
