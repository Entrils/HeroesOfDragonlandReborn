using System;
using System.Collections.Generic;

namespace HeroesOfDragonland.UI.Minimap
{
    public enum MapTeam { None, A, B }
    public enum MapMarkerKind { LocalPlayer, Ally, Fortress, King, Camp, BossCamp, Objective, RevealedEnemy }
    public enum MapMarkerState { Unknown, Active, Cleared, Unavailable }
    public struct MapPoint
    {
        public double X, Y, Z;
        public MapPoint(double x, double y, double z) { X = x; Y = y; Z = z; }
        public bool Finite { get { return TacticalMapRules.Finite(X) && TacticalMapRules.Finite(Y) && TacticalMapRules.Finite(Z); } }
    }
    public struct MapMarker
    {
        public string Id, Label;
        public MapMarkerKind Kind;
        public MapMarkerState State;
        public MapTeam Team;
        public MapPoint Position;
        public float Heading;
    }
    public static class TacticalMapRules
    {
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public static bool TryPlayer(MapTeam viewer, MapTeam subject, bool local, bool suspended, bool dead,
            Func<MapPoint> readPosition, out MapPoint position)
        {
            position = default(MapPoint);
            // Filter before invoking any position reader. No enemy-transform fallback, including on host.
            if (viewer == MapTeam.None || subject != viewer || suspended || dead || readPosition == null) return false;
            position = readPosition(); return position.Finite;
        }
        public static bool Project(MapPoint point, double centerX, double centerZ, double sizeX, double sizeZ, out double u, out double v)
        {
            u = v = 0;
            if (!point.Finite || !Finite(centerX) || !Finite(centerZ) || !Finite(sizeX) || !Finite(sizeZ) || sizeX <= 0 || sizeZ <= 0) return false;
            double side = Math.Max(sizeX, sizeZ); // Square padding preserves world aspect ratio.
            u = .5 + (point.X - centerX) / side; v = .5 - (point.Z - centerZ) / side;
            return u >= 0 && u <= 1 && v >= 0 && v <= 1;
        }
        public static string Compass(double yaw)
        {
            if (!Finite(yaw)) return "?";
            yaw = (yaw % 360 + 360) % 360;
            return new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }[(int)Math.Floor((yaw + 22.5) / 45) % 8];
        }
    }
    public struct EnemyRevealSnapshot
    {
        public string Scope, Token;
        public MapTeam Audience;
        public MapPoint Position;
        public double ObservedAt, ExpiresAt;
    }
    // Stores authorized position snapshots only. It never accepts a Transform or discovers an enemy.
    // Authorization/targeted delivery belongs to the future gameplay reveal producer, not this UI cache.
    public sealed class EnemyRevealCache
    {
        private readonly Dictionary<string, EnemyRevealSnapshot> entries = new Dictionary<string, EnemyRevealSnapshot>();
        private readonly List<string> expired = new List<string>();
        private readonly HashSet<string> revoked = new HashSet<string>();
        private string scope;
        private MapTeam audience;
        public const int Capacity = 16;
        public const double MaximumSeconds = 10;
        public int Count { get { return entries.Count - revoked.Count; } }
        public void Bind(string currentScope, MapTeam currentAudience)
        {
            if (scope != currentScope || audience != currentAudience) { entries.Clear(); revoked.Clear(); }
            scope = currentScope; audience = currentAudience;
        }
        public bool Accept(EnemyRevealSnapshot value, double now)
        {
            Prune(now);
            if (audience == MapTeam.None || string.IsNullOrEmpty(scope) || value.Scope != scope || value.Audience != audience
                || string.IsNullOrEmpty(value.Token) || value.Token.Length > 128 || !value.Position.Finite
                || !TacticalMapRules.Finite(now) || !TacticalMapRules.Finite(value.ObservedAt) || !TacticalMapRules.Finite(value.ExpiresAt)
                || value.ObservedAt > now || value.ExpiresAt <= now || value.ExpiresAt <= value.ObservedAt) return false;
            value.ExpiresAt = Math.Min(value.ExpiresAt, value.ObservedAt + MaximumSeconds);
            if (value.ExpiresAt <= now) return false;
            EnemyRevealSnapshot old;
            if (entries.TryGetValue(value.Token, out old)) { if (value.ObservedAt <= old.ObservedAt) return false; }
            else if (entries.Count >= Capacity) return false;
            entries[value.Token] = value; revoked.Remove(value.Token); return true;
        }
        public void Revoke(string token) { if (token != null && entries.ContainsKey(token)) revoked.Add(token); }
        public void Append(List<MapMarker> destination, double now)
        {
            Prune(now);
            foreach (var entry in entries.Values) if (!revoked.Contains(entry.Token))
                destination.Add(new MapMarker { Id = "reveal:" + entry.Token, Label = "Revealed enemy (snapshot)",
                    Kind = MapMarkerKind.RevealedEnemy, State = MapMarkerState.Active, Position = entry.Position });
        }
        private void Prune(double now)
        {
            expired.Clear();
            foreach (var pair in entries) if (pair.Value.ExpiresAt <= now) expired.Add(pair.Key);
            foreach (var token in expired) { entries.Remove(token); revoked.Remove(token); }
        }
    }
}
