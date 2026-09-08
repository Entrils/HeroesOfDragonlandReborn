using UnityEngine;

namespace HeroesOfDragonland.Interaction
{
    public static class InteractionReach
    {
        public static bool CanReach(Transform player, Transform target, Vector3 point, float range)
        {
            Vector3 origin = player.position + Vector3.up;
            Vector3 ray = point - origin;
            if (ray.sqrMagnitude > range * range) return false;
            foreach (var hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == player || hit.transform.IsChildOf(player)
                    || hit.transform == target || hit.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }
    }
}
