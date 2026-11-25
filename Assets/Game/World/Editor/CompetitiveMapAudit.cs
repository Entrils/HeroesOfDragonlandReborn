using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.World.Editor
{
    // Read-only measurements on the saved Battlefield bake and authored match assets.
    public static class CompetitiveMapAudit
    {
        private static readonly Vector3 North = Vector3.forward;
        private static readonly Vector3 South = Vector3.back;

        [MenuItem("Heroes of Dragonland/Map/Measure Competitive Battlefield")]
        private static void Print() => Debug.Log(Measure());

        public static string Measure()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != BattlefieldSceneBuilder.ScenePath
                || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Open only DragonlandBattlefield in Edit Mode.");
            Physics.SyncTransforms();
            var bootstrap = Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var property = new SerializedObject(bootstrap.GetComponent<NetworkFortressSpawner>()).FindProperty("matchPrefab");
            var match = (NetworkMatch)property.objectReferenceValue;
            var matchData = new SerializedObject(match);
            var balance = match.Balance;
            var assault = bootstrap.GetComponent<FortressAssaultSpawner>().Definition;
            var playerPrefab = (Component)new SerializedObject(bootstrap).FindProperty("playerPrefab").objectReferenceValue;
            var speed = new SerializedObject(playerPrefab.GetComponent<NetworkTestPlayer>()).FindProperty("movementSpeed").floatValue;
            var respawn = bootstrap.GetComponent<NetworkFortressSpawner>().RespawnConfig;
            var aKing = matchData.FindProperty("teamAKingPosition").vector3Value;
            var bKing = matchData.FindProperty("teamBKingPosition").vector3Value;
            var aGate = assault.Gates.First(g => g.Prefab.GetComponent<FortressGate>().Team == TeamId.TeamA);
            var bGate = assault.Gates.First(g => g.Prefab.GetComponent<FortressGate>().Team == TeamId.TeamB);
            var spawn = bootstrap.GetComponent<TeamSpawnService>();
            var a = Points(spawn.GetArea(TeamId.TeamA));
            var b = Points(spawn.GetArea(TeamId.TeamB));
            var world = new StringBuilder();
            void Write(string line) => world.AppendLine(line);
            string Times(Vector3[] origins, Vector3 destination)
                => string.Join(", ", origins.Select(x => F(Length(x, destination) / speed)));
            float Best(Vector3[] origins, Vector3 destination) => origins.Min(x => Length(x, destination)) / speed;
            Write($"Movement {F(speed)} m/s; respawn at 0s L1 {F(balance.RespawnDelay(0,1))}, 600s L5 {F(balance.RespawnDelay(600,5))}, 1200s L10 {F(balance.RespawnDelay(1200,10))}; protection {F(respawn.ProtectionDuration)}");
            Write("Spawn-center A " + Times(a, Vector3.zero) + " B " + Times(b, Vector3.zero));
            Write("Spawn mirrored counterpart exact NavMesh coverage A->B " + string.Join(", ", a.Select(x => MirrorFits(Route(x, Vector3.zero))))
                + " B->A " + string.Join(", ", b.Select(x => MirrorFits(Route(x, Vector3.zero)))));
            Write("Spawn-center paired via ridge ends north/south A " + F(Best(a, new Vector3(-15, 0,15)) + Length(new Vector3(-15, 0,15),Vector3.zero) / speed)
                + "/" + F(Best(a, new Vector3(-15, 0,-15)) + Length(new Vector3(-15, 0,-15),Vector3.zero) / speed)
                + " B " + F(Best(b, new Vector3(15, 0,15)) + Length(new Vector3(15, 0,15),Vector3.zero) / speed)
                + "/" + F(Best(b, new Vector3(15, 0,-15)) + Length(new Vector3(15, 0,-15),Vector3.zero) / speed));
            var grid = new CompetitiveMapGrid(.5f);
            var centerFromA = grid.From(Vector3.zero, spawn, TeamId.TeamA);
            var centerFromB = grid.From(Vector3.zero, spawn, TeamId.TeamB);
            Write("Independent 0.5m grid center A " + string.Join(", ", a.Select(x => F(centerFromA(x) / speed)))
                + " B " + string.Join(", ", b.Select(x => F(centerFromB(x) / speed))));
            Write("Spawn-home King A " + Times(a, aKing) + " B " + Times(b, bKing));
            Write("Spawn-own main gate A " + Times(a, aGate.Position) + " B " + Times(b, bGate.Position));

            var spawner = bootstrap.GetComponent<NetworkCampSpawner>();
            var entries = new SerializedObject(spawner).FindProperty("entries");
            var camps = new List<(string Name, Vector3 Point, string Tier)>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var camp = (NetworkCamp)entry.FindPropertyRelative("prefab").objectReferenceValue;
                var definition = camp.Definition;
                var location = spawner.transform.TransformPoint(entry.FindPropertyRelative("localPosition").vector3Value);
                var rotation = spawner.transform.rotation * Quaternion.Euler(0, entry.FindPropertyRelative("yaw").floatValue, 0);
                foreach (var member in definition.Enemies)
                    if (!NavMesh.SamplePosition(location + rotation * member.Offset, out _, 1, NavMesh.AllAreas))
                        throw new InvalidOperationException("Enemy off NavMesh at camp " + (i + 1));
                balance.TryGetCamp(definition.Difficulty, out var pacing);
                bool boss = definition.Enemies.Any(member => member.Prefab.GetComponent<EnemyBrain>().Definition.Boss != null);
                var loot = new SerializedObject(definition.ChestPrefab.GetComponent<LootChest>().Table);
                string name = "C" + (i + 1);
                camps.Add((name, location, definition.Difficulty.ToString()));
                Write($"{name} {location} {definition.Difficulty} boss={boss} A/B best {F(Best(a, location))}/{F(Best(b, location))}"
                    + $" sight from spawns A/B {a.Count(p => Visible(p - Vector3.up, location))}/{b.Count(p => Visible(p - Vector3.up, location))}"
                    + $" first {F(pacing.FirstSpawnSeconds)} XP {pacing.Experience(false)}/{pacing.Experience(true)}"
                    + $" repeat {F(pacing.RespawnSeconds(false))}/{F(pacing.RespawnSeconds(true))}"
                    + $" draws {loot.FindProperty("minimumDrops").intValue}-{loot.FindProperty("maximumDrops").intValue}"
                    + $" center {F(Length(location, Vector3.zero) / speed)}"
                    + $" return A/B {F(a.Min(p => Length(location, p)) / speed)}/{F(b.Min(p => Length(location, p)) / speed)}");
            }

            Write("Camp-to-camp walking seconds (upper triangle)");
            for (int i = 0; i < camps.Count; i++)
                Write(camps[i].Name + " " + string.Join(" ", camps.Skip(i + 1).Select(x => x.Name + "=" + F(Length(camps[i].Point, x.Point) / speed))));
            Write("Camp-to-camp eye-height lines");
            foreach (var from in camps)
                Write(from.Name + " sees " + string.Join(" ", camps.Where(to => to.Name != from.Name && Visible(from.Point, to.Point)).Select(to => to.Name)));

            var rootA = GameObject.Find("Team A Fortress").transform;
            var rootB = GameObject.Find("Team B Fortress").transform;
            // Audit the two openings at the authored side-wall breaks; main is the actual spawned gate anchor.
            MeasureFortress(rootA, aGate, aKing, a, TeamId.TeamB);
            MeasureFortress(rootB, bGate, bKing, b, TeamId.TeamA);
            void MeasureFortress(Transform root, FortressAssaultDefinition.GateEntry gate, Vector3 king, Vector3[] home, TeamId invader)
            {
                var north = root.TransformPoint(new Vector3(3, 0, 12));
                var south = root.TransformPoint(new Vector3(3, 0, -12));
                var legalFromKing = grid.From(king, spawn, invader, gate);
                foreach (var lane in new[] { (Name: "main", Point: gate.Position), (Name: "side +localZ", Point: north), (Name: "side -localZ", Point: south) })
                {
                    var track = lane.Name == "main" ? root.right : root.forward;
                    float approach = Length(Vector3.zero, lane.Point);
                    float inside = Length(lane.Point, king);
                    float fastReturn = home.Min(p => Length(p, lane.Point)) / speed;
                    var route = Route(lane.Point, king);
                    bool allowed = route.Length > 0 && route.Zip(route.Skip(1), (x, y) => spawn.ConstrainPlayerMovement(invader, x, y) == y).All(x => x);
                    Write($"{root.name} {lane.Name} {lane.Point} center/King {F(approach / speed)}/{F(inside / speed)}"
                        + $" width {F(Width(lane.Point, track))} return-to-entrance {F(fastReturn)}"
                        + $" see center/King {Visible(lane.Point, Vector3.zero)}/{Visible(lane.Point, king)}"
                        + $" raw path avoids spawn restriction {allowed}"
                        + (lane.Name == "main" ? "" : $" closed gate legal grid to King {F(legalFromKing(lane.Point) / speed)}s")
                        + (lane.Name == "side +localZ" ? " raw corners " + string.Join(" / ", route.Select(p => p.ToString("F2"))) : ""));
                }
                var box = gate.Prefab.GetComponentInChildren<BoxCollider>();
                Write($"{root.name} closed gate box {box?.size.ToString()}");
            }

            foreach (var side in new[] { North, South })
                foreach (float x in new[] { -15f, 15f })
                {
                    var location = new Vector3(x, 0, side.z * 15);
                    Write($"Ridge end {location} width {F(Width(location, Vector3.right))} sees center {Visible(location, Vector3.zero)}");
                }

            int walkable = 0, disconnected = 0, remote = 0;
            for (int x = -42; x <= 42; x += 3)
                for (int z = -42; z <= 42; z += 3)
                {
                    var point = new Vector3(x, 0, z);
                    if (!NavMesh.SamplePosition(point, out var hit, .6f, NavMesh.AllAreas)) continue;
                    walkable++;
                    if (Route(Vector3.zero, hit.position).Length == 0)
                    {
                        disconnected++;
                        Write("Disconnected sample " + hit.position);
                    }
                    float nearest = camps.Min(c => Vector3.Distance(c.Point, hit.position));
                    if (nearest > 18 && Vector3.Distance(aKing, hit.position) > 18
                        && Vector3.Distance(bKing, hit.position) > 18)
                    {
                        remote++;
                        Write("Low-objective-density sample " + hit.position);
                    }
                }
            Write($"3m grid walkable {walkable}, disconnected from center {disconnected}, over 18m from every camp/King {remote}");
            return world.ToString();
        }

        private static Vector3[] Points(TeamSpawnArea area)
        {
            var result = new Vector3[area.PointCount];
            for (int i = 0; i < result.Length; i++)
                if (!area.TryGetPoint(i, out result[i], out _)) throw new InvalidOperationException("Invalid spawn " + i);
            return result;
        }

        private static Vector3[] Route(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.SamplePosition(from, out var a, 2, NavMesh.AllAreas)
                && NavMesh.SamplePosition(to, out var b, 2, NavMesh.AllAreas)
                && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete ? path.corners : Array.Empty<Vector3>();
        }

        private static bool MirrorFits(Vector3[] route)
        {
            if (route.Length == 0) return false;
            Vector3 previous = default;
            for (int i = 0; i < route.Length; i++)
            {
                var p = route[i];
                if (!NavMesh.SamplePosition(new Vector3(-p.x, p.y, -p.z), out var point, .22f, NavMesh.AllAreas)
                    || i > 0 && NavMesh.Raycast(previous, point.position, out _, NavMesh.AllAreas)) return false;
                previous = point.position;
            }
            return true;
        }

        private static float Length(Vector3 from, Vector3 to)
        {
            var route = Route(from, to);
            return route.Length == 0 ? float.PositiveInfinity
                : route.Zip(route.Skip(1), Vector3.Distance).Sum();
        }

        private static bool Visible(Vector3 a, Vector3 b)
        {
            var origin = a + Vector3.up * 1.6f;
            return !Physics.Linecast(origin, b + Vector3.up * 1.6f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static float Width(Vector3 point, Vector3 travel)
        {
            Vector3 origin = point + Vector3.up * .8f;
            Vector3 side = Vector3.Cross(Vector3.up, travel.normalized);
            return Clearance(origin, side) + Clearance(origin, -side);
        }

        private static float Clearance(Vector3 origin, Vector3 direction)
            => Physics.Raycast(origin, direction, out var hit, 6, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 6;

        private static string F(float value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
