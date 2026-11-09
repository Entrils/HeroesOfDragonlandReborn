using System;
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Camps
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Network Camp Definition")]
    public sealed class NetworkCampDefinition : ScriptableObject
    {
        [Serializable]
        public struct Member
        {
            [SerializeField] private NetworkEnemy prefab;
            [SerializeField] private Vector3 offset;
            [SerializeField] private float yaw;
            public NetworkEnemy Prefab => prefab;
            public Vector3 Offset => offset;
            public float Yaw => yaw;
        }
        [SerializeField] private Member[] enemies = Array.Empty<Member>();
        [SerializeField, Min(0)] private int experienceReward = 100;
        [Tooltip("Zero preserves legacy room-wide XP. Positive values require a living player near the cleared camp.")]
        [SerializeField, Min(0)] private float experienceRadius;
        [SerializeField] private NetworkLootChest chestPrefab;
        [SerializeField] private Vector3 chestOffset;
        [SerializeField, Min(0)] private float initialSpawnDelay;
        [Tooltip("Zero keeps this camp single-use. Measured from clear, not chest collection.")]
        [SerializeField, Min(0)] private float respawnDelay;
        public float InitialSpawnDelay => initialSpawnDelay;
        public float RespawnDelay => respawnDelay;
        public IReadOnlyList<Member> Enemies => Array.AsReadOnly(enemies);
        public int ExperienceReward => experienceReward;
        public float ExperienceRadius => experienceRadius;
        public NetworkLootChest ChestPrefab => chestPrefab;
        public Vector3 ChestOffset => chestOffset;
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (enemies == null || enemies.Length == 0 || enemies.Length > 32) reason = "Configure 1 to 32 camp enemies.";
            else if (experienceReward < 0) reason = "Camp XP cannot be negative.";
            else if (initialSpawnDelay < 0 || respawnDelay < 0 || float.IsNaN(initialSpawnDelay) || float.IsInfinity(initialSpawnDelay) || float.IsNaN(respawnDelay) || float.IsInfinity(respawnDelay)) reason = "Camp schedule must be finite and nonnegative.";
            else if (experienceRadius < 0 || float.IsNaN(experienceRadius) || float.IsInfinity(experienceRadius)) reason = "Camp XP radius must be finite and nonnegative.";
            else if (chestPrefab == null || !chestPrefab.enabled || chestPrefab.transform.parent != null
                || chestPrefab.GetComponent<NetworkObject>() == null || !chestPrefab.GetComponent<LootChest>().enabled
                || chestPrefab.GetComponent<LootChest>().Table == null) reason = "Assign a network chest prefab with a loot table.";
            if (reason != null) return false;
            if (!chestPrefab.GetComponent<LootChest>().Table.TryValidate(null, out reason)) return false;
            foreach (var entry in enemies)
            {
                var prefab = entry.Prefab;
                if (prefab == null || !prefab.enabled || prefab.transform.parent != null
                    || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NavMeshAgent>() == null
                    || prefab.GetComponent<NavMeshAgent>().enabled || prefab.GetComponent<EnemyBrain>().Definition == null
                    || prefab.GetComponent<EnemyBrain>().Definition.Stats == null)
                { reason = "Use network enemy prefab roots with configured definitions and disabled NavMeshAgents."; return false; }
            }
            return true;
        }
    }
}
