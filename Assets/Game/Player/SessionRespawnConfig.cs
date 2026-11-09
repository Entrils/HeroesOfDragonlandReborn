using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public enum RespawnResourcePolicy { Full, Preserve, Empty }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Session Respawn")]
    public sealed class SessionRespawnConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float delay = 5f;
        [SerializeField, Min(0f)] private float protectionDuration = 3f;
        [SerializeField] private RespawnResourcePolicy mana = RespawnResourcePolicy.Full;
        [SerializeField] private RespawnResourcePolicy stamina = RespawnResourcePolicy.Full;
        public float Delay => FiniteDuration(delay);
        [SerializeField, Min(0)] private float lateDelay;
        [SerializeField, Min(0)] private float rampStartSeconds = 600;
        [SerializeField, Min(1)] private float rampEndSeconds = 1200;
        public float DelayAt(float playingSeconds) => lateDelay <= 0 ? Delay : Mathf.Lerp(Delay, FiniteDuration(lateDelay),
            Mathf.InverseLerp(rampStartSeconds, Mathf.Max(rampStartSeconds + 1, rampEndSeconds), playingSeconds));
        public float ProtectionDuration => FiniteDuration(protectionDuration);
        public RespawnResourcePolicy Mana => mana;
        public RespawnResourcePolicy Stamina => stamina;
        private static float FiniteDuration(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
