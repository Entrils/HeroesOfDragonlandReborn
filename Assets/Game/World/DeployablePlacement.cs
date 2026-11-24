using System;
using Fusion;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Multiplayer
{
    internal static class DeployablePlacement
    {
        internal const int ActiveLimit = 3;
        private static RaycastHit[] lineHits = new RaycastHit[16];

        public static bool TryFind(NetworkRunner runner, NetworkObject owner, DeployableDefinition definition,
            Vector3 requested, out Vector3 point, out string reason)
        {
            point = default;
            reason = "Choose clear walkable ground within placement range.";
            var offset = requested - owner.transform.position;
            if (!float.IsFinite(offset.sqrMagnitude) || offset.sqrMagnitude > definition.PlacementRange * definition.PlacementRange)
                return false;
            var physics = owner.gameObject.scene.GetPhysicsScene();
            if (!physics.Raycast(requested + Vector3.up * .35f, Vector3.down, out var floor, .7f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || floor.normal.y < .8f || floor.collider.GetComponentInParent<NetworkObject>() != null
                || !NavMesh.SamplePosition(floor.point, out var walkable, .25f, NavMesh.AllAreas)
                || (walkable.position - floor.point).sqrMagnitude > .0625f)
                return false;
            point = floor.point;
            offset = point - owner.transform.position;
            if (offset.sqrMagnitude > definition.PlacementRange * definition.PlacementRange ||
                offset.x * offset.x + offset.z * offset.z < 1)
                return false;
            if (physics.LinecastToActor(owner.transform.position, point + Vector3.up * .16f, owner.transform)) return false;
            float radius = definition.FootprintRadius;
            var clearance = new Collider[1];
            if (physics.OverlapBox(point + Vector3.up * (definition.ClearanceHeight * .5f + .06f),
                    new Vector3(radius, definition.ClearanceHeight * .5f, radius), clearance,
                    Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) != 0)
                return false;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                if (!physics.Raycast(point + direction * radius + Vector3.up * .32f, Vector3.down,
                        out var edge, .62f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || edge.normal.y < .8f || edge.collider.GetComponentInParent<NetworkObject>() != null) return false;
            }
            var spawns = runner.GetComponentInParent<TeamSpawnService>();
            if (spawns != null && !spawns.AllowsSpawn(TeamId.Unassigned, point, radius))
            { reason = "Traps cannot be placed in protected spawn areas."; return false; }

            int owned = 0;
            foreach (var trap in UnityEngine.Object.FindObjectsByType<NetworkDeployable>())
            {
                if (trap.Object == null || !trap.Object.IsValid || trap.Runner != runner) continue;
                if (trap.OwnerId == owner.Id) owned++;
                float spacing = Mathf.Max(definition.MinimumSpacing, trap.Definition.MinimumSpacing);
                var delta = point - trap.WorldPosition;
                if (delta.x * delta.x + delta.z * delta.z < spacing * spacing)
                { reason = "Place the trap farther from existing traps."; return false; }
            }
            if (owned >= ActiveLimit) { reason = "You already have three active traps."; return false; }
            reason = null;
            return true;
        }

        // Ignore the actor's own colliders. Scene geometry and other actors still occlude the segment.
        public static bool LinecastToActor(this PhysicsScene scene, Vector3 start, Vector3 end, Transform ignored,
            bool ignorePlayers = false)
        {
            var offset = end - start;
            float length = offset.magnitude;
            if (length < .001f) return false;
            int count;
            while ((count = scene.Raycast(start, offset / length, lineHits, length, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)) == lineHits.Length)
                Array.Resize(ref lineHits, lineHits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var collider = lineHits[i].collider;
                if (!collider.transform.IsChildOf(ignored)
                    && (!ignorePlayers || collider.GetComponentInParent<NetworkTestPlayer>() == null)) return true;
            }
            return false;
        }
    }
}
