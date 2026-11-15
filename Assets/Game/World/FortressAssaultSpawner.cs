using System;
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.World;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    // Round authoring/ownership only. Gate health and guard simulation remain components.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkFortressSpawner))]
    public sealed class FortressAssaultSpawner : MonoBehaviour
    {
        [SerializeField] private FortressAssaultDefinition definition;
        private readonly List<NetworkFortressGate> gates = new();
        public FortressAssaultDefinition Definition => definition;
        public bool Owns(NetworkFortressGate gate) => gate != null && gates.Contains(gate)
            && GetComponent<NetworkFortressSpawner>().Match is { } match && match.Object != null && match.Object.IsValid
            && gate.Runner == match.Runner && gate.MatchId == match.Object.Id;
        internal void SpawnRound(NetworkRunner runner)
        {
            var match = GetComponent<NetworkFortressSpawner>().Match;
            if (!runner.IsServer || match == null || !match.IsServerOwner) throw new InvalidOperationException("Only the match server spawns fortress defenses.");
            gates.Clear();
            if (definition == null || definition.Gates.Count != 2) throw new InvalidOperationException("Configure two fortress gates.");
            foreach (var entry in definition.Gates)
                if (entry.Prefab == null || !TeamSpawnService.AllowsNpcSpawn(runner, entry.Prefab.gameObject, entry.Position))
                    throw new InvalidOperationException("Invalid fortress gate spawn.");
            var positions = new Vector3[definition.Guards.Count];
            for (int i = 0; i < positions.Length; i++)
            {
                var entry = definition.Guards[i];
                if (entry.Prefab == null || !NavMesh.SamplePosition(entry.Position, out var hit, 1, NavMesh.AllAreas)
                    || !TeamSpawnService.AllowsNpcSpawn(runner, entry.Prefab.gameObject, hit.position))
                    throw new InvalidOperationException("Invalid fortress guard post.");
                positions[i] = hit.position;
            }
            foreach (var entry in definition.Gates)
                gates.Add(runner.Spawn(entry.Prefab, entry.Position, entry.Rotation, PlayerRef.None,
                    (r, obj) => obj.GetComponent<NetworkFortressGate>().Initialize(match.Object.Id)));
            for (int i = 0; i < positions.Length; i++)
                runner.Spawn(definition.Guards[i].Prefab, positions[i], definition.Guards[i].Rotation, PlayerRef.None);
        }
    }
}
