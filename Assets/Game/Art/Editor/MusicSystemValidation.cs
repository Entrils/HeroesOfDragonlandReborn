using System;
using System.Collections;
using System.Linq;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class MusicSystemValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("M61: " + message); }
        [MenuItem("Heroes of Dragonland/Audio/Run Dynamic Music Play Validation (M61)")]
        public static void Start()
        {
            Check(Application.isPlaying && !Application.isBatchMode, "Use non-headless Play Mode through MCP");
            Check(Status != "RUNNING", "Already running"); MusicSystemSetup.Validate(); Status = "RUNNING";
            var host = new GameObject("Temporary M61 check").AddComponent<ArtPipelineValidationHost>();
            host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            GameObject fixture = null;
            try
            {
                var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
                Check(bootstrap != null, "Missing bootstrap");
                var main = bootstrap.GetComponent<GameAudioService>(); var director = bootstrap.GetComponent<DynamicMusicDirector>();
                Check(main != null && main.MusicReady && director != null && director.Catalog != null, "Missing M60/M61 scene installation");
                Check(bootstrap.GetComponent<LocalMusicContext>() != null, "Missing local context sampler");
                foreach (var obj in UnityEngine.Object.FindObjectsByType<Fusion.NetworkObject>())
                    if (obj.GetComponent<NetworkCharacterStats>() != null) Check(obj.GetComponent<MusicContextSubject>() != null, "Missing context hook: " + obj.name);
                fixture = new GameObject("Temporary music decks"); fixture.SetActive(false);
                var service = fixture.AddComponent<GameAudioService>(); var data = new SerializedObject(service);
                data.FindProperty("catalog").objectReferenceValue = main.Catalog;
                data.FindProperty("voiceCount").intValue = 8; data.FindProperty("listener").objectReferenceValue = main.Listener;
                data.ApplyModifiedPropertiesWithoutUndo(); fixture.SetActive(true); yield return null;
                var tracks = director.Catalog;
                var a = tracks.Find(MusicState.Exploration).clip; var b = tracks.Find(MusicState.PvPCombat).clip;
                var c = tracks.Find(MusicState.BossEncounter).clip;
                service.TransitionMusic(a, true, .4f, .2f); yield return new WaitForSecondsRealtime(.5f);
                int starts = service.MusicStarts;
                for (int i = 0; i < 30; i++) service.TransitionMusic(a, true, .4f, .2f);
                Check(service.MusicStarts == starts, "Same-track requests restarted native playback");
                var sources = fixture.GetComponentsInChildren<AudioSource>();
                Check(sources.Length == service.Capacity + 3, "Music allocated beyond two decks plus ambience");
                var old = sources.Single(s => s.clip == a); int cursor = old.timeSamples;
                yield return new WaitForSecondsRealtime(.1f);
                Check(old.timeSamples > cursor, "Native music cursor did not advance");
                service.TransitionMusic(b, true, 1, .2f); yield return new WaitForSecondsRealtime(.2f);
                var incoming = sources.Single(s => s.clip == b);
                Check(old.isPlaying && incoming.isPlaying && old.volume > 0 && incoming.volume > 0, "No audible native crossfade overlap");
                Check(Mathf.Abs(old.volume + incoming.volume - .2f) < .01f, "Crossfade exceeded mix gain");
                service.TransitionMusic(a, true, .2f, .2f); int reverseStarts = service.MusicStarts;
                yield return new WaitForSecondsRealtime(.3f);
                Check(service.MusicStarts == reverseStarts && old.clip == a && old.isPlaying, "Reversal restarted outgoing track");
                service.TransitionMusic(b, true, .4f, .2f);
                service.TransitionMusic(c, true, .4f, .2f);
                Check(!sources.Any(s => s.clip == c), "Pending third clip replaced an audible deck");
                yield return new WaitForSecondsRealtime(1);
                Check(sources.Count(s => s.isPlaying) == 1 && sources.Any(s => s.clip == c && s.isPlaying), "Queued transition did not settle on newest state");
                foreach (var source in sources.Where(s => s.clip != null))
                    Check(source.outputAudioMixerGroup == main.Catalog.Group(AudioCategory.Music) && source.spatialBlend == 0, "Music escaped local Music mixer bus");
                service.StopMusic(.1f); yield return new WaitForSecondsRealtime(.2f);
                Check(!sources.Any(s => s.isPlaying), "Graceful stop retained playback");
                service.TransitionMusic(a, false, .1f, .2f); yield return new WaitForSecondsRealtime(.2f);
                starts = service.MusicStarts;
                // Emulate native end without waiting for a future full-length production track.
                foreach (var source in sources.Where(s => s.clip == a)) source.Stop();
                service.TransitionMusic(a, false, .1f, .2f); yield return null;
                Check(service.MusicStarts == starts && !sources.Any(s => s.isPlaying), "Completed non-loop track restarted on repeated context");
                service.enabled = false;
                Check(!sources.Any(s => s.isPlaying), "Disable retained music");
                Status = "PASS native no-restart/cursor, overlap/gain, reversal, latest queued transition, fixed source count, Music routing, stop and non-loop lifecycle. Actual context, network isolation and listening acceptance remain pending.";
                Debug.Log("[M61] " + Status);
            }
            finally
            {
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
                if (Status == "RUNNING") { Status = "FAIL — inspect Console"; Debug.LogError("[M61] " + Status); }
                UnityEngine.Object.Destroy(host);
            }
        }
    }
}
