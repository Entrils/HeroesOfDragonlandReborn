using System;
using UnityEngine;

namespace HeroesOfDragonland.Player
{
    // Reusable local camera probe. The trigger stays disabled outside synchronous geometry queries.
    internal sealed class CameraClearance
    {
        private RaycastHit[] hits = new RaycastHit[16];
        private Collider[] overlaps = new Collider[16];
        private readonly Predicate<Collider> blocks;
        private readonly SphereCollider probe;

        internal CameraClearance(GameObject owner, Predicate<Collider> blocks)
        {
            this.blocks = blocks;
            probe = owner.AddComponent<SphereCollider>();
            probe.enabled = false;
            probe.isTrigger = true;
            probe.hideFlags = HideFlags.HideAndDontSave;
        }

        internal Vector3 ClearOrigin(PhysicsScene scene, Vector3 origin, float radius, int mask)
        {
            float scale = Mathf.Max(.001f, Mathf.Max(Mathf.Abs(probe.transform.lossyScale.x),
                Mathf.Abs(probe.transform.lossyScale.y), Mathf.Abs(probe.transform.lossyScale.z)));
            float localRadius = radius / scale;
            if (!Mathf.Approximately(probe.radius, localRadius)) probe.radius = localRadius;
            try
            {
                for (int pass = 0; pass < 5; pass++)
                {
                    int count;
                    while ((count = scene.OverlapSphere(origin, radius, overlaps, mask, QueryTriggerInteraction.Ignore)) == overlaps.Length)
                        Array.Resize(ref overlaps, overlaps.Length * 2);
                    bool moved = false;
                    for (int i = 0; i < count; i++)
                    {
                        var other = overlaps[i];
                        if (!blocks(other)) continue;
                        // PhysX needs an enabled shape for ComputePenetration. This trigger is ignored by the casts.
                        if (!probe.enabled) probe.enabled = true;
                        if (!Physics.ComputePenetration(probe, origin, Quaternion.identity, other,
                            other.transform.position, other.transform.rotation, out var direction, out float depth)) continue;
                        origin += direction * (depth + .005f);
                        moved = true;
                    }
                    if (!moved) break;
                }
            }
            finally
            {
                if (probe.enabled) probe.enabled = false;
            }
            return origin;
        }

        internal float Distance(PhysicsScene scene, Vector3 origin, Vector3 offset, float radius, float padding, int mask)
        {
            float length = offset.magnitude;
            if (length < .001f) return 0;
            int count;
            while ((count = scene.SphereCast(origin, radius, offset / length, hits, length, mask, QueryTriggerInteraction.Ignore)) == hits.Length)
                Array.Resize(ref hits, hits.Length * 2);
            float available = length;
            for (int i = 0; i < count; i++)
                if (blocks(hits[i].collider))
                    available = Mathf.Min(available, Mathf.Max(0, hits[i].distance - padding));
            return available;
        }
    }
}
