#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    // Computes intentions only. The normal player motor performs movement/collision.
    internal sealed class DeveloperBotNavigation
    {
        private static readonly float[] DetourAngles = { 55, -55, 90, -90, 125, -125, 165, -165 };
        private readonly NavMeshPath path = new NavMeshPath();
        private Vector3[] corners = System.Array.Empty<Vector3>();
        private float refresh;
        private int corner;
        private Vector3 lastGoal;
        private Vector3 progressAnchor, detour;
        private float stalledFor, detourRemaining;
        private int recoveryAttempt;
        private bool hasPath;
        public bool HasPath => hasPath;
        public Vector3 Steer(NetworkTestPlayer player, Vector3 goal, float deltaTime)
        {
            Vector3 position = player.transform.position - Vector3.up;
            goal.y = position.y;
            if ((position - progressAnchor).sqrMagnitude >= .36f)
            {
                progressAnchor = position;
                stalledFor = 0;
            }
            stalledFor += deltaTime;
            refresh -= deltaTime;
            bool goalChanged = (lastGoal - goal).sqrMagnitude > 9;
            if (goalChanged) detourRemaining = 0;
            if (detourRemaining > 0)
            {
                detourRemaining -= deltaTime;
                var offset = Planar(detour - position);
                if (offset.sqrMagnitude > .16f && Clear(player, offset))
                    return offset.normalized;
                detourRemaining = 0;
                refresh = 0;
            }
            if (refresh <= 0 || goalChanged)
            {
                refresh = 1.25f; lastGoal = goal; corner = 1;
                hasPath = NavMesh.SamplePosition(position, out var start, 2, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(goal, out var end, 3, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete;
                corners = hasPath ? path.corners : System.Array.Empty<Vector3>();
            }
            while (corner < corners.Length - 1 && Planar(corners[corner] - position).sqrMagnitude < .36f
                && CanStep(player, position, Planar(corners[corner + 1] - position))) corner++;
            var desired = Planar((corners.Length > 1 ? corners[corner] : goal) - position);
            if (stalledFor >= 1.5f)
            {
                stalledFor = 0;
                progressAnchor = position;
                var heading = desired.sqrMagnitude > .04f ? desired : Planar(goal - position);
                if (TryDetour(player, position, heading)) return Planar(detour - position).normalized;
            }
            return hasPath && CanStep(player, position, desired) ? desired.normalized : Vector3.zero;
        }

        private bool TryDetour(NetworkTestPlayer player, Vector3 position, Vector3 heading)
        {
            if (heading.sqrMagnitude < .001f) return false;
            // Paired lanes use identical timing. Alternate handedness on repeated attempts.
            float sign = (player.SpawnLane + recoveryAttempt++) % 2 == 0 ? 1 : -1;
            foreach (float angle in DetourAngles)
            {
                var direction = Quaternion.Euler(0, angle * sign, 0) * heading.normalized;
                if (!CanStep(player, position, direction)
                    || !NavMesh.SamplePosition(position + direction * 1.8f, out var anchor, .5f, NavMesh.AllAreas)
                    || Planar(anchor.position - position).sqrMagnitude < 1
                    || NavMesh.Raycast(position, anchor.position, out _, NavMesh.AllAreas)
                    || !Clear(player, Planar(anchor.position - position), 1.2f)) continue;
                detour = anchor.position;
                detourRemaining = 1.25f;
                return true;
            }
            return false;
        }

        private static bool CanStep(NetworkTestPlayer player, Vector3 feet, Vector3 direction)
        {
            if (direction.sqrMagnitude < .0001f) return false;
            var offset = direction.normalized * .25f;
            var root = player.transform.position;
            var spawn = player.Runner.GetComponentInParent<TeamSpawnService>();
            return (spawn == null || spawn.ConstrainPlayerMovement(player.Team, root, root + offset) != root)
                && NavMesh.SamplePosition(feet + offset, out var probe, .45f, NavMesh.AllAreas)
                && Planar(probe.position - feet).sqrMagnitude > .01f
                && !NavMesh.Raycast(feet, probe.position, out _, NavMesh.AllAreas)
                && Clear(player, direction);
        }
        private static bool Clear(NetworkTestPlayer player, Vector3 direction, float distance = .2f)
        {
            var root = player.transform;
            foreach (var hit in Physics.CapsuleCastAll(root.position + Vector3.up * .5f, root.position - Vector3.up * .5f,
                .48f, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                if (hit.transform != root && !hit.transform.IsChildOf(root) && hit.normal.y < .7f) return false;
            return true;
        }
        private static Vector3 Planar(Vector3 value) { value.y = 0; return value; }
    }
}
#endif
