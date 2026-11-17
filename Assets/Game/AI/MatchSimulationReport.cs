#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [Serializable]
    public sealed class MatchSimulationOptions
    {
        public int matches = 3;
        public float durationSeconds = 1800;
        public float matchTimeoutSeconds = 900;
        public float stuckSeconds = 30;
        public int seed = 52;
        public string outputDirectory;
        public void Validate()
        {
            if (matches < 1 || matches > 100000 || !Finite(durationSeconds) || durationSeconds < 5
                || !Finite(matchTimeoutSeconds) || matchTimeoutSeconds < 5 || !Finite(stuckSeconds) || stuckSeconds < 5)
                throw new ArgumentException("Invalid simulation bounds.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class MatchSimulationCounters
    {
        public int started, completed, failed, resets, kills, deaths, campsCleared, exceptions, networkErrors;
        public int stuckAI, invalidInventories, duplicateLoot, highestLevel;
        public long itemsGenerated, itemsClaimed;
    }

    [Serializable]
    public sealed class MatchSimulationRecord
    {
        public string utc = DateTime.UtcNow.ToString("O");
        public string kind, message, actor, team, position, goal;
        public int round;
        public double seconds;
        public MatchSimulationCounters counters;
    }

    // One JSON object per line, flushed immediately; no unbounded in-memory history.
    public sealed class MatchSimulationReport : IDisposable
    {
        private readonly StreamWriter writer;
        public string DirectoryPath { get; }
        public MatchSimulationReport(string directory)
        {
            DirectoryPath = Path.GetFullPath(string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Application.persistentDataPath, "MatchSimulation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8))
                : directory);
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(Path.Combine(DirectoryPath, "events.jsonl"))) throw new IOException("Choose a fresh simulation report directory.");
            writer = new StreamWriter(Path.Combine(DirectoryPath, "events.jsonl"), false) { AutoFlush = true };
        }
        public void Write(MatchSimulationRecord record) => writer.WriteLine(JsonUtility.ToJson(record));
        public void Summary(string status, MatchSimulationCounters counters, double seconds)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, "summary.json"), JsonUtility.ToJson(new MatchSimulationRecord
                { kind = "summary", message = status, seconds = seconds, counters = counters }, true));
            File.WriteAllText(Path.Combine(DirectoryPath, "summary.txt"), status + Environment.NewLine
                + "Local single-runner simulation; six server-owned bots; remote-client coverage not implied." + Environment.NewLine
                + "Wall seconds: " + seconds.ToString("F1") + Environment.NewLine + JsonUtility.ToJson(counters, true));
        }
        public void Dispose() => writer.Dispose();
    }
}
#endif
