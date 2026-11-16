using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Audio/Music Catalog")]
    public sealed class MusicCatalog : ScriptableObject
    {
        [Serializable] public sealed class Track
        {
            public MusicState state;
            public AudioClip clip;
            public bool loop = true;
        }
        [SerializeField] private Track[] tracks;
        [SerializeField, Range(0, 1)] private float volume = .35f;
        [SerializeField, Range(.1f, 5)] private float crossfadeSeconds = 1.25f;
        [SerializeField] private MusicTiming timing = new MusicTiming();
        public float Volume => volume;
        public float CrossfadeSeconds => crossfadeSeconds;
        public MusicTiming Timing => timing;
        public Track Find(MusicState state) { if (tracks != null) foreach (var track in tracks) if (track != null && track.state == state) return track; return null; }
        public bool Validate(out string error)
        {
            error = null; var seen = new HashSet<MusicState>();
            if (timing == null || float.IsNaN(volume) || volume < 0 || volume > 1 || float.IsNaN(crossfadeSeconds) || crossfadeSeconds < .1f || crossfadeSeconds > 5)
            { error = "Invalid music mix/timing."; return false; }
            foreach (float value in new[] { timing.matchStartSeconds, timing.campRelease, timing.pvpRelease, timing.bossRelease, timing.fortressRelease, timing.kingRelease })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 30) { error = "Invalid music hold duration."; return false; }
            if (tracks != null) foreach (var track in tracks)
                if (track == null || !Enum.IsDefined(typeof(MusicState), track.state) || !seen.Add(track.state) || track.clip == null)
                { error = "Missing clip/invalid/duplicate music state."; return false; }
            foreach (MusicState state in Enum.GetValues(typeof(MusicState))) if (!seen.Contains(state)) { error = "Missing music state: " + state; return false; }
            return true;
        }
    }
}
