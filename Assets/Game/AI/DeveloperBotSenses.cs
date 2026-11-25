#if UNITY_EDITOR || DEBUG
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Multiplayer
{
    internal sealed class DeveloperBotSenses
    {
        private readonly NetworkTestPlayer player;
        private RaycastHit[] hits = new RaycastHit[12];

        internal DeveloperBotSenses(NetworkTestPlayer player) => this.player = player;

        internal bool CanSee(Transform root, DeveloperBotTuning tuning)
        {
            if (root == null) return false;
            Vector3 origin = player.transform.position + Vector3.up * .55f;
            var collider = root.GetComponent<Collider>();
            Vector3 center = collider != null && collider.enabled ? collider.bounds.center : root.position + Vector3.up * .5f;
            Vector3 offset = center - origin;
            if (offset.sqrMagnitude > tuning.sightRange * tuning.sightRange) return false;
            Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (planar.sqrMagnitude > 9 && Vector3.Angle(player.transform.forward, planar) > tuning.sightArc * .5f)
                return false;
            if (Clear(origin, center, root)) return true;
            return collider != null && collider.enabled && Clear(origin, collider.bounds.ClosestPoint(origin), root);
        }

        private bool Clear(Vector3 origin, Vector3 point, Transform root)
        {
            Vector3 offset = point - origin;
            float length = offset.magnitude;
            if (length < .001f) return true;
            var scene = player.gameObject.scene.GetPhysicsScene();
            int count;
            while ((count = scene.Raycast(origin, offset / length, hits, length, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)) == hits.Length)
                Array.Resize(ref hits, hits.Length * 2);
            var self = player.transform;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].transform;
                if (hit == self || hit.IsChildOf(self) || hit == root || hit.IsChildOf(root)) continue;
                return false;
            }
            return true;
        }
    }
}
#endif
