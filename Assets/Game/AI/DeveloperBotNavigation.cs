#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    // Computes intentions only. The normal player motor performs movement/collision.
    internal sealed class DeveloperBotNavigation
    {
        private readonly NavMeshPath path = new NavMeshPath();
        private float refresh;
        private int corner;
        private Vector3 lastGoal;
        private Vector3 lastPosition;
        private float stuck;
        private bool hasPath;
        public Vector3 Steer(NetworkTestPlayer player, Vector3 goal, float deltaTime)
        {
            Vector3 position = player.transform.position - Vector3.up;
            goal.y = position.y;
            refresh -= deltaTime;
            if (refresh <= 0 || (lastGoal - goal).sqrMagnitude > 9)
            {
                refresh = .6f; lastGoal = goal; corner = 1;
                hasPath = NavMesh.SamplePosition(position, out var start, 2, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(goal, out var end, 3, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete;
            }
            if (!hasPath) return Vector3.zero;
            var corners = path.corners;
            if (corners.Length == 0) return Vector3.zero;
            while (corner < corners.Length - 1 && Planar(corners[corner] - position).sqrMagnitude < .0004f) corner++;
            Vector3 delta = Planar(corners[Mathf.Min(corner, corners.Length - 1)] - position);
            Vector3 direction = delta.normalized * Mathf.Clamp01(delta.magnitude / .3f);
            if ((position - lastPosition).sqrMagnitude < .00001f) stuck += deltaTime; else stuck = 0;
            lastPosition = position;
            // Move aside when avatars block a narrow path, rather than pushing forever.
            if (stuck > .7f)
            {
                float sign = player.ParticipantNumber % 2 == 0 ? 1 : -1;
                direction = Quaternion.Euler(0, sign * 70, 0) * direction;
                if (stuck > 2) { refresh = 0; stuck = 0; }
            }
            direction = AvoidCollisions(player, direction);
            Vector3 probe = position + direction * .8f;
            if (!NavMesh.SamplePosition(probe, out var hit, .6f, NavMesh.AllAreas)
                || NavMesh.Raycast(position, hit.position, out _, NavMesh.AllAreas)) return Vector3.zero;
            return direction;
        }
        private static Vector3 AvoidCollisions(NetworkTestPlayer player, Vector3 desired)
        {
            if (desired.sqrMagnitude < .00001f || Clear(player, desired)) return desired;
            Vector3 best = Vector3.zero;
            float score = -2;
            foreach (float angle in new[] { 35f, -35f, 70f, -70f, 90f, -90f })
            {
                Vector3 candidate = Quaternion.Euler(0, angle, 0) * desired;
                if (!Clear(player, candidate)) continue;
                float alignment = Vector3.Dot(candidate.normalized, desired.normalized);
                if (alignment > score) { score = alignment; best = candidate; }
            }
            return best;
        }
        private static bool Clear(NetworkTestPlayer player, Vector3 direction)
        {
            var root = player.transform;
            foreach (var hit in Physics.CapsuleCastAll(root.position + Vector3.up * .5f, root.position - Vector3.up * .5f,
                .48f, direction.normalized, .55f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.transform != root && !hit.transform.IsChildOf(root) && hit.normal.y < .7f) return false;
            return true;
        }
        private static Vector3 Planar(Vector3 value) { value.y = 0; return value; }
    }
}
#endif
