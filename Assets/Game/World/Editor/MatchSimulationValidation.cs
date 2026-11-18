using System;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class MatchSimulationValidation
    {
        [MenuItem("Tools/Dragonland/Match Simulation/Start Soak")]
        private static void StartSoak() => AutomatedMatchSimulation.StartRun(new MatchSimulationOptions());

        [MenuItem("Tools/Dragonland/Match Simulation/Start Soak", true)]
        private static bool CanStartSoak() => EditorApplication.isPlaying && AutomatedMatchSimulation.Active == null;

        [MenuItem("Tools/Dragonland/Match Simulation/Stop Soak")]
        private static void StopSoak() => _ = AutomatedMatchSimulation.Active.StopRun("explicit developer stop");

        [MenuItem("Tools/Dragonland/Match Simulation/Stop Soak", true)]
        private static bool CanStopSoak() => AutomatedMatchSimulation.Active != null;

        [MenuItem("Tools/Dragonland/Match Simulation/Build Windows Player")]
        private static void BuildSoakPlayer() => BuildPlayer();

        [MenuItem("Tools/Dragonland/Match Simulation/Build Windows Player", true)]
        private static bool CanBuildSoakPlayer() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static BuildSummary BuildPlayer()
        {
            var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/DragonlandBattlefield.unity" },
                locationPathName = "Builds/M52/Player/Dragonland.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            }).summary;
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("M52 player build: " + summary.result);
            return summary;
        }

        public static int Validate()
        {
            int checks = 0;
            void Check(bool ok, string label) { if (!ok) throw new Exception("M52 invariant: " + label); checks++; }
            var ledger = new MatchSimulationLootLedger();
            Check(ledger.RegisterRoll("chest-a", 3), "first generation");
            Check(!ledger.RegisterRoll("chest-a", 3), "duplicate generation");
            Check(!ledger.Claim("unknown", 3), "unissued loot");
            Check(!ledger.Claim("chest-a", 4), "quantity inflation");
            Check(ledger.Claim("chest-a", 3), "legitimate transfer");
            Check(!ledger.Claim("chest-a", 3), "duplicate transfer");
            Check(ledger.RegisterRoll("chest-b", 3) && ledger.Claim("chest-b", 3), "same item/count distinct batch");
            ledger.Clear();
            Check(!ledger.Claim("chest-a", 3), "cross-round stale claim");
            Check(ledger.RegisterRoll("chest-a", 0) && ledger.Claim("chest-a", 0), "empty legitimate roll");
            var stall = new MatchSimulationStall();
            Check(!stall.Observe(Vector3.zero, true, 0, 10), "initial anchor");
            Check(!stall.Observe(Vector3.zero, true, 9, 10), "below threshold");
            Check(stall.Observe(Vector3.zero, true, 10, 10), "stuck threshold");
            Check(!stall.Observe(Vector3.zero, true, 20, 10), "one report per episode");
            Check(!stall.Observe(Vector3.zero, false, 40, 10), "legitimate attack/death/idle excluded");
            Check(!stall.Observe(Vector3.zero, true, 50, 10), "restart after idle");
            Check(!stall.Observe(Vector3.right * 2, true, 59, 10), "movement resets progress");
            Check(stall.Observe(Vector3.right * 2, true, 69, 10), "second distinct stall");
            new MatchSimulationOptions().Validate(); checks++;
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, -1f })
            {
                bool rejected = false;
                try { new MatchSimulationOptions { durationSeconds = bad }.Validate(); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "invalid wall limit");
            }
            Debug.Log("[M52] PASS " + checks + " loot/stall/configuration invariant checks");
            return checks;
        }
    }
}
