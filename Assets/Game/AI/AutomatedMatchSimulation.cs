#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Explicitly installed at runtime; never a NetworkBehaviour or authored scene dependency.
    [DisallowMultipleComponent]
    public sealed class AutomatedMatchSimulation : MonoBehaviour
    {
        private MultiplayerBootstrap bootstrap;
        private DeveloperBotService bots;
        private MatchSimulationOptions options;
        private MatchSimulationDiagnostics diagnostics;
        private MatchSimulationReport report;
        private UnityEngine.Random.State previousRandom;
        private double began, roundBegan, nextSample, startupBegan;
        private int round = -1, previousBots;
        private bool inRound, recordedFinish, stopping, restarting, disposed;
        private Task connection;
        public string Status { get; private set; } = "Not started";
        public string ReportPath => report?.DirectoryPath;
        public MatchSimulationCounters Counters => diagnostics?.Total;
        public static AutomatedMatchSimulation Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-hodrSoak") < 0) return;
            var settings = new MatchSimulationOptions();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-hodrSoakMatches") settings.matches = int.Parse(args[i + 1]);
                if (args[i] == "-hodrSoakSeconds") settings.durationSeconds = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                if (args[i] == "-hodrSoakTimeout") settings.matchTimeoutSeconds = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                if (args[i] == "-hodrSoakSeed") settings.seed = int.Parse(args[i + 1]);
                if (args[i] == "-hodrSoakOutput") settings.outputDirectory = args[i + 1];
            }
            StartRun(settings);
        }
        public static AutomatedMatchSimulation StartRun(MatchSimulationOptions settings)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Start simulation in Play Mode.");
            settings.Validate();
            if (Active != null) throw new InvalidOperationException("A simulation is already active.");
            var owner = FindFirstObjectByType<MultiplayerBootstrap>();
            if (owner == null || owner.Running || owner.Busy) throw new InvalidOperationException("Simulation requires an idle configured bootstrap.");
            var driver = owner.gameObject.AddComponent<AutomatedMatchSimulation>();
            try { driver.Initialize(owner, settings); return driver; }
            catch { Destroy(driver); throw; }
        }
        private void Initialize(MultiplayerBootstrap owner, MatchSimulationOptions settings)
        {
            bootstrap = owner; options = settings; bots = owner.GetComponent<DeveloperBotService>();
            if (bots == null) throw new InvalidOperationException("Existing developer bots missing.");
            report = new MatchSimulationReport(options.outputDirectory);
            previousRandom = UnityEngine.Random.state; UnityEngine.Random.InitState(options.seed);
            previousBots = bots.RequestedBots;
            diagnostics = new MatchSimulationDiagnostics(owner, Emit);
            began = startupBegan = Time.realtimeSinceStartupAsDouble;
            Active = this; bootstrap.SimulationSpectator = true;
            bots.SetBotCount(6);
            Status = "Starting six-bot local simulation";
            Emit("run-start", JsonUtility.ToJson(settings) + "; loot uses production GUID RNG, not deterministic seed", null);
            connection = bootstrap.StartSession(GameMode.Single, "m52-soak");
        }
        private void Update()
        {
            if (disposed || stopping || restarting || diagnostics == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - began >= options.durationSeconds) { _ = StopRun("wall-clock limit"); return; }
            diagnostics.DrainLogs();
            if (diagnostics.Total.exceptions >= 100) { _ = StopRun("error flood; inspect logs"); return; }
            if (!inRound && now - startupBegan > 60) { _ = StopRun("session/lobby startup timeout"); return; }
            if (bootstrap.Busy) return;
            if (!bootstrap.Running)
            {
                if (connection != null && connection.IsCompleted && now - startupBegan > 5 || now - startupBegan > 60)
                    _ = StopRun("session startup/disconnect failure");
                return;
            }
            var match = bootstrap.GetComponent<NetworkFortressSpawner>()?.Match;
            if (match == null || match.Object == null || !match.Object.IsValid) return;
            if (!inRound && now - startupBegan > 60) { _ = StopRun("six-bot lobby readiness timeout"); return; }
            if (match.State == MatchPhase.Lobby && bots.SpawnedCount == 6)
            {
                if (inRound)
                {
                    diagnostics.Total.resets++; Emit("reset", "Fresh lobby avatars=" + string.Join(",", bootstrap.Avatars.Select(a => a.Id)), null);
                    inRound = false;
                    if (diagnostics.Total.started >= options.matches) { _ = StopRun("requested matches exhausted"); return; }
                }
                if (!match.CanStartLobby) { Status = "Waiting for balanced six-bot lobby"; return; }
                diagnostics.BeginRound(); round = match.RoundNumber + 1; roundBegan = now;
                inRound = true; recordedFinish = false; startupBegan = now;
                if (!match.TryStartLobby()) { diagnostics.Finished(false); _ = StopRun("lobby start rejected"); return; }
                Emit("round-start", "Six bots; teamA=" + match.TeamACount + " teamB=" + match.TeamBCount, null);
            }
            if (now >= nextSample)
            {
                nextSample = now + 1;
                if (match.CanPlay) foreach (var actor in bootstrap.Avatars) MatchSimulationBotLoadout.Improve(actor.GetComponent<NetworkTestPlayer>());
                diagnostics.Sample(now, options.stuckSeconds, match.CanPlay);
                Status = "round=" + match.RoundNumber + " " + match.State + " bots=" + bots.SpawnedCount + " seconds=" + match.PlayingSeconds.ToString("F0");
                if (inRound) foreach (var actor in bootstrap.Avatars)
                {
                    var player = actor.GetComponent<NetworkTestPlayer>();
                    Emit("actor-sample", "hp=" + actor.GetComponent<HeroesOfDragonland.Stats.CharacterStats>().CurrentHealth
                        + " level=" + actor.GetComponent<NetworkSessionProgression>().Level + " chest=" + player.BotBrain?.LastChestResult, player);
                }
                report.Summary(Status, diagnostics.Total, now - began);
            }
            if (inRound && match.MatchEnded && !recordedFinish)
            {
                recordedFinish = true; diagnostics.Finished(true);
                Emit("round-completed", "winner=" + match.Winner + " duration=" + (now - roundBegan), null);
                WriteRound();
            }
            if (inRound && !recordedFinish && now - roundBegan >= options.matchTimeoutSeconds)
            {
                recordedFinish = true; diagnostics.Finished(false); Emit("round-timeout", "No natural King victory", null); WriteRound();
                _ = RestartAfterFailure();
            }
        }
        private void WriteRound() => report.Write(new MatchSimulationRecord { kind = "round-summary", round = round,
            seconds = Time.realtimeSinceStartupAsDouble - roundBegan, counters = diagnostics.Round });
        private async Task RestartAfterFailure()
        {
            restarting = true;
            await bootstrap.Disconnect();
            inRound = false;
            Emit("failed-session-cleanup", "reserved=" + bootstrap.ReservedPlayerCount + " avatars=" + bootstrap.Avatars.Count(), null);
            if (diagnostics.Total.started >= options.matches) { restarting = false; await StopRun("requested attempts exhausted"); return; }
            startupBegan = Time.realtimeSinceStartupAsDouble;
            connection = bootstrap.StartSession(GameMode.Single, "m52-soak");
            await connection; restarting = false;
        }
        public async Task StopRun(string reason = "cancelled")
        {
            if (stopping || disposed) return;
            stopping = true;
            if (inRound && !recordedFinish) { diagnostics.Finished(false); recordedFinish = true; Emit("round-interrupted", reason, null); WriteRound(); }
            await bootstrap.Disconnect();
            diagnostics.Sample(Time.realtimeSinceStartupAsDouble, options.stuckSeconds, false);
            Status = "Stopped: " + reason;
            Emit("run-stop", reason + "; avatars=" + bootstrap.Avatars.Count() + " reserved=" + bootstrap.ReservedPlayerCount, null);
            report.Summary(Status, diagnostics.Total, Time.realtimeSinceStartupAsDouble - began);
            Dispose();
        }
        private void Emit(string kind, string message, NetworkTestPlayer actor)
        {
            report?.Write(new MatchSimulationRecord { kind = kind, message = message, round = round,
                seconds = Time.realtimeSinceStartupAsDouble - began, actor = actor != null ? actor.Object.Id.ToString() : null,
                team = actor != null ? actor.Team.ToString() : null, position = actor != null ? actor.transform.position.ToString() : null,
                goal = actor?.BotBrain?.Goal.ToString() });
        }
        private void Dispose()
        {
            if (disposed) return;
            disposed = true;
            diagnostics?.Dispose(); report?.Dispose();
            if (bootstrap != null) { bootstrap.SimulationSpectator = false; bots.SetBotCount(previousBots); }
            UnityEngine.Random.state = previousRandom;
            if (Active == this) Active = null;
        }
        private void OnDestroy()
        {
            if (!disposed && report != null)
            {
                if (inRound && !recordedFinish) { diagnostics.Finished(false); WriteRound(); }
                report.Summary("Destroyed/interrupted (shutdown may be pending)", diagnostics.Total, Time.realtimeSinceStartupAsDouble - began);
                Dispose();
            }
        }
    }
}
#endif
