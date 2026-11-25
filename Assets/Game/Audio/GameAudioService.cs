using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    // One pool per bootstrap/client. Call sites never create AudioSources or networking objects.
    [DisallowMultipleComponent]
    public sealed class GameAudioService : MonoBehaviour
    {
        [SerializeField] private AudioCatalog catalog;
        [SerializeField, Range(8, 32)] private int voiceCount = 24;
        [SerializeField] private Transform listener;
        private AudioSource[] voices;
        private readonly System.Random variation = new System.Random();
        private AudioVoiceBudget budget;
        private int[] generations;
        private AudioSource music, musicB, ambience;
        private readonly MusicDecks<AudioClip> musicDecks = new MusicDecks<AudioClip>();
        private readonly int[] musicRevisions = { -1, -1 };
        private float musicGain = .35f;
        private AudioCue ambienceCue = AudioCue.ForestAmbience;
        private bool warned;
        public AudioCatalog Catalog => catalog;
        public Transform Listener => listener;
        public int ActiveVoices => budget?.ActiveCount ?? 0;
        public int Capacity => budget?.Capacity ?? 0;
        public bool MusicReady => isActiveAndEnabled && music != null && musicB != null;
        public bool MusicTransitioning => musicDecks.Fading;
        public int MusicStarts { get; private set; }
        public int PlaybackGeneration { get; private set; }
        public event System.Action<AudioCue> CuePlayed;
        private void OnEnable() => PlaybackGeneration++;
        private void Awake()
        {
            if (Application.isBatchMode) { enabled = false; return; }
            if (catalog == null || !catalog.Validate(out _))
            { Debug.LogError("[M60] Complete and validate the audio catalog before enabling playback.", this); enabled = false; return; }
            voiceCount = Mathf.Clamp(voiceCount, 8, 32);
            budget = new AudioVoiceBudget(voiceCount); voices = new AudioSource[voiceCount]; generations = new int[voiceCount];
            for (int i = 0; i < voiceCount; i++) voices[i] = Source("One shot " + i);
            music = Source("Music A"); musicB = Source("Music B"); ambience = Source("Ambience");
        }
        private AudioSource Source(string label)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }
        private void Configure(AudioSource source, AudioCatalog.Sound sound, Vector3 point)
        {
            source.Stop(); source.clip = sound.clips[variation.Next(sound.clips.Length)];
            source.transform.position = point; source.outputAudioMixerGroup = catalog.Group(sound.category);
            source.spatialBlend = sound.spatial ? 1 : 0; source.minDistance = sound.minDistance; source.maxDistance = sound.maxDistance;
            source.volume = sound.volume; source.pitch = 1 + ((float)variation.NextDouble() * 2 - 1) * sound.pitchVariation;
            source.priority = 256 - Mathf.Clamp(sound.priority * 2, 1, 255); source.loop = sound.loop;
        }
        public bool Play(AudioCue cue, Vector3 point, Object owner = null)
            => PlayImpact(cue, point, owner, 1);

        internal bool PlayImpact(AudioCue cue, Vector3 point, Object owner, float pitch)
        {
            if (!isActiveAndEnabled || budget == null) return false;
            var sound = catalog.Find(cue);
            if (sound == null || sound.loop || sound.clips == null || sound.clips.Length == 0) return false;
            // Unity rolloff can retain a quiet tail beyond maxDistance: reject inaudible requests explicitly.
            if (sound.spatial && (listener == null || (listener.position - point).sqrMagnitude > sound.maxDistance * sound.maxDistance)) return false;
            Reclaim();
            int index = budget.Acquire((int)cue, owner != null ? unchecked((long)EntityId.ToULong(owner.GetEntityId())) : 0, sound.priority,
                sound.maximumConcurrent, Time.unscaledTimeAsDouble, sound.cooldown, out int generation);
            if (index < 0) return false;
            var source = voices[index];
            if (source == null) { budget.Release(index, generation); return false; }
            generations[index] = generation;
            Configure(source, sound, point);
            source.pitch *= Mathf.Clamp(pitch, .65f, 1.4f);
            source.Play();
            try { CuePlayed?.Invoke(cue); } catch (System.Exception error) { Debug.LogException(error, this); }
            return true;
        }
        private void Reclaim()
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++) if (voices[i] == null || !voices[i].isPlaying) budget.Release(i, generations[i]);
        }
        private void Update()
        {
            Reclaim();
            musicDecks.Tick(Time.unscaledDeltaTime); ApplyMusic();
            if (listener == null)
            {
                var camera = Camera.main;
                if (camera != null && camera.GetComponent<AudioListener>() is { enabled: true }) listener = camera.transform;
                else if (!warned) { warned = true; Debug.LogWarning("[M60] Assign the one active camera AudioListener; spatial cues remain muted until available.", this); }
            }
        }
        public void SetBed(AudioCue cue, bool musicBed)
        {
            if (!isActiveAndEnabled || budget == null) return;
            var sound = catalog.Find(cue);
            if (sound == null || !sound.loop || sound.spatial || sound.clips == null || sound.clips.Length == 0
                || sound.category != (musicBed ? AudioCategory.Music : AudioCategory.Ambience)) return;
            if (musicBed) { TransitionMusic(sound.clips[0], true, .5f, sound.volume); return; }
            if (!musicBed) ambienceCue = cue;
            Configure(ambience, sound, transform.position); ambience.pitch = 1; ambience.Play();
        }
        public void TransitionMusic(AudioClip clip, bool loop, float seconds, float gain)
        {
            if (!MusicReady) return;
            musicGain = AudioMath.Sanitize(gain); musicDecks.Request(clip, loop, seconds); ApplyMusic();
        }
        public void StopMusic(float seconds = .5f) { if (MusicReady) { musicDecks.Request(null, false, seconds); ApplyMusic(); } }
        private void ApplyMusic()
        {
            if (!MusicReady) return;
            for (int i = 0; i < 2; i++)
            {
                var source = i == 0 ? music : musicB; var clip = musicDecks.Clip(i);
                if (clip == null)
                { if (source.clip != null) { source.Stop(); source.clip = null; } continue; }
                if (source.clip != clip || musicRevisions[i] != musicDecks.Revision(i))
                {
                    source.Stop(); source.clip = clip; source.loop = musicDecks.Loop(i);
                    source.outputAudioMixerGroup = catalog.Group(AudioCategory.Music);
                    source.spatialBlend = 0; source.pitch = 1; source.priority = 128;
                    source.volume = musicDecks.Gain(i) * musicGain;
                    source.Play(); MusicStarts++; musicRevisions[i] = musicDecks.Revision(i);
                }
                else source.loop = musicDecks.Loop(i);
                source.volume = musicDecks.Gain(i) * musicGain;
                // Natural completion of a non-looping result/intro is deliberately not restarted.
            }
        }
        public void SetAmbienceGain(float gain) { if (ambience != null) ambience.volume = catalog.Find(ambienceCue).volume * Mathf.Clamp01(gain); }
        private void OnDisable()
        {
            if (voices != null) for (int i = 0; i < voices.Length; i++) { if (voices[i] != null) voices[i].Stop(); budget.Release(i, generations[i]); }
            musicDecks.Reset(); PlaybackGeneration++;
            if (music != null) { music.Stop(); music.clip = null; }
            if (musicB != null) { musicB.Stop(); musicB.clip = null; }
            if (ambience != null) ambience.Stop();
        }
    }
}
