using System;
using HeroesOfDragonland.Profiles;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(GameAudioService))]
    public sealed class AudioPreferences : MonoBehaviour
    {
        private GameAudioService service;
        private readonly float[] levels = { .8f, .65f, .85f };
        private static readonly string[] Parameters = { "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb" };
        private bool started, dirty;
        public event Action Changed;
        public float Get(int index) => levels[Mathf.Clamp(index, 0, 2)];
        private void Awake()
        {
            service = GetComponent<GameAudioService>();
            for (int i = 0; i < levels.Length; i++) levels[i] = LocalPlayerProfile.Current.Data.Settings.Volume(i);
        }
        // SetFloat must run after mixer initialization, not in Awake/OnEnable.
        private void Start() { started = true; Apply(); }
        public void Set(int index, float value)
        {
            if (index < 0 || index >= levels.Length) return;
            levels[index] = AudioMath.Sanitize(value);
            LocalPlayerProfile.Current.Data.Settings.SetVolume(index, levels[index]);
            dirty = true; if (started) Apply(); Changed?.Invoke();
        }
        private void Apply()
        {
            var mixer = service.Catalog != null ? service.Catalog.Mixer : null;
            if (mixer == null) return;
            for (int i = 0; i < levels.Length; i++)
                if (!mixer.SetFloat(Parameters[i], AudioMath.Decibels(levels[i]))) Debug.LogError("[M60] Mixer parameter missing: " + Parameters[i], this);
        }
        public void Save() { if (dirty) { LocalPlayerProfile.Current.Save(); dirty = false; } }
        private void OnApplicationFocus(bool focus) { if (!focus) Save(); }
        private void OnApplicationQuit() => Save();
        private void OnDisable() => Save();
    }
}
