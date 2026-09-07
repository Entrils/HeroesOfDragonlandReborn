using UnityEngine;

namespace HeroesOfDragonland.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = -25f;
        [SerializeField, Min(1f)] private float terminalSpeed = 50f;

        private CharacterController characterController;
        private float verticalSpeed;
        public bool IsGrounded => characterController != null && characterController.isGrounded;

        private void Awake() => characterController = GetComponent<CharacterController>();
        private void OnDisable() => verticalSpeed = 0f;

        public void Tick(Vector2 input, Transform view, float deltaTime)
        {
            if (deltaTime <= 0f || !characterController.enabled) return;

            input = Vector2.ClampMagnitude(input, 1f);
            // Yaw alone keeps movement stable even when the camera looks steeply down.
            Quaternion yaw = Quaternion.Euler(0f, view.eulerAngles.y, 0f);
            Vector3 direction = yaw * new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), rotationSpeed * deltaTime);

            if (IsGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * deltaTime, -terminalSpeed);
            CollisionFlags flags = characterController.Move(
                (direction * movementSpeed + Vector3.up * verticalSpeed) * deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        }

        private void OnValidate()
        {
            gravity = Mathf.Min(-0.01f, gravity);
        }
    }
}
