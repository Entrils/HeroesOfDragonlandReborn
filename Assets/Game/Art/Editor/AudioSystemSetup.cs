using System;
using System.IO;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.Art.Editor
{
    public static class AudioSystemSetup
    {
        public const string Root = "Assets/Art/Audio";
        public const string CatalogPath = Root + "/DragonlandAudioCatalog.asset";
        [Serializable] private sealed class Manifest { public Recipe[] sounds; }
        [Serializable] private sealed class Recipe
        {
            public string cue, category;
            public string[] clips;
            public bool spatial, loop;
            public float volume, cooldown, pitchVariation, minDistance, maxDistance;
            public int priority, maximumConcurrent;
        }
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        { var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void EditOnly() { if (Application.isPlaying) throw new InvalidOperationException("Run audio setup in Edit Mode."); }
        [MenuItem("Heroes of Dragonland/Audio/Build Assets and Prefab Hooks (M60)")]
        public static void Build()
        {
            EditOnly();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/Audio/audio_manifest.json"));
            if (manifest?.sounds == null || manifest.sounds.Length != Enum.GetValues(typeof(AudioCue)).Length)
                throw new InvalidOperationException("Missing/incomplete M60 audio manifest.");
            foreach (var recipe in manifest.sounds) foreach (var clip in recipe.clips)
                if (!File.Exists(clip)) throw new InvalidOperationException("Missing audio source: " + clip + ". Fetch Git LFS objects first.");
            AssetDatabase.Refresh();
            foreach (var recipe in manifest.sounds) foreach (var path in recipe.clips)
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Audio import failed: " + path);
                bool bed = recipe.loop;
                var settings = importer.defaultSampleSettings;
                var load = bed ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType == load && settings.compressionFormat == AudioCompressionFormat.PCM && importer.forceToMono && !importer.loadInBackground) continue;
                settings.loadType = load; settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.forceToMono = true; importer.loadInBackground = false; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
            var mixer = AudioMixerFactory.Ensure(Root + "/DragonlandAudio.mixer");
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                var data = new SerializedObject(catalog); data.FindProperty("mixer").objectReferenceValue = mixer;
                var groups = data.FindProperty("groups"); groups.arraySize = 6;
                foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
                    groups.GetArrayElementAtIndex((int)category).objectReferenceValue = mixer.FindMatchingGroups("").Single(g => g.name == category.ToString());
                var sounds = data.FindProperty("sounds"); sounds.arraySize = manifest.sounds.Length;
                for (int i = 0; i < manifest.sounds.Length; i++)
                {
                    var recipe = manifest.sounds[i]; var sound = sounds.GetArrayElementAtIndex(i);
                    sound.FindPropertyRelative("cue").intValue = (int)Enum.Parse<AudioCue>(recipe.cue);
                    sound.FindPropertyRelative("category").intValue = (int)Enum.Parse<AudioCategory>(recipe.category);
                    foreach (var name in new[] { "volume", "cooldown", "pitchVariation", "minDistance", "maxDistance" })
                        sound.FindPropertyRelative(name).floatValue = (float)typeof(Recipe).GetField(name).GetValue(recipe);
                    sound.FindPropertyRelative("priority").intValue = recipe.priority;
                    sound.FindPropertyRelative("maximumConcurrent").intValue = recipe.maximumConcurrent;
                    sound.FindPropertyRelative("spatial").boolValue = recipe.spatial; sound.FindPropertyRelative("loop").boolValue = recipe.loop;
                    var clips = sound.FindPropertyRelative("clips"); clips.arraySize = recipe.clips.Length;
                    for (int c = 0; c < recipe.clips.Length; c++) clips.GetArrayElementAtIndex(c).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(recipe.clips[c]);
                }
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            // Preserve reviewed catalogs and existing metadata; patch only missing adapters.
            if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path);
                try { if (Install(root, false)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            Validate();
            Debug.Log("[M60] Audio assets/prefab hooks prepared. Install in the intended scene through MCP, save, then run Play and host/client tests.");
        }
        private static bool Install(GameObject root, bool undo)
        {
            if (root.GetComponent<NetworkObject>() == null) return false;
            bool changed = false;
            void Add<T>() where T : Component
            { if (root.GetComponent<T>() != null) return; changed = true; if (undo) Undo.AddComponent<T>(root); else root.AddComponent<T>(); }
            if (root.GetComponent<NetworkCharacterStats>() != null) Add<ActorAudioPresentation>();
            if (root.GetComponent<NetworkCamp>() != null || root.GetComponent<NetworkLootChest>() != null || root.GetComponent<NetworkMatch>() != null) Add<WorldAudioPresentation>();
            return changed;
        }
        [MenuItem("Heroes of Dragonland/Audio/Install in Loaded Scene (M60)")]
        public static void InstallLoadedScene()
        {
            EditOnly(); Validate();
            var scene = SceneManager.GetActiveScene(); var roots = scene.GetRootGameObjects();
            T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            var bootstrap = All<MultiplayerBootstrap>().Single();
            var panel = All<UIDocument>().FirstOrDefault(d => d.panelSettings != null)?.panelSettings;
            if (panel == null) throw new InvalidOperationException("Install the existing UI Toolkit HUD first; reuse its PanelSettings.");
            var cameras = All<Camera>().Where(c => c.isActiveAndEnabled && c.CompareTag("MainCamera")).ToArray();
            if (cameras.Length != 1) throw new InvalidOperationException("Expected one active MainCamera. Inspect Hierarchy through MCP.");
            var listeners = All<AudioListener>().Where(l => l.isActiveAndEnabled).ToArray();
            if (listeners.Length > 1 || (listeners.Length == 1 && listeners[0].gameObject != cameras[0].gameObject))
                throw new InvalidOperationException("Resolve duplicate/non-camera AudioListeners through MCP before audio installation.");
            T Ensure<T>(GameObject target) where T : Component => target.GetComponent<T>() ?? Undo.AddComponent<T>(target);
            var listener = Ensure<AudioListener>(cameras[0].gameObject); Undo.RecordObject(listener, "M60 listener"); listener.enabled = true;
            var service = Ensure<GameAudioService>(bootstrap.gameObject);
            Undo.RecordObject(service, "M60 audio service"); Set(service, "catalog", AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath)); Set(service, "listener", cameras[0].transform);
            var prefs = Ensure<AudioPreferences>(bootstrap.gameObject);
            var ambience = Ensure<AudioAmbienceController>(bootstrap.gameObject);
            var gate = Ensure<LocalGameplayInputGate>(bootstrap.gameObject);
            foreach (var area in All<TeamSpawnArea>())
            {
                // Follow existing fortress spawn anchors, preserving all gameplay footprints.
                if (area.GetComponentInChildren<AudioAmbienceZone>(true) != null) continue;
                var zone = new GameObject("M60 Fortress ambience"); Undo.RegisterCreatedObjectUndo(zone, "M60 zone"); zone.transform.SetParent(area.transform, false);
                Undo.AddComponent<AudioAmbienceZone>(zone);
            }
            Undo.RecordObject(ambience, "M60 ambience zones");
            var data = new SerializedObject(ambience); var zones = All<AudioAmbienceZone>(); var list = data.FindProperty("zones"); list.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = zones[i]; data.ApplyModifiedPropertiesWithoutUndo();
            var ui = All<AudioSettingsView>().SingleOrDefault();
            if (ui == null)
            {
                var obj = new GameObject("Audio Settings UI"); Undo.RegisterCreatedObjectUndo(obj, "M60 UI"); obj.transform.SetParent(bootstrap.transform, false);
                var document = Undo.AddComponent<UIDocument>(obj); document.panelSettings = panel;
                document.sortingOrder = All<UIDocument>().Where(d => d != document).Select(d => d.sortingOrder).DefaultIfEmpty(0).Max() + 10;
                ui = Undo.AddComponent<AudioSettingsView>(obj);
            }
            Undo.RecordObject(ui, "M60 settings references"); Set(ui, "preferences", prefs); Set(ui, "audioService", service); Set(ui, "inputGate", gate);
            foreach (var obj in All<NetworkObject>()) Install(obj.gameObject, true);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[M60] Installed local audio, settings and fortress ambience at existing anchors. Review zones/UI/listener, Console and save through MCP. No gameplay layout changed.");
        }
        [MenuItem("Heroes of Dragonland/Audio/Validate Assets (M60)")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Build the M60 catalog first.");
            if (!catalog.Validate(out var error)) throw new InvalidOperationException(error);
            AudioMixerFactory.ValidateVolumeBindings(catalog.Mixer);
            // Validate routing, not just matching display names.
            var master = catalog.Group(AudioCategory.Master);
            var children = master.GetType().GetProperty("children", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.GetValue(master) as Array;
            if (children == null) throw new InvalidOperationException("Cannot inspect mixer hierarchy on this Unity version; verify routing through MCP.");
            foreach (var category in new[] { AudioCategory.Music, AudioCategory.SFX, AudioCategory.UI, AudioCategory.Ambience, AudioCategory.Voice })
                if (!children.Cast<AudioMixerGroup>().Contains(catalog.Group(category))) throw new InvalidOperationException("Group must route directly to Master: " + category);
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                var sound = catalog.Find(cue);
                if (cue == AudioCue.VoicePlaceholder) continue;
                foreach (var clip in sound.clips)
                    if (clip.length <= 0 || (sound.spatial && clip.channels != 1)
                        || (AssetDatabase.GetAssetPath(clip).StartsWith(Root + "/Placeholders/", StringComparison.Ordinal) && (clip.channels != 1 || clip.frequency != 22050)))
                        throw new InvalidOperationException("Unexpected placeholder format: " + clip.name);
            }
            Debug.Log("[M60] PASS catalog/groups/hierarchy/placeholder formats. Playback, mixer persistence, UI and network delivery still require Play/host-client tests.");
        }
    }
}
