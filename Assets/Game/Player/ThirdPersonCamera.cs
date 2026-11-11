using UnityEngine;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField, Min(0.5f)] private float distance = 5f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float stickSensitivity = 120f;
        [SerializeField] private float minimumPitch = -25f;
        [SerializeField] private float maximumPitch = 70f;
        [SerializeField] private float pitch = 20f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        private float yaw;
        [SerializeField, Min(1f)] private float lockTrackingSpeed = 8f;
        private Vector3? focusPoint;
        public Transform Target => target;
        public void SetTarget(Transform value, Vector3 offset)
        {
            if (target == value) return;
            target = value;
            targetOffset = offset;
            focusPoint = null;
            OrientationOverride = null;
            if (target != null) { yaw = target.eulerAngles.y; pitch = 20f; }
        }
        public void SetFocusPoint(Vector3? point)
        {
            if (focusPoint.HasValue && !point.HasValue)
            {
                yaw = transform.eulerAngles.y;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), minimumPitch, maximumPitch);
            }
            focusPoint = point;
        }
        // Optional orientation supplied by a future camera mode; no target selection here.
        public Quaternion? OrientationOverride { get; set; }

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
        }

        public void ApplyLook(Vector2 look, bool pointerInput, float deltaTime)
        {
            if (focusPoint.HasValue && target != null)
            {
                Vector3 toTarget = focusPoint.Value - (target.position + targetOffset);
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    float blend = 1f - Mathf.Exp(-lockTrackingSpeed * deltaTime);
                    yaw = Mathf.LerpAngle(yaw, desiredYaw, blend);
                    pitch = Mathf.Lerp(pitch, 20f, blend);
                }
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                return;
            }
            if (OrientationOverride.HasValue)
            {
                Vector3 angles = OrientationOverride.Value.eulerAngles;
                yaw = angles.y;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), minimumPitch, maximumPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                return;
            }
            // Pointer delta is already per frame; sticks represent a rate.
            float scale = pointerInput ? mouseSensitivity : stickSensitivity * deltaTime;
            yaw = Mathf.Repeat(yaw + look.x * scale, 360f);
            pitch = Mathf.Clamp(pitch - look.y * scale, minimumPitch, maximumPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 backward = rotation * Vector3.back;
            float allowedDistance = distance;
            foreach (RaycastHit hit in Physics.SphereCastAll(pivot, collisionRadius, backward,
                         distance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - 0.05f));
            }
            Vector3 position = pivot + backward * allowedDistance;
            if (focusPoint.HasValue)
            {
                Vector3 aim = Vector3.Lerp(pivot, focusPoint.Value, 0.5f) - position;
                if (aim.sqrMagnitude > 0.001f) rotation = Quaternion.LookRotation(aim);
            }
            transform.SetPositionAndRotation(position, rotation);
        }

        private void OnValidate()
        {
            minimumPitch = Mathf.Clamp(minimumPitch, -80f, 80f);
            maximumPitch = Mathf.Clamp(maximumPitch, minimumPitch, 80f);
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
        }
    }
}
