using System;
using System.IO;
using HeroesOfDragonland.World;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace HeroesOfDragonland.World.Editor
{
    public static class DemoWindowsBuild
    {
        [MenuItem("Dragonland/Demo/Build Windows Development")]
        public static void Development() => Build("Development", BuildOptions.Development);

        [MenuItem("Dragonland/Demo/Build Windows Release")]
        public static void Release() => Build("Release", BuildOptions.None);

        private static void Build(string configuration, BuildOptions options)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the demo in Edit Mode.");

            var output = Path.Combine("Builds", "Demo-" + PlayerSettings.bundleVersion, configuration);
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TutorialProgress.BattlefieldPath, TutorialProgress.ScenePath },
                locationPathName = Path.Combine(output, "Dragonland.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = options | BuildOptions.StrictMode | BuildOptions.CleanBuildCache
            });
            var summary = report.summary;
            var result = $"{configuration} {PlayerSettings.bundleVersion} | {summary.result} | "
                + $"{summary.totalErrors} errors | {summary.totalWarnings} warnings | {summary.totalSize} bytes";
            File.WriteAllText(Path.Combine(output, "build-result.txt"), result);
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(result);
            UnityEngine.Debug.Log(result);
        }
    }
}
