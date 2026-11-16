using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [RequireComponent(typeof(GameAudioService)), DisallowMultipleComponent]
    public sealed class AudioAmbienceController : MonoBehaviour
    {
        [SerializeField] private AudioAmbienceZone[] zones;
        [SerializeField] private AudioCue fallback = AudioCue.ForestAmbience;
        [SerializeField] private bool playPlaceholderMusic;
        private GameAudioService service;
        private AudioCue current;
        private float gain;
        private bool initialized;
        private void Awake() => service = GetComponent<GameAudioService>();
        private void OnEnable() { initialized = false; gain = 0; }
        private void Update()
        {
            if (!service.isActiveAndEnabled || service.Listener == null) { initialized = false; gain = 0; return; }
            var desired = fallback; int priority = int.MinValue;
            if (zones != null) foreach (var zone in zones)
                if (zone != null && zone.isActiveAndEnabled && zone.Priority > priority && zone.Contains(service.Listener.position))
                { desired = zone.Cue; priority = zone.Priority; }
            if (!initialized)
            {
                initialized = true; current = desired; service.SetBed(current, false);
                if (playPlaceholderMusic && !(GetComponent<DynamicMusicDirector>() is { isActiveAndEnabled: true }))
                    service.SetBed(AudioCue.MusicPlaceholder, true);
            }
            gain = Mathf.MoveTowards(gain, desired == current ? 1 : 0, Time.unscaledDeltaTime * 2);
            if (gain <= 0 && desired != current) { current = desired; service.SetBed(current, false); }
            service.SetAmbienceGain(gain);
        }
    }
}
