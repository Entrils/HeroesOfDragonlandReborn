using UnityEngine;

namespace HeroesOfDragonland.Player
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Dodge")]
    public sealed class DodgeDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float staminaCost = 25f;
        [SerializeField, Min(0f)] private float distance = 4f;
        [SerializeField, Min(0.05f)] private float duration = 0.4f;
        [SerializeField, Min(0f)] private float recovery = 0.3f;
        [SerializeField, Range(0f, 1f)] private float invulnerabilityStart = 0.15f;
        [SerializeField, Range(0f, 1f)] private float invulnerabilityEnd = 0.75f;

        public float StaminaCost => Mathf.Max(0f, staminaCost);
        public float Distance => Mathf.Max(0f, distance);
        public float Duration => Mathf.Max(0.05f, duration);
        public float Recovery => Mathf.Max(0f, recovery);
        public float InvulnerabilityStartTime => Mathf.Clamp01(invulnerabilityStart) * Duration;
        public float InvulnerabilityEndTime => Mathf.Clamp(invulnerabilityEnd, Mathf.Clamp01(invulnerabilityStart), 1f) * Duration;
    }
}
