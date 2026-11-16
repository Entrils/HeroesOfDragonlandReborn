using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    // Presentation volume only: no colliders, triggers, navigation or network state.
    public sealed class AudioAmbienceZone : MonoBehaviour
    {
        [SerializeField] private AudioCue cue = AudioCue.FortressAmbience;
        [SerializeField, Min(1)] private float radius = 18;
        [SerializeField] private int priority = 10;
        public AudioCue Cue => cue;
        public int Priority => priority;
        public bool Contains(Vector3 position) => (position - transform.position).sqrMagnitude <= radius * radius;
        private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, radius); }
    }
}
