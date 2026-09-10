using System;
using HeroesOfDragonland.Targeting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    public sealed class TargetLock : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(1f)] private float maximumDistance = 20f;
        [SerializeField, Range(1f, 90f)] private float acquisitionAngle = 60f;
        [SerializeField] private LayerMask visibilityMask = ~0;
        [SerializeField] private InputAction toggleLock = new InputAction("Toggle Lock", InputActionType.Button, "<Mouse>/middleButton");
        [SerializeField] private InputAction switchLeft = new InputAction("Switch Left", InputActionType.Button, "<Keyboard>/q");
        [SerializeField] private InputAction switchRight = new InputAction("Switch Right", InputActionType.Button, "<Keyboard>/e");

        public Targetable CurrentTarget { get; private set; }
        public event Action<Targetable> TargetChanged;
        public Vector3? FacingDirection => CurrentTarget != null
            ? CurrentTarget.AimPosition - transform.position : (Vector3?)null;
        public void SetViewCamera(Camera value) => viewCamera = value;

        private void OnEnable() { toggleLock.Enable(); switchLeft.Enable(); switchRight.Enable(); }
        private void OnDisable() { toggleLock.Disable(); switchLeft.Disable(); switchRight.Disable(); Unlock(); }
        private void OnDestroy() { toggleLock.Dispose(); switchLeft.Dispose(); switchRight.Dispose(); }

        // Called by the player coordinator before camera and movement update.
        public void Tick(bool acceptInput)
        {
            if (!isActiveAndEnabled) return;
            if (!ReferenceEquals(CurrentTarget, null) && !InRange(CurrentTarget)) Unlock();
            if (!acceptInput) return;
            if (toggleLock.WasPressedThisFrame()) Toggle();
            if (switchLeft.WasPressedThisFrame()) Switch(-1);
            else if (switchRight.WasPressedThisFrame()) Switch(1);
        }

        public void Toggle()
        {
            if (CurrentTarget != null) { Unlock(); return; }
            Targetable best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<Targetable>())
            {
                if (!CanAcquire(candidate)) continue;
                float angle = Vector3.Angle(viewCamera.transform.forward, candidate.AimPosition - viewCamera.transform.position);
                float score = angle / acquisitionAngle + Vector3.Distance(transform.position, candidate.AimPosition) / maximumDistance * 0.3f;
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
            SetTarget(best);
        }

        public void Switch(int direction)
        {
            if (!InRange(CurrentTarget) || viewCamera == null || direction == 0) return;
            Vector3 current = viewCamera.WorldToViewportPoint(CurrentTarget.AimPosition);
            Targetable best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<Targetable>())
            {
                if (candidate == CurrentTarget || !CanAcquire(candidate)) continue;
                Vector3 point = viewCamera.WorldToViewportPoint(candidate.AimPosition);
                float horizontal = (point.x - current.x) * Mathf.Sign(direction);
                if (horizontal <= 0.001f) continue;
                float score = horizontal + Mathf.Abs(point.y - current.y) * 0.5f;
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
            if (best != null) SetTarget(best);
        }

        public void Unlock() => SetTarget(null);
        private void SetTarget(Targetable value)
        {
            if (ReferenceEquals(CurrentTarget, value)) return;
            CurrentTarget = value;
            TargetChanged?.Invoke(value);
        }

        private bool InRange(Targetable value) => value != null && value.IsValid
            && HeroesOfDragonland.Teams.TeamIdentity.CanDamage(gameObject, value.gameObject)
            && value.transform != transform && !value.transform.IsChildOf(transform)
            && (value.AimPosition - transform.position).sqrMagnitude <= maximumDistance * maximumDistance;

        private bool CanAcquire(Targetable value)
        {
            if (viewCamera == null || !InRange(value)) return false;
            Vector3 ray = value.AimPosition - viewCamera.transform.position;
            Vector3 viewport = viewCamera.WorldToViewportPoint(value.AimPosition);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f
                || Vector3.Angle(viewCamera.transform.forward, ray) > acquisitionAngle) return false;
            foreach (var hit in Physics.RaycastAll(viewCamera.transform.position, ray.normalized,
                         ray.magnitude, visibilityMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)
                    || hit.transform == value.transform || hit.transform.IsChildOf(value.transform)) continue;
                return false;
            }
            return true;
        }
    }
}
