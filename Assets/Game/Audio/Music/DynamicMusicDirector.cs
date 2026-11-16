using System;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [DefaultExecutionOrder(250), DisallowMultipleComponent, RequireComponent(typeof(GameAudioService), typeof(LocalMusicContext))]
    public sealed class DynamicMusicDirector : MonoBehaviour
    {
        [SerializeField] private MusicCatalog catalog;
        private GameAudioService audioService;
        private LocalMusicContext context;
        private readonly MusicPolicy policy = new MusicPolicy();
        private float nextSample;
        private int generation = -1;
        private bool applied;
        public MusicCatalog Catalog => catalog;
        public MusicState State { get; private set; } = MusicState.Lobby;
        public event Action<MusicState> StateChanged;
        private void Awake()
        {
            audioService = GetComponent<GameAudioService>(); context = GetComponent<LocalMusicContext>();
            if (catalog == null || !catalog.Validate(out _))
            { Debug.LogError("[M61] Complete and validate the music catalog before enabling the director.", this); enabled = false; }
        }
        private void OnEnable() { policy.Reset(); applied = false; nextSample = 0; }
        // Local presentation hook for future encounter/zone systems. Phase and local-life gates still apply.
        public void Signal(MusicState state, float seconds = 2) => policy.Signal(state, Time.unscaledTimeAsDouble, seconds);
        private void LateUpdate()
        {
            if (Application.isBatchMode || !audioService.MusicReady) { applied = false; return; }
            if (Time.unscaledTime < nextSample && generation == audioService.PlaybackGeneration) return;
            nextSample = Time.unscaledTime + .2f;
            if (catalog == null) return;
            var next = policy.Evaluate(context.Capture(Time.unscaledTimeAsDouble), Time.unscaledTimeAsDouble, catalog.Timing);
            if (applied && next == State && generation == audioService.PlaybackGeneration) return;
            bool changed = !applied || State != next; State = next;
            var track = catalog.Find(next);
            if (track == null || track.clip == null) { audioService.StopMusic(catalog.CrossfadeSeconds); applied = false; return; }
            audioService.TransitionMusic(track.clip, track.loop, catalog.CrossfadeSeconds, catalog.Volume);
            generation = audioService.PlaybackGeneration; applied = true;
            if (changed && StateChanged != null) foreach (Action<MusicState> handler in StateChanged.GetInvocationList())
                try { handler(State); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private void OnDisable()
        {
            policy.Reset(); applied = false;
            if (audioService != null) audioService.StopMusic();
        }
    }
}
