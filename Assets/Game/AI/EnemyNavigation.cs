using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        private NavMeshAgent agent;
        private NavMeshPath path;
        private NavMeshAgent Agent => agent != null ? agent : agent = GetComponent<NavMeshAgent>();
        public bool IsReady => Agent.enabled && Agent.isOnNavMesh;
        public bool HasArrived => IsReady && !Agent.pathPending && (!Agent.hasPath || Agent.remainingDistance <= Agent.stoppingDistance + 0.1f);
        public bool MoveTo(Vector3 destination, float speed, float stoppingDistance)
        {
            if (path == null) path = new NavMeshPath();
            if (!IsReady || !NavMesh.SamplePosition(destination, out var hit, 1.5f, Agent.areaMask)
                || !Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            Agent.speed = speed;
            Agent.stoppingDistance = stoppingDistance;
            Agent.isStopped = false;
            return Agent.SetPath(path);
        }
        public void Stop()
        {
            if (!IsReady) return;
            Agent.ResetPath();
            // ResetPath clears the stop flag in the current Unity runtime.
            Agent.isStopped = true;
        }
        public void Face(Vector3 point, float deltaTime)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540 * deltaTime);
        }
        public void Die() { Stop(); Agent.enabled = false; }
        private void OnDisable() => Stop();
    }
}
