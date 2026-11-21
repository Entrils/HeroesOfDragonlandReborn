using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Profiles;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent]
    public sealed class PlaytestSettings : MonoBehaviour
    {
        private static readonly string[] VolumeNames = { "Master volume", "Music volume", "Effects volume" };
        [SerializeField] private AudioPreferences audio;
        private UniversalRenderPipelineAsset graphics;
        private RenderPipelineAsset previousPipeline;
        private bool fullscreen, vsync, reducedResolution;
        public static float MouseMultiplier => LocalPlayerProfile.Current.Data.Settings.MouseSensitivity;

        private void Start()
        {
            if (Application.isBatchMode) return;
            var settings = LocalPlayerProfile.Current.Data.Settings;
            fullscreen = settings.Fullscreen;
            vsync = settings.VSync;
            reducedResolution = settings.ReducedResolution;
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
                LocalPlayerProfile.Current.Data.Settings.MouseSensitivity = mouse;
            }

            bool nextFullscreen = GUILayout.Toggle(fullscreen, "Borderless fullscreen");
            bool nextVSync = GUILayout.Toggle(vsync, "VSync");
            bool nextReduced = GUILayout.Toggle(reducedResolution, "Reduce 3D render scale to 75%");
            if (fullscreen == nextFullscreen && vsync == nextVSync && reducedResolution == nextReduced) return;
            fullscreen = nextFullscreen;
            vsync = nextVSync;
            reducedResolution = nextReduced;
            var settings = LocalPlayerProfile.Current.Data.Settings;
            settings.Fullscreen = fullscreen;
            settings.VSync = vsync;
            settings.ReducedResolution = reducedResolution;
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
            if (Application.isBatchMode && !LocalPlayerProfile.WriteMatchTotals) return;
            audio?.Save();
            LocalPlayerProfile.Current.Save();
        }

        private void OnDestroy() => RestorePipeline();
        private void OnApplicationQuit() => Save();
    }
}
