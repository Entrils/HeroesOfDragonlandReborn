using UnityEngine;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 720f;
        [SerializeField, Min(0.01f)] private float acceleration = 25f;
        [SerializeField, Min(0.01f)] private float deceleration = 35f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -25f;
        [SerializeField, Min(1f)] private float terminalSpeed = 50f;

        private CharacterController characterController;
        private float verticalSpeed;
        private Vector3 planarVelocity;
        public bool IsGrounded => characterController != null && characterController.isGrounded;

        private void Awake() => characterController = GetComponent<CharacterController>();
        private void OnDisable()
        {
            verticalSpeed = 0f;
            planarVelocity = Vector3.zero;
        }

        // Directions are world-space; input and camera policy belong to the caller.
        // A future targeting coordinator can supply facing without changing locomotion.
        public void Tick(Vector3 desiredDirection, bool jumpRequested, float deltaTime,
            Vector3? facingOverride = null, Vector3? velocityOverride = null)
        {
            if (deltaTime <= 0f || !characterController.enabled) return;

            desiredDirection.y = 0f;
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            float rate = desiredDirection.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity,
                desiredDirection * movementSpeed, rate * deltaTime);
            // Scripted actions use the same collision/gravity motor without residual drift.
            if (velocityOverride.HasValue) planarVelocity = Vector3.zero;
            Vector3 facing = facingOverride ?? desiredDirection;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(facing), rotationSpeed * deltaTime);

            bool grounded = IsGrounded && verticalSpeed <= 0f;
            Vector3 actionVelocity = velocityOverride ?? planarVelocity;
            Vector3 surfaceVelocity = actionVelocity;
            Vector3 feet = transform.TransformPoint(characterController.center)
                - Vector3.up * (characterController.height * 0.5f);
            bool walkable = Physics.Raycast(feet + Vector3.up * 0.2f, Vector3.down,
                out RaycastHit ground, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(ground.normal, Vector3.up) <= characterController.slopeLimit;
            if (grounded && walkable)
                surfaceVelocity = Vector3.ProjectOnPlane(actionVelocity, ground.normal).normalized
                    * actionVelocity.magnitude;

            if (grounded) verticalSpeed = -2f;
            if (jumpRequested && grounded && walkable)
            {
                verticalSpeed = Mathf.Sqrt(-2f * gravity * jumpHeight);
                surfaceVelocity = planarVelocity;
            }
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * deltaTime, -terminalSpeed);
            CollisionFlags flags = characterController.Move(
                (surfaceVelocity + Vector3.up * verticalSpeed) * deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        }

        private void OnValidate()
        {
            gravity = Mathf.Min(-0.01f, gravity);
        }
    }
}
