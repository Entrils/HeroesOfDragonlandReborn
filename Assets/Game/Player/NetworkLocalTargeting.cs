using HeroesOfDragonland.Player;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(TargetLock), typeof(NetworkTestPlayer))]
    public sealed class NetworkLocalTargeting : MonoBehaviour
    {
        private NetworkTestPlayer player;
        private NetworkCharacterStats stats;
        private TargetLock targetLock;
        public Vector2 FacingIntent
        {
            get
            {
                if (!IsLocal || !targetLock.enabled || stats.Dead) return Vector2.zero;
                var facing = targetLock.FacingDirection;
                return facing.HasValue ? new Vector2(facing.Value.x, facing.Value.z).normalized : Vector2.zero;
            }
        }
        private bool IsLocal => player.Object != null && player.Object.IsValid && player.HasInputAuthority;
        private void Awake()
        {
            player = GetComponent<NetworkTestPlayer>();
            stats = GetComponent<NetworkCharacterStats>();
            targetLock = GetComponent<TargetLock>();
            targetLock.enabled = false;
        }
        private void Update()
        {
            bool active = IsLocal && !stats.Dead && !Application.isBatchMode;
            targetLock.enabled = active;
            if (!active) return;
            targetLock.SetViewCamera(Camera.main);
            var gate = player.Runner.GetComponentInParent<LocalGameplayInputGate>();
            targetLock.Tick(Application.isFocused && (gate == null || !gate.IsBlocked));
        }
        private void OnDisable() { if (targetLock != null) targetLock.enabled = false; }
    }
}
