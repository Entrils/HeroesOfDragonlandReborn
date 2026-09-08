using System;
using System.Collections.Generic;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Camps
{
    [DisallowMultipleComponent]
    public sealed class Camp : MonoBehaviour
    {
        private sealed class Member
        {
            public EnemyBrain Enemy;
            public CharacterStats Stats;
            public Action DeathHandler;
            public bool Dead;
        }

        [SerializeField] private string campId;
        [SerializeField] private CampDefinition definition;
        [SerializeField] private Transform[] enemySpawnPoints = Array.Empty<Transform>();
        [SerializeField] private Transform chestSpawnPoint;
        [SerializeField] private bool hasAuthority = true;
        private readonly List<Member> members = new List<Member>();
        private readonly List<EnemyBrain> enemies = new List<EnemyBrain>();
        private bool processing;
        private LootChest rewardPrefab;
        private LootTable rewardTable;
        private int rewardExperience;
        public string Id => campId;
        public CampDefinition Definition => definition;
        public bool HasAuthority => hasAuthority;
        public bool IsInitialized { get; private set; }
        public bool IsCleared { get; private set; }
        public int AwardedExperience { get; private set; }
        public int DeadCount { get; private set; }
        public long Revision { get; private set; }
        public IReadOnlyList<EnemyBrain> Enemies => enemies.AsReadOnly();
        public LootChest SpawnedChest { get; private set; }
        public event Action<CampExperienceReward> ExperienceAwarded;
        public event Action<Camp> Cleared;
        public event Action Changed;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(campId)) campId = Guid.NewGuid().ToString("N");
        }

        private void Start()
        {
            if (hasAuthority && !IsInitialized && !TryInitialize(out string reason)) Debug.LogError(reason, this);
        }
        private void OnEnable()
        {
            if (IsInitialized) TryComplete();
        }
        private void OnDestroy()
        {
            foreach (var member in members)
                if (member.Stats != null) member.Stats.Died -= member.DeathHandler;
        }

        // Transport adapters must set authority BEFORE activation/Start. No replicas
        // spawn enemies or grant rewards. Authority changes cannot split completion.
        public bool SetAuthority(bool value)
        {
            if (processing) return false;
            hasAuthority = value;
            foreach (var member in members)
                if (member.Enemy != null) member.Enemy.SetAuthority(value);
            if (SpawnedChest != null) SpawnedChest.SetAuthority(value);
            if (value && isActiveAndEnabled && IsInitialized) TryComplete();
            return true;
        }

        public bool TryInitialize(out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled || !hasAuthority || processing) reason = "Camp initialization requires active authority.";
            else if (IsInitialized) reason = "Camp already initialized.";
            else if (string.IsNullOrWhiteSpace(campId)) reason = "Camp needs a stable session-unique ID.";
            else if (definition == null) reason = "Camp needs a definition.";
            if (reason != null) return false;
            if (!definition.TryValidate(out reason)) return false;
            if (chestSpawnPoint == null || !chestSpawnPoint.IsChildOf(transform))
            { reason = "Chest spawn point must belong to the camp."; return false; }
            if (enemySpawnPoints == null || enemySpawnPoints.Length < definition.EnemyCount)
            { reason = "Provide one distinct enemy spawn point per configured enemy."; return false; }

            var positions = new List<Vector3>();
            var used = new HashSet<Transform>();
            int index = 0;
            foreach (var group in definition.Enemies)
            {
                var agent = group.Prefab.GetComponent<NavMeshAgent>();
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                for (int i = 0; i < group.Count; i++)
                {
                    var point = enemySpawnPoints[index++];
                    if (point == null || !point.IsChildOf(transform) || !used.Add(point)
                        || !NavMesh.SamplePosition(point.position, out var hit, 1f, filter))
                    { reason = "Every enemy spawn must be distinct, camp-owned and on a compatible NavMesh."; return false; }
                    positions.Add(hit.position);
                }
            }

            processing = true;
            var root = new GameObject("Camp Enemies");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            try
            {
                rewardPrefab = definition.ChestPrefab;
                rewardTable = definition.LootTable;
                rewardExperience = definition.ExperienceReward;
                index = 0;
                foreach (var group in definition.Enemies)
                    for (int i = 0; i < group.Count; i++)
                    {
                        var enemy = Instantiate(group.Prefab, positions[index], enemySpawnPoints[index].rotation, root.transform);
                        enemy.gameObject.SetActive(true);
                        if (!enemy.TryConfigureSpawn(group.Definition, hasAuthority))
                            throw new InvalidOperationException("Enemy must support configuration before activation.");
                        var member = new Member { Enemy = enemy, Stats = enemy.GetComponent<CharacterStats>() };
                        member.DeathHandler = () => OnMemberDied(member);
                        member.Stats.Died += member.DeathHandler;
                        members.Add(member);
                        enemies.Add(enemy);
                        index++;
                    }
                IsInitialized = true;
                Revision++;
                root.SetActive(true);
            }
            catch (Exception exception)
            {
                foreach (var member in members)
                    if (member.Stats != null) member.Stats.Died -= member.DeathHandler;
                members.Clear(); enemies.Clear();
                IsInitialized = false;
                Destroy(root);
                reason = exception.Message;
                return false;
            }
            finally { processing = false; }
            Notify(Changed);
            return true;
        }

        private void OnMemberDied(Member member)
        {
            if (IsCleared || member.Dead || !hasAuthority) return;
            member.Dead = true;
            DeadCount++;
            Revision++;
            if (!TryComplete()) Notify(Changed);
        }

        public bool TryComplete()
        {
            if (!hasAuthority || !isActiveAndEnabled || !IsInitialized || IsCleared || processing) return false;
            foreach (var member in members)
            {
                // Despawned/disabled enemies are NOT dead. Keep unresolved membership
                // so removing a live enemy cannot clear a camp or create rewards.
                if (!member.Dead && member.Stats != null && member.Stats.IsDead)
                { member.Dead = true; DeadCount++; Revision++; }
            }
            if (members.Count == 0 || DeadCount != members.Count) return false;
            if (rewardPrefab == null || rewardTable == null || chestSpawnPoint == null) return false;
            processing = true;
            try
            {
                IsCleared = true;
                Revision++;
                AwardedExperience = rewardExperience;
                Notify(ExperienceAwarded, new CampExperienceReward(campId, AwardedExperience));
                var root = new GameObject("Camp Reward");
                root.transform.SetParent(transform, false);
                root.SetActive(false);
                SpawnedChest = Instantiate(rewardPrefab, chestSpawnPoint.position, chestSpawnPoint.rotation, root.transform);
                SpawnedChest.gameObject.SetActive(true);
                SpawnedChest.TryConfigureSpawn(rewardTable, hasAuthority);
                root.SetActive(true);
                Notify(Cleared, this);
                Notify(Changed);
                return true;
            }
            finally { processing = false; }
        }

        // A presentation/progression subscriber must not prevent later subscribers
        // or the chest spawn from completing an already committed clear.
        private void Notify(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
