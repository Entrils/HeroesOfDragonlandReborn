using System;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Round-owned objects are recreated from their authored defaults. Connections
    // and the server's team roster live on the bootstrap and survive this boundary.
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class NetworkRoundReset : MonoBehaviour
    {
        private bool IsOwner(NetworkRunner runner)
            => runner != null && runner.IsServer && runner == GetComponent<MultiplayerBootstrap>().Runner;

        internal void ClearWorld(NetworkRunner runner)
        {
            if (!IsOwner(runner)) throw new InvalidOperationException("Only this session's server can reset its world.");
            foreach (var obj in FindObjectsByType<NetworkObject>(FindObjectsInactive.Include))
            {
                if (!obj.IsValid || obj.Runner != runner) continue;
                if (obj.GetComponent<NetworkEnemy>() != null || obj.GetComponent<NetworkKingObjective>() != null
                    || obj.GetComponent<NetworkCamp>() != null || obj.GetComponent<NetworkLootChest>() != null)
                {
                    foreach (var collider in obj.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    runner.Despawn(obj);
                }
            }
        }

        internal void ResetPlayers(NetworkRunner runner)
        {
            if (!IsOwner(runner)) throw new InvalidOperationException("Only this session's server can reset players.");
            GetComponent<MultiplayerBootstrap>().ResetRoundPlayers();
        }

        internal void SpawnWorld(NetworkRunner runner)
        {
            if (!IsOwner(runner)) throw new InvalidOperationException("Only this session's server can create its world.");
            GetComponent<NetworkEnemySpawner>()?.SpawnRound(runner);
            GetComponent<NetworkCampSpawner>()?.SpawnRound(runner);
        }
    }
}
