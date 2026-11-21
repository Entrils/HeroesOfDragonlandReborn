using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI.Minimap;
using HeroesOfDragonland.World.Camps;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    internal enum TeamPingKind { Location, Enemy, Camp, Boss, Fortress, King, Loot, Retreat, Danger }

    internal readonly struct TeamPing
    {
        public readonly int Round, Source, Expires;
        public readonly TeamId Team;
        public readonly TeamPingKind Kind;
        public readonly Vector3 Position;
        public TeamPing(int round, TeamId team, int source, TeamPingKind kind, Vector3 position, int expires)
        {
            Round = round;
            Team = team;
            Source = source;
            Kind = kind;
            Position = position;
            Expires = expires;
        }
    }

    internal static class TeamPings
    {
        private const float MaxDistance = 42f;
        private const float LandmarkRadius = 4f;

        internal static bool PlayableTeam(TeamId team) => team == TeamId.TeamA || team == TeamId.TeamB;

        internal static bool TryAim(NetworkTestPlayer player, Camera camera, TeamPingKind intent, out Vector3 point, out NetworkId target)
        {
            target = default;
            point = default;
            if (camera == null) return false;
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!FirstHit(player, ray, MaxDistance + 10, out var hit) || !InRange(player, hit.point))
            {
                if (intent == TeamPingKind.Location) return false;
                // A warning can still mark your footing when the view is aimed into the sky.
                if (!FirstHit(player, new Ray(player.transform.position, Vector3.down), 3, out hit)) return false;
            }
            point = hit.point;
            if (intent == TeamPingKind.Location)
            {
                var actor = hit.collider.GetComponentInParent<NetworkObject>();
                if (actor != null && actor.IsValid && actor.Runner == player.Runner && TryClassify(player, actor, out _))
                    target = actor.Id;
            }
            return true;
        }

        internal static bool TryResolve(NetworkTestPlayer player, TeamPingKind intent, Vector3 point, NetworkId target,
            out TeamPingKind kind, out Vector3 position)
        {
            kind = intent;
            position = default;
            if ((intent != TeamPingKind.Location && intent != TeamPingKind.Danger && intent != TeamPingKind.Retreat)
                || !InRange(player, point)) return false;
            var map = player.Runner.GetComponentInParent<TacticalMapSource>()?.Definition;
            if (map == null || !InBounds(map, point)) return false;
            var eye = player.transform.position + Vector3.up * .65f;
            if (target != default)
            {
                if (intent != TeamPingKind.Location || !player.Runner.TryFindObject(target, out var actor)
                    || actor == null || !actor.IsValid || !TryClassify(player, actor, out kind)) return false;
                var collider = actor.GetComponentInChildren<Collider>();
                if (collider == null || !collider.enabled || (point - collider.ClosestPoint(point)).sqrMagnitude > 4f)
                    return false;
                var aim = collider.ClosestPoint(point);
                if (!InRange(player, aim) || !InBounds(map, aim) ||
                    !SightHit(player, eye, aim, out var hit) || hit.collider.GetComponentInParent<NetworkObject>() != actor)
                    return false;
                position = hit.point;
                return true;
            }

            // Canonicalize free pings to a real, nearby visible surface.
            if (!SightHit(player, eye, point, out var surface) || (surface.point - point).sqrMagnitude > .36f) return false;
            position = surface.point;
            if (intent != TeamPingKind.Location) return true;
            float nearest = LandmarkRadius * LandmarkRadius;
            foreach (var landmark in map.Landmarks)
            {
                float distance = (landmark.position - position).sqrMagnitude;
                if (distance >= nearest) continue;
                switch (landmark.kind)
                {
                    case MapMarkerKind.Camp: kind = TeamPingKind.Camp; break;
                    case MapMarkerKind.BossCamp: kind = TeamPingKind.Boss; break;
                    case MapMarkerKind.Fortress: kind = TeamPingKind.Fortress; break;
                    default: continue;
                }
                nearest = distance;
            }
            return true;
        }

        private static bool TryClassify(NetworkTestPlayer sender, NetworkObject actor, out TeamPingKind kind)
        {
            kind = TeamPingKind.Location;
            if (actor.TryGetComponent<NetworkCharacterStats>(out var stats) && stats.Dead) return false;
            if (actor.TryGetComponent<NetworkTestPlayer>(out var player))
            {
                if (!PlayableTeam(player.Team) || player.Team == sender.Team || player.IsSuspended) return false;
                kind = TeamPingKind.Enemy;
            }
            else if (actor.TryGetComponent<NetworkKingObjective>(out _)) kind = TeamPingKind.King;
            else if (actor.TryGetComponent<NetworkFortressGate>(out var gate) && !gate.IsOpen) kind = TeamPingKind.Fortress;
            else if (actor.TryGetComponent<NetworkLootChest>(out var chest) && !chest.IsOpen) kind = TeamPingKind.Loot;
            else if (actor.TryGetComponent<BossEncounter>(out _)) kind = TeamPingKind.Boss;
            else if (actor.TryGetComponent<NetworkCamp>(out var camp))
                kind = camp.Definition?.Difficulty == CampDifficulty.Contested ? TeamPingKind.Boss : TeamPingKind.Camp;
            return kind != TeamPingKind.Location;
        }

        private static bool InRange(NetworkTestPlayer player, Vector3 point) =>
            float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z)
            && (point - player.transform.position).sqrMagnitude <= MaxDistance * MaxDistance;

        private static bool InBounds(TacticalMapDefinition map, Vector3 point) =>
            Mathf.Abs(point.x - map.Center.x) <= map.Size.x * .5f && Mathf.Abs(point.z - map.Center.y) <= map.Size.y * .5f;

        private static bool SightHit(NetworkTestPlayer player, Vector3 eye, Vector3 point, out RaycastHit hit)
        {
            var delta = point - eye;
            return FirstHit(player, new Ray(eye, delta.normalized), delta.magnitude + .55f, out hit);
        }

        private static bool FirstHit(NetworkTestPlayer player, Ray ray, float distance, out RaycastHit hit)
        {
            hit = default;
            var scene = player.gameObject.scene.GetPhysicsScene();
            var hits = new RaycastHit[32];
            int count = scene.Raycast(ray.origin, ray.direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // A saturated unordered result can't establish the first obstruction.
            if (count == hits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i];
                if (candidate.distance >= nearest || candidate.transform.IsChildOf(player.transform)) continue;
                hit = candidate;
                nearest = candidate.distance;
            }
            return hit.collider != null;
        }
    }
}
