using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Editor
{
    // Ground samples give a second route estimate independent of polygon A*.
    internal sealed class CompetitiveMapGrid
    {
        private readonly float spacing;
        private readonly int halfCells, side;
        private readonly Vector3?[] nodes;
        private readonly List<int>[] edges;
        private static readonly Vector2Int[] Steps =
            { new(1, 0), new(0, 1), new(1, 1), new(-1, 1) };

        internal CompetitiveMapGrid(float spacing)
        {
            this.spacing = spacing;
            halfCells = Mathf.RoundToInt(43 / spacing);
            side = halfCells * 2 + 1;
            nodes = new Vector3?[side * side];
            edges = new List<int>[side * side];
            for (int z = -halfCells; z <= halfCells; z++)
                for (int x = -halfCells; x <= halfCells; x++)
                    if (NavMesh.SamplePosition(new Vector3(x * spacing, 0, z * spacing), out var point, .25f, NavMesh.AllAreas)
                        && Mathf.Abs(point.position.x - x * spacing) < .12f && Mathf.Abs(point.position.z - z * spacing) < .12f)
                        nodes[Index(x, z)] = point.position;
            for (int z = -halfCells; z <= halfCells; z++)
                for (int x = -halfCells; x <= halfCells; x++)
                {
                    int from = Index(x, z);
                    if (nodes[from] is not { } a) continue;
                    foreach (var step in Steps)
                    {
                        int xx = x + step.x, zz = z + step.y;
                        if (!Inside(xx, zz)) continue;
                        int to = Index(xx, zz);
                        if (nodes[to] is not { } b || NavMesh.Raycast(a, b, out _, NavMesh.AllAreas)) continue;
                        (edges[from] ??= new List<int>(8)).Add(to);
                        (edges[to] ??= new List<int>(8)).Add(from);
                    }
                }
        }

        internal System.Func<Vector3, float> From(Vector3 source, TeamSpawnService spawns, TeamId team,
            FortressAssaultDefinition.GateEntry? closedGate = null)
        {
            Vector3 gateOrigin = default, half = default;
            Quaternion inverseGate = Quaternion.identity;
            if (closedGate is { } gate)
            {
                var box = gate.Prefab.GetComponentInChildren<BoxCollider>();
                var root = gate.Prefab.transform;
                gateOrigin = gate.Position + gate.Rotation * root.InverseTransformPoint(box.transform.TransformPoint(box.center));
                inverseGate = Quaternion.Inverse(gate.Rotation * Quaternion.Inverse(root.rotation) * box.transform.rotation);
                half = Vector3.Scale(box.size, box.transform.lossyScale) * .5f + Vector3.one * .5f;
            }

            bool Clear(Vector3 a, Vector3 b)
            {
                if (spawns.ConstrainPlayerMovement(team, a, b) != b) return false;
                if (!closedGate.HasValue) return true;
                Vector3 localA = inverseGate * (a - gateOrigin), localB = inverseGate * (b - gateOrigin);
                float enter = 0, exit = 1;
                return !Clip(localA.x, localB.x - localA.x, half.x, ref enter, ref exit)
                    || !Clip(localA.z, localB.z - localA.z, half.z, ref enter, ref exit);
            }

            int Attach(Vector3 point)
            {
                if (!NavMesh.SamplePosition(point, out var hit, 2, NavMesh.AllAreas)) return -1;
                int x = Mathf.RoundToInt(hit.position.x / spacing), z = Mathf.RoundToInt(hit.position.z / spacing);
                int best = -1;
                float distance = 4;
                for (int ix = x - 2; ix <= x + 2; ix++)
                    for (int iz = z - 2; iz <= z + 2; iz++)
                    {
                        if (!Inside(ix, iz)) continue;
                        int index = Index(ix, iz);
                        if (nodes[index] is not { } candidate) continue;
                        float delta = (candidate - hit.position).sqrMagnitude;
                        if (delta >= distance || !spawns.AllowsSpawn(team, candidate)
                            || !Clear(hit.position, candidate) || NavMesh.Raycast(hit.position, candidate, out _, NavMesh.AllAreas)) continue;
                        best = index;
                        distance = delta;
                    }
                return best;
            }

            var distances = new float[nodes.Length];
            System.Array.Fill(distances, float.PositiveInfinity);
            int start = Attach(source);
            var frontier = new SortedSet<(float Distance, int Node)>();
            if (start >= 0) { distances[start] = PlanarDistance(source, nodes[start].Value); frontier.Add((distances[start], start)); }
            while (frontier.Count > 0)
            {
                var item = frontier.Min;
                frontier.Remove(item);
                if (item.Distance != distances[item.Node] || edges[item.Node] == null) continue;
                Vector3 a = nodes[item.Node].Value;
                foreach (int index in edges[item.Node])
                {
                    Vector3 b = nodes[index].Value;
                    float next = item.Distance + Vector3.Distance(a, b);
                    if (next >= distances[index] || !spawns.AllowsSpawn(team, b) || !Clear(a, b)) continue;
                    distances[index] = next;
                    frontier.Add((next, index));
                }
            }

            return point =>
            {
                int index = Attach(point);
                return index < 0 ? float.PositiveInfinity : distances[index] + PlanarDistance(point, nodes[index].Value);
            };
        }

        private static bool Clip(float start, float direction, float half, ref float enter, ref float exit)
        {
            if (Mathf.Abs(direction) < .0001f) return Mathf.Abs(start) <= half;
            float a = (-half - start) / direction, b = (half - start) / direction;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            return enter <= exit;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        private int Index(int x, int z) => (z + halfCells) * side + x + halfCells;
        private bool Inside(int x, int z) => x >= -halfCells && x <= halfCells && z >= -halfCells && z <= halfCells;
    }
}
