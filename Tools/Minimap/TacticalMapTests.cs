using System;
using System.Collections.Generic;
using HeroesOfDragonland.UI.Minimap;

internal static class TacticalMapTests
{
    private static int checks;
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    public static int Main()
    {
        MapPoint result; double u, v; int reads = 0;
        Func<MapPoint> forbidden = delegate { throw new Exception("HIDDEN PLAYER POSITION WAS READ"); };
        Check(!TacticalMapRules.TryPlayer(MapTeam.A, MapTeam.B, false, false, false, forbidden, out result), "A cannot read enemy B position");
        Check(!TacticalMapRules.TryPlayer(MapTeam.B, MapTeam.A, false, false, false, forbidden, out result), "B cannot read enemy A position");
        Check(!TacticalMapRules.TryPlayer(MapTeam.None, MapTeam.A, false, false, false, forbidden, out result), "Unassigned viewer fails closed");
        Check(!TacticalMapRules.TryPlayer(MapTeam.A, MapTeam.A, true, true, false, forbidden, out result), "Suspended player hidden before position access");
        Check(!TacticalMapRules.TryPlayer(MapTeam.A, MapTeam.A, false, false, true, forbidden, out result), "Dead ally hidden before position access");
        Check(TacticalMapRules.TryPlayer(MapTeam.A, MapTeam.A, true, false, false, delegate { reads++; return new MapPoint(10, 0, 5); }, out result) && reads == 1, "Authorized local position read once");
        Check(TacticalMapRules.TryPlayer(MapTeam.B, MapTeam.B, false, false, false, delegate { return new MapPoint(0, 0, 0); }, out result), "B ally visible");
        Check(!TacticalMapRules.TryPlayer(MapTeam.A, MapTeam.A, false, false, false, delegate { return new MapPoint(double.NaN, 0, 0); }, out result), "Invalid position rejected");
        Check(TacticalMapRules.Project(new MapPoint(0, 0, 0), 0, 0, 88, 88, out u, out v) && u == .5 && v == .5, "Map center");
        Check(TacticalMapRules.Project(new MapPoint(44, 10, 44), 0, 0, 88, 88, out u, out v) && u == 1 && v == 0, "North-east axes");
        Check(TacticalMapRules.Project(new MapPoint(-44, 0, -44), 0, 0, 88, 88, out u, out v) && u == 0 && v == 1, "South-west axes");
        Check(!TacticalMapRules.Project(new MapPoint(45, 0, 0), 0, 0, 88, 88, out u, out v), "Out of bounds hidden, not falsified at map edge");
        Check(!TacticalMapRules.Project(new MapPoint(0, 0, 0), 0, 0, 0, 88, out u, out v), "Invalid bounds fail safely");
        Check(TacticalMapRules.Project(new MapPoint(20, 0, 10), 0, 0, 40, 20, out u, out v) && u == 1 && v == .25, "Square padding preserves non-square world proportions");
        Check(TacticalMapRules.Compass(0) == "N" && TacticalMapRules.Compass(90) == "E" && TacticalMapRules.Compass(180) == "S" && TacticalMapRules.Compass(270) == "W", "Compass cardinals");
        Check(TacticalMapRules.Compass(-45) == "NW" && TacticalMapRules.Compass(405) == "NE", "Heading wraps");
        var cache = new EnemyRevealCache(); var markers = new List<MapMarker>();
        cache.Bind("match1:round1:avatar1", MapTeam.A);
        var reveal = new EnemyRevealSnapshot { Scope = "match1:round1:avatar1", Audience = MapTeam.A, Token = "target1",
            Position = new MapPoint(5, 0, 7), ObservedAt = 1, ExpiresAt = 3 };
        Check(cache.Accept(reveal, 1), "Authorized snapshot accepted");
        Check(!cache.Accept(reveal, 1.1), "Duplicate/replayed observation rejected");
        cache.Append(markers, 2);
        Check(markers.Count == 1 && markers[0].Position.X == 5 && markers[0].Kind == MapMarkerKind.RevealedEnemy, "Only supplied position snapshot shown");
        Check(markers[0].Team == MapTeam.None, "No hidden enemy affiliation inferred");
        markers.Clear(); cache.Append(markers, 3);
        Check(markers.Count == 0 && cache.Count == 0, "Expiry removes snapshot exactly on deadline");
        var wrong = reveal; wrong.Audience = MapTeam.B;
        Check(!cache.Accept(wrong, 1), "Other team's reveal rejected");
        wrong = reveal; wrong.Scope = "old-match";
        Check(!cache.Accept(wrong, 1), "Old session/round snapshot rejected");
        wrong = reveal; wrong.ObservedAt = 2;
        Check(!cache.Accept(wrong, 1), "Future observation rejected");
        wrong = reveal; wrong.ExpiresAt = double.NaN;
        Check(!cache.Accept(wrong, 1), "Invalid expiry rejected");
        wrong = reveal; wrong.Position = new MapPoint(double.PositiveInfinity, 0, 0);
        Check(!cache.Accept(wrong, 1), "Invalid reveal coordinate rejected");
        wrong = reveal; wrong.ExpiresAt = 1000;
        Check(cache.Accept(wrong, 1), "Long duration can be clamped");
        markers.Clear(); cache.Append(markers, 11);
        Check(markers.Count == 0, "Reveal cannot exceed ten seconds from observation");
        Check(cache.Accept(reveal, 1), "Fresh cache fixture"); cache.Revoke("target1");
        Check(cache.Count == 0, "Explicit revoke removes snapshot");
        Check(!cache.Accept(reveal, 1.5), "Revocation tombstone prevents old snapshot restoring a marker");
        wrong = reveal; wrong.ObservedAt = 2;
        Check(cache.Accept(wrong, 2), "Newer authorized observation can renew a revoked target");
        cache.Accept(reveal, 1); cache.Bind("match1:round2:avatar1", MapTeam.A);
        Check(cache.Count == 0, "New round clears prior reveals");
        cache.Bind(reveal.Scope, MapTeam.B);
        Check(!cache.Accept(reveal, 1), "Team change fails closed");
        cache.Bind(reveal.Scope, MapTeam.A);
        for (int i = 0; i < 1000; i++) { wrong = reveal; wrong.Token = "target" + i; cache.Accept(wrong, 1); }
        Check(cache.Count == EnemyRevealCache.Capacity, "Reveal spam bounded to sixteen snapshots");
        cache.Bind(null, MapTeam.None);
        Check(cache.Count == 0 && !cache.Accept(reveal, 1), "Disconnect/disable clears reveals and closes ingress");
        Console.WriteLine("PASS: " + checks + " tactical map/privacy/projection/reveal checks."); return 0;
    }
}
