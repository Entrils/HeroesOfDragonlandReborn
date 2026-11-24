using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using HeroesOfDragonland.Multiplayer;

namespace HeroesOfDragonland.Combat
{
    internal sealed class DeathRecapHistory
    {
        internal const float DisplaySeconds = 8f;
        private const float HistorySeconds = 10f;
        private const int MaxHits = 4;
        private readonly Queue<Hit> hits = new(MaxHits);
        internal readonly struct Hit
        {
            internal readonly string Source, Action;
            internal readonly float Damage;
            internal readonly double Time;
            internal Hit(string source, string action, float damage, double time)
            { Source = source; Action = action; Damage = damage; Time = time; }
        }

        internal void Record(string source, string action, float damage, double time)
        {
            Expire(time);
            if (hits.Count >= MaxHits) hits.Dequeue();
            hits.Enqueue(new Hit(Safe(source, 20), Safe(action, 22), damage, time));
        }

        internal void Build(string creditedKiller, string assists, out string headline, out string recent, out string support)
        {
            var entries = hits.ToArray();
            Clear();
            var final = entries[entries.Length - 1];
            string killer = string.IsNullOrEmpty(creditedKiller) ? final.Source : creditedKiller;
            headline = killer == final.Source
                ? $"{killer} | {final.Action} | {Damage(final.Damage)} damage"
                : $"{killer} credited | Final hit: {final.Source}, {final.Action}, {Damage(final.Damage)}";
            var rows = new StringBuilder();
            int recentBytes = 0;
            for (int i = entries.Length - 2; i >= 0; i--)
            {
                var entry = entries[i];
                string line = $"-{(final.Time - entry.Time).ToString("0.0", CultureInfo.InvariantCulture)}s  {entry.Source} | {entry.Action} | {Damage(entry.Damage)}";
                int lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
                if (recentBytes + lineBytes > 106) break;
                recentBytes += lineBytes;
                if (rows.Length > 0) rows.Append('\n');
                rows.Append(line);
            }
            recent = rows.ToString();
            support = assists;
        }

        internal void Expire(double now)
        {
            while (hits.Count > 0 && now - hits.Peek().Time > HistorySeconds) hits.Dequeue();
        }

        internal void Clear() => hits.Clear();
        internal static string Describe(GameObject source, GameObject victim)
        {
            if (source == null) return "Environment";
            if (source == victim) return "Self";
            if (source.TryGetComponent<NetworkTestPlayer>(out var player)) return "Player " + player.ParticipantNumber;
            if (source.TryGetComponent<NetworkDeployable>(out _)) return "Trap";
            if (source.TryGetComponent<NetworkFortressGuard>(out _)) return "Fortress guard";
            if (source.TryGetComponent<NetworkEnemy>(out var enemy))
                return enemy.Definition?.Boss != null ? "Stone Warden" : enemy.Definition?.Archetype.ToString() ?? "Enemy";
            return "World hazard";
        }
        private static string Damage(float amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Safe(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Damage";
            var text = new StringBuilder(max);
            int bytes = 0;
            foreach (char c in value)
            {
                if (char.IsControl(c) || char.IsSurrogate(c) || c == '<' || c == '>') continue;
                bytes += c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
                if (bytes > max) break;
                text.Append(c);
            }
            return text.Length == 0 ? "Damage" : text.ToString();
        }
    }
}
