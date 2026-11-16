using System;
using System.IO;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    public static class MusicSystemSetup
    {
        public const string CatalogPath = "Assets/Art/Audio/DragonlandMusicCatalog.asset";
        [Serializable] private sealed class Manifest { public float volume, crossfadeSeconds; public Recipe[] tracks; }
        [Serializable] private sealed class Recipe { public string state, clip; public bool loop; }
        private static void EditOnly() { if (Application.isPlaying) throw new InvalidOperationException("Run music setup in Edit Mode."); }
        private static bool Install(GameObject root, bool undo)
        {
            if (root.GetComponent<NetworkObject>() == null || root.GetComponent<NetworkCharacterStats>() == null || root.GetComponent<MusicContextSubject>() != null) return false;
            if (undo) Undo.AddComponent<MusicContextSubject>(root); else root.AddComponent<MusicContextSubject>();
            return true;
        }
        [MenuItem("Heroes of Dragonland/Audio/Build Dynamic Music Assets and Hooks (M61)")]
        public static void Build()
        {
            EditOnly(); AudioSystemSetup.Validate();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/Audio/music_manifest.json"));
            if (manifest?.tracks == null || manifest.tracks.Length != Enum.GetValues(typeof(MusicState)).Length)
                throw new InvalidOperationException("Incomplete M61 music manifest.");
            AssetDatabase.Refresh();
            foreach (var track in manifest.tracks)
            {
                var importer = AssetImporter.GetAtPath(track.clip) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Fetch/import music WAV first: " + track.clip);
                var settings = importer.defaultSampleSettings;
                if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.PCM && importer.forceToMono && !importer.loadInBackground) continue;
                settings.loadType = AudioClipLoadType.Streaming; settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.loadInBackground = false; importer.SaveAndReimport();
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MusicCatalog>(); var data = new SerializedObject(catalog);
                data.FindProperty("volume").floatValue = manifest.volume; data.FindProperty("crossfadeSeconds").floatValue = manifest.crossfadeSeconds;
                var tracks = data.FindProperty("tracks"); tracks.arraySize = manifest.tracks.Length;
                for (int i = 0; i < manifest.tracks.Length; i++)
                {
                    var source = manifest.tracks[i]; var track = tracks.GetArrayElementAtIndex(i);
                    track.FindPropertyRelative("state").intValue = (int)Enum.Parse<MusicState>(source.state);
                    track.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(source.clip);
                    track.FindPropertyRelative("loop").boolValue = source.loop;
                }
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (!catalog.Validate(out var error)) throw new InvalidOperationException(error);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path);
                try { if (Install(root, false)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable(); Validate();
            Debug.Log("[M61] Built/preserved ten-state music catalog and local context hooks. Install in loaded scene through MCP; Play/listening/host-client acceptance remain required.");
        }
        [MenuItem("Heroes of Dragonland/Audio/Install Dynamic Music in Loaded Scene (M61)")]
        public static void InstallLoadedScene()
        {
            EditOnly(); Validate(); var scene = SceneManager.GetActiveScene(); var roots = scene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(r => r.GetComponentsInChildren<MultiplayerBootstrap>(true)).Single();
            if (bootstrap.GetComponent<GameAudioService>() == null) throw new InvalidOperationException("Install M60 audio in this scene first.");
            if (bootstrap.GetComponent<LocalMusicContext>() == null) Undo.AddComponent<LocalMusicContext>(bootstrap.gameObject);
            var director = bootstrap.GetComponent<DynamicMusicDirector>() ?? Undo.AddComponent<DynamicMusicDirector>(bootstrap.gameObject);
            Undo.RecordObject(director, "M61 music catalog"); var data = new SerializedObject(director);
            data.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath); data.ApplyModifiedPropertiesWithoutUndo();
            var ambience = bootstrap.GetComponent<AudioAmbienceController>();
            if (ambience != null)
            {
                Undo.RecordObject(ambience, "M61 music ownership"); data = new SerializedObject(ambience);
                data.FindProperty("playPlaceholderMusic").boolValue = false; data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var actor in roots.SelectMany(r => r.GetComponentsInChildren<NetworkObject>(true))) Install(actor.gameObject, true);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[M61] Installed local music director/context. Music shares the M60 Music bus; no network state/scene layout change. Review Console/Hierarchy and save through MCP.");
        }
        [MenuItem("Heroes of Dragonland/Audio/Validate Dynamic Music (M61)")]
        public static void Validate()
        {
            AudioSystemSetup.Validate();
            var catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Build M61 music catalog first.");
            if (!catalog.Validate(out var error)) throw new InvalidOperationException(error);
            foreach (MusicState state in Enum.GetValues(typeof(MusicState)))
            {
                var track = catalog.Find(state);
                if (track.clip.length <= 0) throw new InvalidOperationException("Empty music clip: " + state);
                string path = AssetDatabase.GetAssetPath(track.clip);
                if (path.StartsWith("Assets/Art/Audio/MusicPlaceholders/", StringComparison.Ordinal)
                    && (track.clip.channels != 1 || track.clip.frequency != 22050 || track.clip.loadType != AudioClipLoadType.Streaming))
                    throw new InvalidOperationException("Placeholder music import mismatch: " + state);
            }
            Debug.Log("[M61] PASS ten-state catalog and import validation. Native fades, actual contexts, reconnect and listening still require Unity Play/host-client checks.");
        }
    }
}
