using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyPerception : MonoBehaviour
    {
        public bool CanSee(EnemyThreatTarget target)
        {
            if (target == null || !target.IsAlive) return false;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 ray = target.AimPosition - origin;
            foreach (var hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)
                    || hit.transform == target.transform || hit.transform.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }

        public EnemyThreatTarget FindTarget(float radius, Vector3 home, float leash)
        {
            EnemyThreatTarget best = null;
            float distance = radius * radius;
            foreach (var collider in Physics.OverlapSphere(transform.position, radius,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<EnemyThreatTarget>();
                if (target == null || target.transform == transform
                    || (target.transform.position - home).sqrMagnitude > leash * leash) continue;
                float candidateDistance = (target.transform.position - transform.position).sqrMagnitude;
                if (candidateDistance > distance || !CanSee(target)) continue;
                best = target;
                distance = candidateDistance;
            }
            return best;
        }
    }
}
