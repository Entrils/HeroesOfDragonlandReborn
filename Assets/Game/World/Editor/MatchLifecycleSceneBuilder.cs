using System;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class MatchLifecycleSceneBuilder
    {
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            string folder = MultiplayerFortressSceneBuilder.Folder;
            var config = AssetDatabase.LoadAssetAtPath<MatchLifecycleConfig>(folder + "/MatchLifecycle.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MatchLifecycleConfig>();
                AssetDatabase.CreateAsset(config, folder + "/MatchLifecycle.asset");
            }
            string path = folder + "/NetworkMatch.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(prefab.GetComponent<NetworkMatch>());
                data.FindProperty("lifecycle").objectReferenceValue = config;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene(MultiplayerFortressSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<NetworkFortressSpawner>() != null)
                    {
                        if (root.GetComponent<NetworkRoundReset>() == null) root.AddComponent<NetworkRoundReset>();
                        if (root.GetComponent<MultiplayerLifecycleProbe>() == null) root.AddComponent<MultiplayerLifecycleProbe>();
                    }
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save match lifecycle scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            MultiplayerFortressSceneBuilder.Register();
            AssetDatabase.SaveAssets();
        }
    }
}
