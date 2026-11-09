using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.World.Editor
{
    public static class GameplayHudSceneBuilder
    {
        [MenuItem("Dragonland/Install Gameplay HUD")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            const string panelPath = "Assets/Game/Data/MultiplayerFortress/GameplayHudPanel.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1280, 720);
                settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match = .5f;
                AssetDatabase.CreateAsset(settings, panelPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Game/UI/GameplayHudTheme.tss");
            if (settings.themeStyleSheet == null) throw new InvalidOperationException("Missing HUD theme.");
            EditorUtility.SetDirty(settings);
            const string scenePath = "Assets/Scenes/MultiplayerFortressTest.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                MultiplayerBootstrap bootstrap = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent<MultiplayerBootstrap>(out var candidate)) bootstrap = candidate;
                if (bootstrap == null) throw new InvalidOperationException("Missing fortress bootstrap.");
                var hud = bootstrap.GetComponentInChildren<GameplayHudPresenter>(true);
                if (hud == null)
                {
                    var go = new GameObject("Gameplay HUD");
                    go.transform.SetParent(bootstrap.transform, false);
                    go.AddComponent<UIDocument>().panelSettings = settings;
                    go.AddComponent<GameplayHudView>();
                    hud = go.AddComponent<GameplayHudPresenter>();
                }
                hud.GetComponent<UIDocument>().panelSettings = settings;
                if (hud.GetComponent<GameplayHudAcceptanceProbe>() == null) hud.gameObject.AddComponent<GameplayHudAcceptanceProbe>();
                var data = new SerializedObject(hud);
                data.FindProperty("bootstrap").objectReferenceValue = bootstrap;
                data.ApplyModifiedPropertiesWithoutUndo();
                var old = bootstrap.GetComponent<NetworkFortressPresentation>();
                if (old != null) old.enabled = false;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("HUD scene save failed.");
                AssetDatabase.SaveAssets();
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
