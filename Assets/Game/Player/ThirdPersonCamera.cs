using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Player
{
    [DefaultExecutionOrder(250), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Free orbit")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new(0f, 1.5f, 0f);
        [SerializeField, Min(.5f)] private float distance = 5f;
        [SerializeField, Min(0f)] private float mouseSensitivity = .12f;
        [SerializeField, Min(0f)] private float stickSensitivity = 120f;
        [SerializeField] private float minimumPitch = -25f;
        [SerializeField] private float maximumPitch = 70f;
        [SerializeField] private float pitch = 20f;
        [SerializeField, Min(0f)] private float followTime = .09f;
        [SerializeField, Min(0f)] private float orbitAccelerationTime = .055f;
        [SerializeField, Min(.2f)] private float teleportDistance = 4f;
        [SerializeField, Range(-1f, 1f)] private float shoulderOffset = .48f;

        [Header("World clearance")]
        [SerializeField, Min(.01f)] private float collisionRadius = .25f;
        [SerializeField, Min(0f)] private float wallPadding = .08f;
        [SerializeField, Min(0f)] private float obstructionReleaseTime = .24f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Lock-on composition")]
        [SerializeField, Min(1f)] private float lockTrackingSpeed = 8f;
        [SerializeField, Min(.1f)] private float closeLockDistance = 1.35f;
        [SerializeField, Range(0f, .6f)] private float targetFramingWeight = .35f;
        [SerializeField, Min(.01f)] private float lockTransitionTime = .16f;
        [SerializeField, Range(0f, 1f)] private float lockShoulderWeight = .35f;
        [SerializeField, Range(-15f, 40f)] private float lockRestPitch = 17f;
        [SerializeField, Min(0f)] private float bossPullback = 2.6f;
        [SerializeField, Min(.1f)] private float largeTargetHeight = 2.35f;

        [Header("Local presentation")]
        [SerializeField] private bool combatImpulses = true;
        [SerializeField] private bool debugFraming;

        private Camera view;
        private CameraClearance clearance;
        private Targetable focusTarget;
        private Vector3? focusPoint;
        private float yaw, visualYaw, visualPitch, yawVelocity, pitchVelocity;
        private float boom, boomVelocity, shoulder, shoulderVelocity, lockWeight, lockVelocity;
        private float lockDistance, lockDistanceVelocity, lockPitch;
        private Vector3 pivot, pivotVelocity, previousAnchor, composedPoint, aimVelocity;
        private Vector3 debugPivot, debugShoulder, debugDesired;
        private bool initialized;

        public Transform Target => target;
        public float InputYaw => yaw;
        public bool CombatImpulses => combatImpulses;
        public Quaternion? OrientationOverride { get; set; }

        private void Awake()
        {
            view = GetComponent<Camera>();
            clearance = new CameraClearance(gameObject, BlocksCamera);
        }

        public void SetTarget(Transform value, Vector3 offset)
        {
            if (target == value && targetOffset == offset) return;
            target = value;
            targetOffset = offset;
            SetFocusTarget(null);
            OrientationOverride = null;
            if (target != null) { yaw = target.eulerAngles.y; pitch = Mathf.Clamp(20f, minimumPitch, maximumPitch); }
            initialized = false;
        }

        public void SetFocusTarget(Targetable value)
        {
            focusTarget = value;
            focusPoint = null;
        }

        public void SetFocusPoint(Vector3? point)
        {
            focusTarget = null;
            focusPoint = point;
        }

        private void OnEnable()
        {
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            initialized = false;
        }

        private Vector3? Focus => focusTarget != null ? focusTarget.AimPosition : focusPoint;

        public void ApplyLook(Vector2 look, bool pointerInput, float deltaTime)
        {
            if (OrientationOverride is { } orientation)
            {
                Vector3 angles = orientation.eulerAngles;
                yaw = angles.y;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), minimumPitch, maximumPitch);
                return;
            }
            if (target != null && Focus is { } focus)
            {
                var delta = focus - (target.position + targetOffset);
                float planar = new Vector2(delta.x, delta.z).magnitude;
                if (planar > closeLockDistance)
                {
                    float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    float stability = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(closeLockDistance, closeLockDistance * 2, planar));
                    yaw = Mathf.LerpAngle(yaw, bearing, (1 - Mathf.Exp(-lockTrackingSpeed * deltaTime)) * stability);
                }
                return;
            }
            // The input basis responds immediately. Only the rendered orbit is damped.
            float scale = pointerInput ? mouseSensitivity * HeroesOfDragonland.UI.PlaytestSettings.MouseMultiplier : stickSensitivity * deltaTime;
            yaw = Mathf.Repeat(yaw + look.x * scale, 360f);
            pitch = Mathf.Clamp(pitch - look.y * scale, minimumPitch, maximumPitch);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            Vector3 anchor = target.position + targetOffset;
            Vector3? focus = Focus;
            bool locked = focus.HasValue && !OrientationOverride.HasValue;
            bool reset = !initialized || (anchor - previousAnchor).sqrMagnitude > teleportDistance * teleportDistance;
            if (reset) ResetRig(anchor, focus ?? anchor, locked);
            previousAnchor = anchor;

            pivot = followTime > 0 ? Vector3.SmoothDamp(pivot, anchor, ref pivotVelocity, followTime, Mathf.Infinity, dt) : anchor;
            // A trailing pivot must never remain around a corner after the player clears it.
            var scene = target.gameObject.scene.GetPhysicsScene();
            float radius = Mathf.Max(collisionRadius, NearPlaneRadius());
            Vector3 safeAnchor = clearance.ClearOrigin(scene, anchor, radius, obstructionMask);
            Vector3 drift = pivot - safeAnchor;
            Vector3 origin = safeAnchor + drift.normalized * clearance.Distance(scene, safeAnchor, drift, radius, wallPadding, obstructionMask);

            lockWeight = Mathf.SmoothDamp(lockWeight, locked ? 1 : 0, ref lockVelocity, lockTransitionTime, Mathf.Infinity, dt);
            float desiredPullback = 0;
            if (locked)
            {
                Vector3 interest = focus.Value;
                float height = focusTarget != null ? focusTarget.FramingHeight : 0;
                float large = Mathf.InverseLerp(largeTargetHeight, largeTargetHeight * 2.5f, height);
                Vector3 separation = interest - anchor;
                float horizontal = new Vector2(separation.x, separation.z).magnitude;
                float vertical = Mathf.Atan2(separation.y, Mathf.Max(closeLockDistance, horizontal)) * Mathf.Rad2Deg;
                lockPitch = Mathf.Clamp(lockRestPitch - vertical * .55f, minimumPitch, maximumPitch);
                float weight = targetFramingWeight * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, closeLockDistance * 2, horizontal));
                // Wide arenas can show more of a large target without aiming the camera through its feet.
                if (Vector3.Dot(separation, Quaternion.Euler(0, yaw, 0) * Vector3.forward) < 0) weight = 0;
                Vector3 focalPoint = focusTarget != null ? Vector3.Lerp(interest, focusTarget.FramingCenter, large) : interest;
                Vector3 desiredPoint = Vector3.Lerp(anchor, focalPoint, weight);
                float elevationWeight = targetFramingWeight * .65f * Mathf.InverseLerp(.5f, 2.5f, Mathf.Abs(separation.y));
                desiredPoint.y = Mathf.Lerp(anchor.y, focalPoint.y, Mathf.Max(weight, elevationWeight));
                composedPoint = Vector3.SmoothDamp(composedPoint, desiredPoint, ref aimVelocity, lockTransitionTime, Mathf.Infinity, dt);
                desiredPullback = large * bossPullback;
            }
            else
            {
                composedPoint = Vector3.SmoothDamp(composedPoint, anchor, ref aimVelocity, lockTransitionTime, Mathf.Infinity, dt);
            }
            lockDistance = Mathf.SmoothDamp(lockDistance, desiredPullback, ref lockDistanceVelocity, .25f, Mathf.Infinity, dt);

            float desiredPitch = Mathf.Lerp(pitch, lockPitch, lockWeight);
            visualYaw = DampAngle(visualYaw, yaw, ref yawVelocity, dt);
            visualPitch = DampAngle(visualPitch, desiredPitch, ref pitchVelocity, dt);
            var orbit = Quaternion.Euler(visualPitch, visualYaw, 0);
            Vector3 backward = orbit * Vector3.back;
            Vector3 right = orbit * Vector3.right;
            float desiredDistance = distance + lockDistance;
            float centerRoom = clearance.Distance(scene, origin, backward * desiredDistance, radius, wallPadding, obstructionMask);
            float space = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, Mathf.Min(2.6f, desiredDistance + .6f), centerRoom));
            float desiredShoulder = shoulderOffset * Mathf.Lerp(1f, lockShoulderWeight, lockWeight) * space;
            if (reset) shoulder = desiredShoulder;
            shoulder = Mathf.SmoothDamp(shoulder, desiredShoulder, ref shoulderVelocity, .12f, Mathf.Infinity, dt);
            Vector3 side = right * shoulder;
            Vector3 shoulderOrigin = origin + side.normalized * clearance.Distance(scene, origin, side, radius, wallPadding, obstructionMask);
            float allowed = clearance.Distance(scene, shoulderOrigin, backward * desiredDistance, radius, wallPadding, obstructionMask);
            // In a narrow opening favor a centered boom over a blocked shoulder.
            if (allowed + .3f < centerRoom)
            {
                shoulderOrigin = origin;
                allowed = centerRoom;
                shoulder = shoulderVelocity = 0;
            }

            if (reset || allowed < boom) { boom = allowed; boomVelocity = 0; }
            else boom = Mathf.SmoothDamp(boom, allowed, ref boomVelocity, Mathf.Max(.001f, obstructionReleaseTime), Mathf.Infinity, dt);
            Vector3 position = shoulderOrigin + backward * boom;
            Vector3 aim = composedPoint - position;
            var framed = aim.sqrMagnitude > .16f ? Quaternion.LookRotation(aim) : orbit;
            float framing = lockWeight * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, 1.25f, boom));
            transform.SetPositionAndRotation(position, Quaternion.Slerp(orbit, framed, framing));

            debugPivot = origin;
            debugShoulder = shoulderOrigin;
            debugDesired = shoulderOrigin + backward * desiredDistance;
        }

        private float DampAngle(float current, float desired, ref float velocity, float dt)
            => orbitAccelerationTime > 0 ? Mathf.SmoothDampAngle(current, desired, ref velocity, orbitAccelerationTime, Mathf.Infinity, dt) : desired;

        private void ResetRig(Vector3 anchor, Vector3 focus, bool locked)
        {
            initialized = true;
            pivot = anchor;
            composedPoint = locked ? Vector3.Lerp(anchor, focus, targetFramingWeight) : anchor;
            visualYaw = yaw;
            visualPitch = lockPitch = pitch;
            pivotVelocity = aimVelocity = Vector3.zero;
            yawVelocity = pitchVelocity = boomVelocity = shoulderVelocity = lockVelocity = lockDistanceVelocity = 0;
            lockWeight = lockDistance = 0;
        }

        private float NearPlaneRadius()
        {
            float height = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad) * view.nearClipPlane;
            return new Vector3(height * view.aspect, height, view.nearClipPlane).magnitude;
        }

        private bool BlocksCamera(Collider collider) => collider != null
            && collider.transform != target && !collider.transform.IsChildOf(target)
            && (collider.GetComponentInParent<FortressGate>() != null
                || (collider.GetComponentInParent<CharacterStats>() == null
                    && collider.GetComponentInParent<CharacterController>() == null));

        private void OnValidate()
        {
            minimumPitch = Mathf.Clamp(minimumPitch, -80f, 80f);
            maximumPitch = Mathf.Clamp(maximumPitch, minimumPitch, 80f);
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugFraming || !initialized || view == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(debugPivot, debugShoulder);
            Gizmos.DrawLine(debugShoulder, transform.position);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(collisionRadius, NearPlaneRadius()));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, debugDesired);
            if (Focus.HasValue) Gizmos.DrawWireSphere(composedPoint, .15f);
        }
    }
}
