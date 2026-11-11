using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.World.Editor
{
    public static class HudLayoutValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string message) { if (!value) throw new Exception("HUD layout: " + message); }
        public static void Setup()
        {
            Check(!Application.isPlaying, "Use Edit Mode");
            foreach (var settings in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Select(d => d.panelSettings).Distinct())
            {
                Undo.RecordObject(settings, "Fit UI to screen");
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1280, 720);
                settings.screenMatchMode = PanelScreenMatchMode.Expand;
                EditorUtility.SetDirty(settings);
            }
            var camera = UnityEngine.Object.FindFirstObjectByType<BattlefieldCamera>();
            if (camera.GetComponent<BattlefieldLabelVisibility>() == null) Undo.AddComponent<BattlefieldLabelVisibility>(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveScene(camera.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void StartChecks()
        {
            Check(Application.isPlaying, "Use Play Mode"); Status = "RUNNING";
            UnityEngine.Object.FindFirstObjectByType<HeroesOfDragonland.Multiplayer.MultiplayerBootstrap>().StartCoroutine(Run());
        }
        public static void CaptureGame()
        {
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).Focus();
            UnityEngine.Object.FindFirstObjectByType<HeroesOfDragonland.Multiplayer.MultiplayerBootstrap>().StartCoroutine(CaptureFrame());
        }
        private static IEnumerator CaptureFrame()
        {
            yield return null; yield return new WaitForEndOfFrame();
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            try { System.IO.File.WriteAllBytes("Builds/hud-in-game.png", pixels.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static void Bounds(VisualElement element, Rect viewport)
        {
            if (element == null || element.resolvedStyle.display == DisplayStyle.None) return;
            Rect bounds = element.worldBound;
            Check(bounds.xMin >= viewport.xMin - 1 && bounds.yMin >= viewport.yMin - 1
                && bounds.xMax <= viewport.xMax + 1 && bounds.yMax <= viewport.yMax + 1,
                element.name + " outside screen " + bounds + " viewport=" + viewport);
        }
        private static IEnumerator Run()
        {
            var documents = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            var settings = documents.Select(d => d.panelSettings).Distinct().ToArray();
            var originals = settings.Select(s => s.targetTexture).ToArray();
            RenderTexture texture = null;
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            try
            {
                view.SetTarget(true); view.SetTargetName("Neutral Stone Warden - contested boss"); view.SetTargetHealth(1500, 1800);
                foreach (var size in new[] { new Vector2Int(960,540), new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(1024,768), new Vector2Int(2560,1080) })
                {
                    texture = new RenderTexture(size.x, size.y, 24); texture.Create();
                    foreach (var panel in settings) panel.targetTexture = texture;
                    yield return null; yield return null; yield return null;
                    var root = view.GetComponent<UIDocument>().rootVisualElement;
                    Rect viewport = root.worldBound;
                    Check(viewport.width >= 1279 && viewport.height >= 719, "Fit scaling keeps reference layout visible " + size + " actual=" + viewport);
                    foreach (string name in new[] { "Player", "Match", "Target", "Notice", "Controls" }) Bounds(root.Q(name), viewport);
                    Check(!root.Q("Player").worldBound.Overlaps(root.Q("Match").worldBound), "Player and match overlap");
                    Check(!root.Q("Target").worldBound.Overlaps(root.Q("Controls").worldBound), "Target and controls overlap");
                    foreach (var doc in documents)
                        foreach (var button in doc.rootVisualElement.Query<Button>().ToList())
                            if (button.resolvedStyle.display != DisplayStyle.None && button.worldBound.width > 0) Bounds(button, doc.rootVisualElement.worldBound);
                    var active = RenderTexture.active; var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,size.x,size.y),0,0); pixels.Apply();
                        System.IO.File.WriteAllBytes("Builds/hud-layout-" + size.x + "x" + size.y + ".png", pixels.EncodeToPNG());
                    }
                    finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(pixels); }
                    foreach (var panel in settings) panel.targetTexture = null;
                    texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null;
                    Debug.Log("[HudLayout] PASS " + size);
                }
                Status = "PASS: HUD bounds and non-overlap at 960x540, 1280x720, 1920x1080, 1024x768 and 2560x1080.";
            }
            finally
            {
                for (int i = 0; i < settings.Length; i++) settings[i].targetTexture = originals[i];
                if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
                view.SetTarget(false);
                if (Status == "RUNNING") Status = "FAILED; see Console";
            }
        }
    }
}
