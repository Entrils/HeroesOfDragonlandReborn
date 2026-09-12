using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class NetworkEnemySpawner : MonoBehaviour
    {
        [Serializable]
        private struct SpawnEntry
        {
            [SerializeField] private NetworkObject prefab;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private float yaw;
            internal NetworkObject Prefab => prefab;
            internal Vector3 LocalPosition => localPosition;
            internal float Yaw => yaw;
        }
        [SerializeField] private SpawnEntry[] entries = Array.Empty<SpawnEntry>();
        private MultiplayerBootstrap bootstrap;
        private readonly List<NetworkEnemy> enemies = new List<NetworkEnemy>();
        public IReadOnlyList<NetworkEnemy> Enemies => enemies.AsReadOnly();
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void OnEnable() => bootstrap.SessionStarted += Spawn;
        private void OnDisable() => bootstrap.SessionStarted -= Spawn;
        private void Spawn(NetworkRunner runner)
        {
            enemies.Clear();
            if (!runner.IsServer) return;
            // Validate all points before spawning any enemy. No client spawn request exists.
            var positions = new Vector3[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var prefab = entries[i].Prefab;
                if (prefab == null || prefab.GetComponent<NetworkEnemy>() == null)
                    throw new InvalidOperationException("Assign a network enemy prefab to every spawn entry.");
                var agent = prefab.GetComponent<NavMeshAgent>();
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(transform.TransformPoint(entries[i].LocalPosition), out var hit, 1f, filter))
                    throw new InvalidOperationException("Network enemy spawn is off the compatible NavMesh.");
                positions[i] = hit.position;
            }
            for (int i = 0; i < entries.Length; i++)
            {
                int index = i;
                var obj = runner.Spawn(entries[i].Prefab, positions[i], Quaternion.Euler(0, entries[i].Yaw, 0), PlayerRef.None,
                    (r, actor) => actor.GetComponent<NetworkEnemy>().Initialize(index));
                enemies.Add(obj.GetComponent<NetworkEnemy>());
            }
        }
    }
}
