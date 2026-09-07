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

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
        }

        public void ApplyLook(Vector2 look, bool pointerInput, float deltaTime)
        {
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
            transform.SetPositionAndRotation(pivot + backward * allowedDistance, rotation);
        }

        private void OnValidate()
        {
            minimumPitch = Mathf.Clamp(minimumPitch, -80f, 80f);
            maximumPitch = Mathf.Clamp(maximumPitch, minimumPitch, 80f);
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
        }
    }
}
