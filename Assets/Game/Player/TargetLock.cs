using System;
using HeroesOfDragonland.Targeting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    public sealed class TargetLock : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(1f)] private float maximumDistance = 20f;
        [SerializeField, Range(1f, 90f)] private float acquisitionAngle = 60f;
        [SerializeField, Min(.1f)] private float largeTargetHeight = 2.35f;
        [SerializeField] private LayerMask visibilityMask = ~0;
        [SerializeField] private InputAction toggleLock = new InputAction("Toggle Lock", InputActionType.Button, "<Mouse>/middleButton");
        [SerializeField] private InputAction switchLeft = new InputAction("Switch Left", InputActionType.Button, "<Keyboard>/q");
        [SerializeField] private InputAction switchRight = new InputAction("Switch Right", InputActionType.Button, "<Keyboard>/e");

        private RaycastHit[] visibilityHits = new RaycastHit[16];

        public Targetable CurrentTarget { get; private set; }
        public event Action<Targetable> TargetChanged;
        public event Action AcquisitionFailed;
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
                if (!TryVisiblePoint(candidate, out var visible)) continue;
                float angle = Vector3.Angle(viewCamera.transform.forward, visible - viewCamera.transform.position);
                float score = angle / acquisitionAngle + Vector3.Distance(transform.position, candidate.AimPosition) / maximumDistance * 0.3f;
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
            if (best == null) AcquisitionFailed?.Invoke();
            else SetTarget(best);
        }

        public void Switch(int direction)
        {
            if (!InRange(CurrentTarget) || viewCamera == null || direction == 0) return;
            Vector3 current = viewCamera.WorldToViewportPoint(CurrentTarget.AimPosition);
            float currentBearing = Bearing(CurrentTarget);
            float sign = Mathf.Sign(direction);
            Targetable best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<Targetable>())
            {
                if (candidate == CurrentTarget || !TryVisiblePoint(candidate, out var visible)) continue;
                Vector3 point = viewCamera.WorldToViewportPoint(visible);
                float horizontal = Mathf.DeltaAngle(currentBearing, Bearing(candidate)) * sign;
                if (horizontal <= .5f) continue;
                float vertical = Mathf.Abs(Mathf.Clamp01(point.y) - Mathf.Clamp01(current.y));
                float score = horizontal + vertical * 20f;
                if (best == null || score < bestScore || (Mathf.Approximately(score, bestScore) && EntityId.ToULong(candidate.GetEntityId()) < EntityId.ToULong(best.GetEntityId())))
                { best = candidate; bestScore = score; }
            }
            if (best != null) SetTarget(best);
        }

        private float Bearing(Targetable candidate)
        {
            Vector3 offset = candidate.AimPosition - transform.position;
            // The camera shoulder and boom length must not change the left/right ordering.
            return offset.x * offset.x + offset.z * offset.z > .16f
                ? Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg : viewCamera.transform.eulerAngles.y;
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
            && value.Owner != transform && !value.Owner.IsChildOf(transform)
            && ((value.AimPosition - transform.position).sqrMagnitude <= maximumDistance * maximumDistance
                || (value.FramingHeight >= largeTargetHeight
                    && (value.FramingCenter - transform.position).sqrMagnitude <= maximumDistance * maximumDistance));

        private bool TryVisiblePoint(Targetable value, out Vector3 point)
        {
            point = default;
            if (viewCamera == null || !InRange(value) || value.gameObject.scene.GetPhysicsScene() != gameObject.scene.GetPhysicsScene())
                return false;
            point = value.AimPosition;
            if (IsVisible(value, point)) return true;
            if (value.FramingHeight < largeTargetHeight) return false;
            point = value.FramingCenter;
            return IsVisible(value, point);
        }

        private bool IsVisible(Targetable value, Vector3 point)
        {
            Vector3 ray = point - viewCamera.transform.position;
            Vector3 viewport = viewCamera.WorldToViewportPoint(point);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f
                || Vector3.Angle(viewCamera.transform.forward, ray) > acquisitionAngle) return false;
            var scene = gameObject.scene.GetPhysicsScene();
            int count;
            while ((count = scene.Raycast(viewCamera.transform.position, ray.normalized, visibilityHits,
                ray.magnitude, visibilityMask, QueryTriggerInteraction.Ignore)) == visibilityHits.Length)
                Array.Resize(ref visibilityHits, visibilityHits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                Transform hit = visibilityHits[i].transform;
                if (hit == transform || hit.IsChildOf(transform)
                    || hit == value.Owner || hit.IsChildOf(value.Owner)) continue;
                return false;
            }
            return true;
        }
    }
}
