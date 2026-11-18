using HeroesOfDragonland.Audio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent]
    public sealed class PlaytestSettings : MonoBehaviour
    {
        private const string Prefix = "HOD.Playtest.";
        private static readonly string[] VolumeNames = { "Master volume", "Music volume", "Effects volume" };
        [SerializeField] private AudioPreferences audio;
        private UniversalRenderPipelineAsset graphics;
        private RenderPipelineAsset previousPipeline;
        private bool fullscreen, vsync, reducedResolution;
        private const string MouseKey = Prefix + "MouseMultiplier";
        private static float? savedMouse;
        public static float MouseMultiplier => savedMouse ??= LoadMouse();

        private static float LoadMouse()
        {
            float value = PlayerPrefs.GetFloat(MouseKey, 1);
            return float.IsNaN(value) ? 1 : Mathf.Clamp(value, .25f, 3);
        }

        private void Start()
        {
            if (Application.isBatchMode) return;
            savedMouse = LoadMouse();
            fullscreen = PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            vsync = PlayerPrefs.GetInt(Prefix + "VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
            reducedResolution = PlayerPrefs.GetInt(Prefix + "ReducedResolution", 0) != 0;
            ApplyGraphics();
        }

        public void Draw()
        {
            if (audio != null)
                for (int i = 0; i < VolumeNames.Length; i++)
                {
                    float current = audio.Get(i);
                    GUILayout.Label(VolumeNames[i] + "  " + Mathf.RoundToInt(current * 100) + "%");
                    float next = GUILayout.HorizontalSlider(current, 0, 1);
                    if (!Mathf.Approximately(next, current)) audio.Set(i, next);
                }

            GUILayout.Label("Mouse sensitivity  " + MouseMultiplier.ToString("0.00") + "×");
            float mouse = GUILayout.HorizontalSlider(MouseMultiplier, .25f, 3);
            if (!Mathf.Approximately(mouse, MouseMultiplier))
            {
                savedMouse = mouse;
                PlayerPrefs.SetFloat(MouseKey, mouse);
            }

            bool nextFullscreen = GUILayout.Toggle(fullscreen, "Borderless fullscreen");
            bool nextVSync = GUILayout.Toggle(vsync, "VSync");
            bool nextReduced = GUILayout.Toggle(reducedResolution, "Reduce 3D render scale to 75%");
            if (fullscreen == nextFullscreen && vsync == nextVSync && reducedResolution == nextReduced) return;
            fullscreen = nextFullscreen;
            vsync = nextVSync;
            reducedResolution = nextReduced;
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "VSync", vsync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "ReducedResolution", reducedResolution ? 1 : 0);
            ApplyGraphics();
        }

        private void ApplyGraphics()
        {
            if (Screen.fullScreen != fullscreen) Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = 60;
            if (reducedResolution && graphics == null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)
            {
                previousPipeline = QualitySettings.renderPipeline;
                graphics = Instantiate(source);
                graphics.renderScale = .75f;
                QualitySettings.renderPipeline = graphics;
            }
            else if (!reducedResolution) RestorePipeline();
        }

        private void RestorePipeline()
        {
            if (graphics == null) return;
            if (QualitySettings.renderPipeline == graphics) QualitySettings.renderPipeline = previousPipeline;
            Destroy(graphics);
            graphics = null;
        }

        public void Save()
        {
            audio?.Save();
            PlayerPrefs.Save();
        }

        private void OnDestroy() => RestorePipeline();
        private void OnApplicationQuit() => Save();
    }
}
