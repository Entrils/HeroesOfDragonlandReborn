using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HeroesOfDragonland.Audio
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Audio/Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Sound
        {
            public AudioCue cue;
            public AudioCategory category = AudioCategory.SFX;
            public AudioClip[] clips;
            [Range(0, 1)] public float volume = .65f;
            [Range(1, 100)] public int priority = 40;
            [Range(1, 8)] public int maximumConcurrent = 4;
            [Range(0, 10)] public float cooldown = .1f;
            public bool spatial = true, loop;
            [Range(0, .2f)] public float pitchVariation = .04f;
            [Min(.1f)] public float minDistance = 2;
            [Min(1)] public float maxDistance = 24;
        }
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup[] groups = new AudioMixerGroup[6];
        [SerializeField] private Sound[] sounds;
        public AudioMixer Mixer => mixer;
        public Sound Find(AudioCue cue)
        { if (sounds != null) foreach (var sound in sounds) if (sound != null && sound.cue == cue) return sound; return null; }
        public AudioMixerGroup Group(AudioCategory category) => groups != null && (int)category < groups.Length ? groups[(int)category] : null;
        public bool Validate(out string error)
        {
            error = null;
            if (mixer == null) { error = "Missing mixer."; return false; }
            foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
                if (Group(category) == null || Group(category).audioMixer != mixer || Group(category).name != category.ToString())
                { error = "Missing/mismatched mixer group: " + category; return false; }
            foreach (var parameter in new[] { "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb" })
                if (!mixer.GetFloat(parameter, out _)) { error = "Missing exposed parameter: " + parameter; return false; }
            var seen = new HashSet<AudioCue>();
            if (sounds != null) foreach (var sound in sounds)
            {
                if (sound == null || !seen.Add(sound.cue)) { error = "Null/duplicate audio cue."; return false; }
                if (sound.category == AudioCategory.Master || !Enum.IsDefined(typeof(AudioCategory), sound.category)
                    || sound.maximumConcurrent < 1 || sound.maximumConcurrent > 8 || sound.priority < 1 || sound.priority > 100
                    || sound.maxDistance < sound.minDistance || sound.minDistance <= 0 || sound.cooldown < 0 || sound.cooldown > 10
                    || float.IsNaN(sound.volume) || sound.volume < 0 || sound.volume > 1)
                { error = "Invalid audio recipe: " + sound.cue; return false; }
                if (sound.cue == AudioCue.VoicePlaceholder) continue;
                if (sound.clips == null || sound.clips.Length == 0 || Array.Exists(sound.clips, clip => clip == null))
                { error = "Missing clips: " + sound.cue; return false; }
            }
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
                if (!seen.Contains(cue)) { error = "Missing cue: " + cue; return false; }
            return true;
        }
    }
}
