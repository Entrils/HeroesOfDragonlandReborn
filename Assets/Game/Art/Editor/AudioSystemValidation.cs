using System;
using System.Collections;
using System.Linq;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.Art.Editor
{
    public static class AudioSystemValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("M60: " + message); }
        [MenuItem("Heroes of Dragonland/Audio/Run Play Validation (M60)")]
        public static void Start()
        {
            Check(Application.isPlaying && !Application.isBatchMode, "Use non-headless Play Mode through MCP");
            Check(Status != "RUNNING", "Already running"); AudioSystemSetup.Validate();
            Status = "RUNNING";
            var host = new GameObject("Temporary M60 check").AddComponent<ArtPipelineValidationHost>();
            host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            GameObject fixture = null;
            AudioSettingsView view = null;
            AudioPreferences preferences = null;
            float[] original = null;
            try
            {
                var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
                Check(bootstrap != null, "Missing bootstrap");
                var service = bootstrap.GetComponent<GameAudioService>();
                Check(service != null && service.isActiveAndEnabled && service.Listener != null, "Missing/disabled audio service or listener");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l => l.isActiveAndEnabled) == 1, "Expected one active AudioListener");
                fixture = new GameObject("Temporary bounded audio pool"); fixture.SetActive(false);
                var pool = fixture.AddComponent<GameAudioService>(); var data = new SerializedObject(pool);
                data.FindProperty("catalog").objectReferenceValue = service.Catalog;
                data.FindProperty("voiceCount").intValue = 8;
                data.FindProperty("listener").objectReferenceValue = service.Listener; data.ApplyModifiedPropertiesWithoutUndo();
                fixture.SetActive(true); yield return null;
                var point = service.Listener.position;
                Check(pool.Play(AudioCue.Footstep, point, fixture), "Native source did not start");
                Check(!pool.Play(AudioCue.Footstep, point, fixture), "Duplicate footstep bypassed actor cooldown");
                Check(!pool.Play(AudioCue.Attack, point + Vector3.right * 1000, fixture), "Distant attack bypassed culling");
                Check(!pool.Play(AudioCue.VoicePlaceholder, point, fixture), "Empty voice placeholder played");
                foreach (var cue in new[] { AudioCue.Hit, AudioCue.Block, AudioCue.Parry, AudioCue.Death, AudioCue.Attack, AudioCue.EnemyAggro, AudioCue.EnemyAttack })
                    pool.Play(cue, point, fixture);
                Check(pool.ActiveVoices == pool.Capacity, "Could not fill bounded pool");
                Check(pool.Play(AudioCue.KingUnderAttack, point, fixture), "Warning could not replace a lower priority sound");
                for (int i = 0; i < 200; i++) pool.Play(AudioCue.Footstep, point, fixture);
                var sources = fixture.GetComponentsInChildren<AudioSource>();
                Check(sources.Length == pool.Capacity + 3 && pool.ActiveVoices <= pool.Capacity, "Burst created extra native sources");
                foreach (var source in sources) if (source.isPlaying)
                    Check(source.outputAudioMixerGroup != null && !source.loop, "Unrouted/looping one shot");
                pool.SetBed(AudioCue.ForestAmbience, false); pool.SetBed(AudioCue.MusicPlaceholder, true);
                Check(fixture.GetComponentsInChildren<AudioSource>().Length == sources.Length, "Beds allocated extra sources");
                preferences = bootstrap.GetComponent<AudioPreferences>(); view = bootstrap.GetComponentInChildren<AudioSettingsView>(true);
                var gate = bootstrap.GetComponent<LocalGameplayInputGate>();
                Check(preferences != null && view != null && gate != null, "Missing audio settings wiring");
                Check(!view.IsOpen && !gate.IsBlocked, "Close all gameplay menus/release controls before this validation");
                original = new[] { preferences.Get(0), preferences.Get(1), preferences.Get(2) };
                view.SetOpen(true); Check(gate.IsBlocked, "Settings did not block local combat input");
                var root = view.GetComponent<UIDocument>().rootVisualElement;
                string[] names = { "Master", "Music", "SFX" }, parameters = { "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb" };
                for (int i = 0; i < names.Length; i++)
                {
                    var slider = root.Q<Slider>("Audio" + names[i]); Check(slider != null, "Missing slider " + names[i]);
                    slider.value = i == 0 ? 0 : .3f;
                    Check(Mathf.Abs(preferences.Get(i) - slider.value) < .001f, "Slider did not update local preference");
                    Check(service.Catalog.Mixer.GetFloat(parameters[i], out var db) && Mathf.Abs(db - AudioMath.Decibels(slider.value)) < .01f, "Slider did not reach mixer");
                }
                view.SetOpen(false); yield return null;
                Check(!gate.IsBlocked, "Settings retained local input ownership");
                pool.enabled = false;
                foreach (var source in sources) Check(!source.isPlaying, "Disable retained native playback");
                Check(pool.ActiveVoices == 0, "Disable did not release voices");
                Status = "PASS local native pool, routing, culling, cooldown, priority, beds, UI/input and mixer checks. Actual host/client events, persistence across restart and listening remain pending.";
                Debug.Log("[M60] " + Status);
            }
            finally
            {
                if (original != null && preferences != null) { for (int i = 0; i < original.Length; i++) preferences.Set(i, original[i]); preferences.Save(); }
                if (view != null && original != null) view.SetOpen(false);
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
                if (Status == "RUNNING") { Status = "FAIL — inspect Console"; Debug.LogError("[M60] " + Status); }
                UnityEngine.Object.Destroy(host);
            }
        }
    }
}
