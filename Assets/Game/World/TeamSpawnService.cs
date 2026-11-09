using System;
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class TeamSpawnService : MonoBehaviour
    {
        [SerializeField] private TeamSpawnArea[] areas = Array.Empty<TeamSpawnArea>();
        [SerializeField] private LayerMask obstructionMask = ~0;
        private readonly Dictionary<TeamId, int> nextPoint = new();
        private readonly List<Vector3> reservations = new();
        private NetworkRunner reservationRunner;
        private int reservationTick;
        public IReadOnlyList<TeamSpawnArea> Areas => areas;
        internal void ResetReservations()
        {
            nextPoint.Clear(); reservations.Clear(); reservationRunner = null;
        }
        public TeamSpawnArea GetArea(TeamId team)
        {
            if (team != TeamId.TeamA && team != TeamId.TeamB) return null;
            TeamSpawnArea result = null;
            foreach (var area in areas)
                if (area != null && area.isActiveAndEnabled && area.Team == team)
                {
                    if (result != null) return null; // Ambiguous ownership fails closed.
                    result = area;
                }
            return result;
        }
        public bool TryChooseSpawn(NetworkRunner runner, TeamId team, Transform ignore,
            out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (!isActiveAndEnabled || runner == null || !runner.IsServer
                || runner != GetComponent<MultiplayerBootstrap>().Runner) return false;
            var area = GetArea(team);
            if (area == null || area.PointCount == 0) return false;
            if (reservationRunner != runner || reservationTick != runner.Tick.Raw)
            {
                if (reservationRunner != runner) nextPoint.Clear();
                reservationRunner = runner; reservationTick = runner.Tick.Raw; reservations.Clear();
            }
            nextPoint.TryGetValue(team, out int start);
            Physics.SyncTransforms();
            for (int offset = 0; offset < area.PointCount; offset++)
            {
                int index = (start + offset) % area.PointCount;
                if (!area.TryGetPoint(index, out var point, out var facing) || !AllowsSpawn(team, point, .5f)) continue;
                bool occupied = reservations.Exists(p => (p - point).sqrMagnitude < 1.1f * 1.1f);
                // Match the existing planar player's capsule, leaving its ground clearance intact.
                foreach (var collider in Physics.OverlapCapsule(point + Vector3.up * .5f, point - Vector3.up * .5f,
                    .48f, obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    if (ignore != null && (collider.transform == ignore || collider.transform.IsChildOf(ignore))) continue;
                    occupied = true; break;
                }
                if (occupied) continue;
                position = point; rotation = facing;
                reservations.Add(point); nextPoint[team] = (index + 1) % area.PointCount;
                return true;
            }
            return false; // Never fall back to a stacked or opposing-fortress position.
        }
        public bool AllowsSpawn(TeamId team, Vector3 position, float radius = .5f)
        {
            foreach (var area in areas)
                if (area != null && area.IsProtected && area.Team != team && area.Contains(position, radius)) return false;
            return true;
        }
        public Vector3 ConstrainPlayerMovement(TeamId team, Vector3 from, Vector3 to)
        {
            foreach (var area in areas)
                if (area != null && area.BlocksEntry(team, from, to, .5f)) return from;
            return to;
        }
        public static bool AllowsNpcSpawn(NetworkRunner runner, GameObject prefab, Vector3 position)
        {
            var service = runner.GetComponentInParent<TeamSpawnService>();
            var identity = prefab.GetComponent<TeamIdentity>();
            var guard = prefab.GetComponent<NetworkFortressGuard>();
            var king = prefab.GetComponent<NetworkKingObjective>();
            TeamId team = guard != null ? guard.Team : king != null ? king.Team : identity != null ? identity.Team : TeamId.Unassigned;
            var agent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
            return service == null || service.AllowsSpawn(team, position, agent != null ? agent.radius : .7f);
        }
    }
}
