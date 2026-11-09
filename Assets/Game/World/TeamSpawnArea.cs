using System;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // A planar fortress footprint; its child transforms author the exact spawn poses.
    [DisallowMultipleComponent]
    public sealed class TeamSpawnArea : MonoBehaviour
    {
        [SerializeField] private TeamId team;
        [SerializeField] private Vector2 size = new Vector2(6f, 8f);
        [SerializeField] private Transform[] points = Array.Empty<Transform>();
        [SerializeField] private SpawnZoneRules rules;
        public TeamId Team => team;
        public int PointCount => points.Length;
        public SpawnZoneRules Rules => rules;
        public bool IsProtected => isActiveAndEnabled && rules != null && rules.ProtectedZone;
        public bool Contains(Vector3 position, float padding = 0f)
        {
            var local = transform.InverseTransformPoint(position);
            var half = HalfSize(padding);
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
        }
        private Vector2 HalfSize(float padding)
            => size * .5f + new Vector2(padding / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x)),
                padding / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.z)));
        public bool TryGetPoint(int index, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (!isActiveAndEnabled || index < 0 || index >= points.Length || points[index] == null
                || !points[index].gameObject.activeInHierarchy || !points[index].IsChildOf(transform)) return false;
            position = points[index].position; rotation = points[index].rotation;
            // The complete player radius must remain in its team's authored footprint.
            return Contains(position, -.5f);
        }
        public bool IsSpawnPoint(Vector3 position)
        {
            for (int i = 0; i < points.Length; i++)
                if (TryGetPoint(i, out var point, out _) && (point - position).sqrMagnitude < .0001f) return true;
            return false;
        }
        public bool BlocksEntry(TeamId movingTeam, Vector3 from, Vector3 to, float radius)
        {
            if (!IsProtected || movingTeam == team || rules.EnemyPlayerEntry != EnemySpawnZoneEntry.BlockEntry
                || Contains(from, radius)) return false; // Existing occupants can leave; no forced ejection policy.
            var a = transform.InverseTransformPoint(from);
            var b = transform.InverseTransformPoint(to);
            var half = HalfSize(radius);
            float enter = 0f, exit = 1f;
            return Clip(a.x, b.x - a.x, half.x, ref enter, ref exit)
                && Clip(a.z, b.z - a.z, half.y, ref enter, ref exit);
        }
        private static bool Clip(float start, float delta, float half, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < .000001f) return Mathf.Abs(start) <= half;
            float a = (-half - start) / delta, b = (half - start) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b)); exit = Mathf.Min(exit, Mathf.Max(a, b));
            return enter <= exit;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = team == TeamId.TeamA ? Color.blue : Color.red;
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 2f, size.y));
            Gizmos.matrix = previous;
            foreach (var point in points) if (point != null) Gizmos.DrawWireSphere(point.position, .5f);
        }
    }
}
