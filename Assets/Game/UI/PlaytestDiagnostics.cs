using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Fusion.Statistics;
using HeroesOfDragonland.Multiplayer;
using Unity.Profiling;
using UnityEngine.Rendering;
#endif

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent]
    public sealed class PlaytestDiagnostics : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string Prefix = "[Playtest]";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private MultiplayerBootstrap bootstrap;
        private ProfilerRecorder mainThread, allocations, bots, enemies;
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private float elapsed, maxFrame;
        private int frames, gcSamples, mainSamples, botSamples, enemySamples, networkSamples;
        private long allocated, mainNanos, botNanos, enemyNanos;
        private double receivedBytes, sentBytes, rttMs;
        public bool Recording { get; private set; }

        private void Start()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hodrPlaytestLog") >= 0) SetRecording(true);
        }

        public void SetRecording(bool value)
        {
            if (Recording == value) return;
            if (value)
            {
                mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
                allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                bots = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dragonland.BotTick");
                enemies = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dragonland.EnemyTick");
                ResetSampleWindow();
            }
            else
            {
                mainThread.Dispose();
                allocations.Dispose();
                bots.Dispose();
                enemies.Dispose();
            }
            Recording = value;
            Debug.Log(Prefix + (value ? " recording started" : " recording stopped"));
        }

        private void LateUpdate()
        {
            if (!Recording) return;
            if (!Application.isBatchMode) FrameTimingManager.CaptureFrameTimings();
            float delta = Time.unscaledDeltaTime;
            elapsed += delta;
            maxFrame = Mathf.Max(maxFrame, delta);
            frames++;
            Accumulate(allocations, ref allocated, ref gcSamples);
            Accumulate(mainThread, ref mainNanos, ref mainSamples);
            Accumulate(bots, ref botNanos, ref botSamples);
            Accumulate(enemies, ref enemyNanos, ref enemySamples);
            var runner = bootstrap != null ? bootstrap.Runner : null;
            if (runner != null && runner.IsRunning && runner.TryGetFusionStatistics(out var statistics))
            {
                var stats = statistics.SimulationSnapshot.Stats;
                if (stats.TryGetValue(FusionStatType.InBandwidth, out var incoming)) receivedBytes += incoming;
                if (stats.TryGetValue(FusionStatType.OutBandwidth, out var outgoing)) sentBytes += outgoing;
                if (stats.TryGetValue(FusionStatType.RoundTripTime, out var rtt) && rtt > 0) rttMs = rtt * 1000;
                networkSamples++;
            }
            if (elapsed < 10) return;

            double gpu = !Application.isBatchMode && FrameTimingManager.GetLatestTimings(1, timing) > 0 ? timing[0].gpuFrameTime : 0;
            string gpuMs = gpu > 0 ? gpu.ToString("F2", Invariant) : "n/a";
            string gc = gcSamples > 0 ? (allocated / gcSamples).ToString(Invariant) : "n/a";
            string traffic = "in_Bps=n/a out_Bps=n/a rtt_ms=n/a";
            if (networkSamples > 0)
            {
                string rtt = rttMs > 0 ? rttMs.ToString("F1", Invariant) : "n/a";
                traffic = FormattableString.Invariant($"in_Bps={receivedBytes / elapsed:F0} out_Bps={sentBytes / elapsed:F0} rtt_ms={rtt}");
            }
            var match = bootstrap != null ? bootstrap.GetComponent<NetworkFortressSpawner>()?.Match : null;
            string phase = match != null && match.Object != null && match.Object.IsValid ? match.State.ToString() : "offline";
            Debug.Log(FormattableString.Invariant(
                $"{Prefix} phase={phase} fps={frames / elapsed:F1} max_frame_ms={maxFrame * 1000:F2} main_ms={Ms(mainNanos, mainSamples)} gpu_ms={gpuMs} gc_B_frame={gc} bot_ms={Ms(botNanos, botSamples)} enemy_ms={Ms(enemyNanos, enemySamples)} {traffic}"));
            ResetSampleWindow();
            if (!bots.Valid) bots = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dragonland.BotTick");
            if (!enemies.Valid) enemies = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dragonland.EnemyTick");
        }

        private void ResetSampleWindow()
        {
            elapsed = maxFrame = 0;
            frames = gcSamples = mainSamples = botSamples = enemySamples = networkSamples = 0;
            allocated = mainNanos = botNanos = enemyNanos = 0;
            receivedBytes = sentBytes = rttMs = 0;
        }

        private static void Accumulate(ProfilerRecorder recorder, ref long total, ref int samples)
        {
            if (!recorder.Valid || recorder.Count == 0) return;
            total += recorder.LastValue;
            samples++;
        }

        private static string Ms(long nanos, int samples)
            => samples > 0 ? (nanos / (1_000_000d * samples)).ToString("F3", Invariant) : "n/a";

        private void OnDisable() => SetRecording(false);
#endif
    }
}
