using System;
using System.Collections.Generic;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Camps
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Camp Definition")]
    public sealed class CampDefinition : ScriptableObject
    {
        [Serializable]
        public struct EnemyGroup
        {
            [SerializeField] private EnemyBrain prefab;
            [SerializeField] private EnemyDefinition definition;
            [SerializeField, Min(1)] private int count;
            public EnemyBrain Prefab => prefab;
            public EnemyDefinition Definition => definition;
            public int Count => count;
        }

        [SerializeField] private EnemyGroup[] enemies = Array.Empty<EnemyGroup>();
        [SerializeField, Min(0)] private int experienceReward = 100;
        [SerializeField] private LootChest chestPrefab;
        [SerializeField] private LootTable lootTable;
        public IReadOnlyList<EnemyGroup> Enemies => Array.AsReadOnly(enemies);
        public int ExperienceReward => experienceReward;
        public LootChest ChestPrefab => chestPrefab;
        public LootTable LootTable => lootTable;
        public int EnemyCount
        {
            get { int count = 0; if (enemies != null) foreach (var group in enemies) count += group.Count; return count; }
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (enemies == null || enemies.Length == 0) reason = "Camp needs at least one enemy group.";
            else if (experienceReward < 0) reason = "XP reward cannot be negative.";
            else if (chestPrefab == null || !chestPrefab.enabled || chestPrefab.transform.parent != null)
                reason = "Assign an enabled LootChest on a prefab root.";
            else if (lootTable == null) reason = "Camp needs a loot table.";
            if (reason != null) return false;
            if (!lootTable.TryValidate(null, out reason)) return false;
            int total = 0;
            foreach (var group in enemies)
            {
                if (group.Count < 1 || group.Count > 32 || total + group.Count > 32)
                    reason = "Camp must contain between 1 and 32 enemies.";
                else if (group.Definition == null || group.Definition.Stats == null)
                    reason = "Each group needs an enemy definition with stats.";
                else if (group.Prefab == null || !group.Prefab.enabled || group.Prefab.transform.parent != null
                    || !group.Prefab.TryGetComponent<NavMeshAgent>(out var agent) || !agent.enabled)
                    reason = "Each group needs an enabled enemy prefab root with NavMeshAgent.";
                if (reason != null) return false;
                total += group.Count;
            }
            return true;
        }
    }
}
