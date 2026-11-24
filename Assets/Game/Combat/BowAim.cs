using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    internal static class BowAim
    {
        public static Vector3 Read(Camera camera, Transform shooter, float range)
        {
            if (camera == null) return shooter.forward;
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            var origin = BowShot.Origin(shooter);
            float distance = range + Vector3.Distance(ray.origin, origin);
            var point = ray.GetPoint(distance);
            foreach (var hit in Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= distance || hit.collider.transform.IsChildOf(shooter)) continue;
                distance = hit.distance;
                point = hit.point;
            }
            var direction = point - origin;
            return Vector3.Dot(direction, ray.direction) > .01f ? direction.normalized : ray.direction;
        }
    }
}
