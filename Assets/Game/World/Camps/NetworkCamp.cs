using System;
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    public enum NetworkCampState { Pending, Active, Cleared, Faulted }

    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkCamp : NetworkBehaviour
    {
        private sealed class Member
        {
            internal NetworkEnemy Enemy;
            internal CharacterStats Stats;
            internal Action OnDeath;
            internal bool Dead;
            internal readonly DamageContributionLedger Contributions = new();
            internal Action<float, GameObject> OnDamage;
            internal HashSet<NetworkId> BossParticipants;
            internal Action<EnemyState> OnState;
        }
        [SerializeField] private NetworkCampDefinition definition;
        [Networked] public NetworkCampState State { get; private set; }
        [Networked] public int EnemyCount { get; private set; }
        [Networked] public int DeadCount { get; private set; }
        [Networked] public int CompletionCount { get; private set; }
        [Networked] public int AwardedExperience { get; private set; }
        [Networked] public int RewardedPlayers { get; private set; }
        [Networked] public NetworkId ChestId { get; private set; }
        private readonly List<Member> members = new List<Member>();
        private bool processing;
        private NetworkLootChest rewardPrefab;
        private Vector3 chestPosition;
        private int rewardExperience;
        private float experienceRadius;
        private TickTimer chestRetry;
        [Networked] public TickTimer NextSpawnTimer { get; private set; }
        private bool scheduleStarted;
        public NetworkCampDefinition Definition => definition;
        public int AvailableExperience => TryPacing(out var profile) ? profile.Experience(IsLate) : definition != null ? definition.ExperienceReward : 0;
        public float ConfiguredFirstSpawn => TryPacing(out var profile) ? profile.FirstSpawnSeconds : definition != null ? definition.InitialSpawnDelay : 0;
        public float ConfiguredRespawn => TryPacing(out var profile) ? profile.RespawnSeconds(IsLate) : definition != null ? definition.RespawnDelay : 0;
        public float ConfiguredRadius => TryPacing(out var profile) ? profile.RewardRadius : definition != null ? definition.ExperienceRadius : 0;
        private bool IsLate => NetworkMatchPacing.MatchFor(Runner) is { } match && match.Balance != null && match.Balance.IsLate(match.PlayingSeconds);
        private bool TryPacing(out MatchBalanceConfig.CampPacing profile)
        {
            var balance = NetworkMatchPacing.BalanceFor(Runner);
            if (balance != null && definition != null) return balance.TryGetCamp(definition.Difficulty, out profile);
            profile = default; return false;
        }
        public bool CanMutateCamp => isActiveAndEnabled && Object != null && Object.IsValid
            && Runner.IsServer && HasStateAuthority && Object.InputAuthority == PlayerRef.None && NetworkMatch.AllowsGameplay(Runner);
        public event Action<NetworkCamp> Cleared;

        public override void FixedUpdateNetwork()
        {
            if (!CanMutateCamp) return;
            if (!scheduleStarted)
            {
                scheduleStarted = true;
                if (ConfiguredFirstSpawn > 0)
                    NextSpawnTimer = TickTimer.CreateFromSeconds(Runner, ConfiguredFirstSpawn);
            }
            if (State == NetworkCampState.Pending && NextSpawnTimer.ExpiredOrNotRunning(Runner)) TryInitialize();
            else if (State == NetworkCampState.Active) TryComplete();
            else if (State == NetworkCampState.Cleared)
            {
                if (ConfiguredRespawn > 0 && NextSpawnTimer.Expired(Runner)) BeginNextCycle();
                else if (ChestId == default && chestRetry.ExpiredOrNotRunning(Runner)) SpawnChest();
            }
        }
        private void BeginNextCycle()
        {
            ReleaseMembers(true);
            if (ChestId != default && Runner.TryFindObject(ChestId, out var chest)) Runner.Despawn(chest);
            ChestId = default;
            DeadCount = 0; EnemyCount = 0; AwardedExperience = 0; RewardedPlayers = 0;
            chestRetry = TickTimer.None; NextSpawnTimer = TickTimer.None;
            State = NetworkCampState.Pending;
        }
#if UNITY_EDITOR || DEBUG
        // Explicit acceptance fixture only; ordinary matches retain the configured spawn schedule.
        internal bool TryStartDevelopmentEncounter(string flag)
        {
            if (!CanMutateCamp || State != NetworkCampState.Pending
                || Array.IndexOf(Environment.GetCommandLineArgs(), flag) < 0) return false;
            scheduleStarted = true; NextSpawnTimer = TickTimer.None;
            return TryInitialize();
        }
#endif
        public bool TryInitialize()
        {
            if (!CanMutateCamp || State != NetworkCampState.Pending || processing
                || (!scheduleStarted && ConfiguredFirstSpawn > 0)
                || !NextSpawnTimer.ExpiredOrNotRunning(Runner)) return false;
            processing = true;
            try
            {
                if (definition == null) throw new InvalidOperationException("Assign a network camp definition.");
                if (!definition.TryValidate(out var reason)) throw new InvalidOperationException(reason);
                var positions = new Vector3[definition.Enemies.Count];
                for (int i = 0; i < positions.Length; i++)
                {
                    var entry = definition.Enemies[i];
                    var agent = entry.Prefab.GetComponent<NavMeshAgent>();
                    var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                    if (!NavMesh.SamplePosition(transform.TransformPoint(entry.Offset), out var hit, 1f, filter))
                        throw new InvalidOperationException("Camp enemy spawn is off its compatible NavMesh.");
                    positions[i] = hit.position;
                    if (!TeamSpawnService.AllowsNpcSpawn(Runner, entry.Prefab.gameObject, positions[i]))
                        throw new InvalidOperationException("Camp enemy spawn overlaps a protected opposing spawn zone.");
                }
                rewardPrefab = definition.ChestPrefab;
                rewardExperience = definition.ExperienceReward;
                experienceRadius = definition.ExperienceRadius;
                chestPosition = transform.TransformPoint(definition.ChestOffset);
                for (int i = 0; i < positions.Length; i++)
                {
                    int index = i;
                    var entry = definition.Enemies[i];
                    var obj = Runner.Spawn(entry.Prefab.GetComponent<NetworkObject>(), positions[i],
                        transform.rotation * Quaternion.Euler(0, entry.Yaw, 0), PlayerRef.None,
                        (runner, actor) => actor.GetComponent<NetworkEnemy>().Initialize(index));
                    if (obj == null) throw new InvalidOperationException("Camp enemy spawn failed.");
                    var member = new Member { Enemy = obj.GetComponent<NetworkEnemy>(), Stats = obj.GetComponent<CharacterStats>() };
                    member.OnDeath = () => OnMemberDied(member);
                    members.Add(member);
                    member.Stats.Died += member.OnDeath;
                    member.OnDamage = (amount, source) => {
                        if (!CanMutateCamp || State != NetworkCampState.Active || source == null) return;
                        var actor = source.GetComponent<NetworkObject>();
                        if (actor == null || actor.Runner != Runner) return;
                        var balance = NetworkMatchPacing.BalanceFor(Runner);
                        if (balance != null) member.Contributions.Record(actor, amount, Runner.SimulationTime, balance.CampContributionWindowSeconds);
                        if (member.Enemy.Definition.Boss == null || !NetworkParticipant.IsAdmitted(actor)) return;
                        member.BossParticipants ??= new HashSet<NetworkId>();
                        if (member.BossParticipants.Add(actor.Id))
                            MatchSessionData.Credit(actor, new MatchStatistics { BossParticipation = 1 });
                    };
                    member.OnState = state => { if (state == EnemyState.Return) member.Contributions.Clear(); };
                    member.Enemy.GetComponent<DamageReceiver>().DamageTaken += member.OnDamage;
                    member.Enemy.GetComponent<EnemyBrain>().StateChanged += member.OnState;
                }
                EnemyCount = members.Count;
                State = NetworkCampState.Active;
                return true;
            }
            catch (Exception exception)
            {
                State = NetworkCampState.Faulted;
                ReleaseMembers(true);
                Debug.LogError("Camp initialization failed: " + exception.Message, this);
                return false;
            }
            finally { processing = false; }
        }
        private void OnMemberDied(Member member)
        {
            if (!CanMutateCamp || State != NetworkCampState.Active || member.Dead || !member.Stats.IsDead) return;
            member.Dead = true;
            DeadCount++;
            // Commit on the next network tick, after the lethal attack/stat events finish.
        }
        public bool TryComplete()
        {
            if (!CanMutateCamp || State != NetworkCampState.Active || processing || members.Count == 0) return false;
            foreach (var member in members)
            {
                // A missing/despawned LIVE member never counts as a kill.
                if (!member.Dead && member.Stats != null && member.Stats.IsDead) OnMemberDied(member);
                if (!member.Dead) return false;
            }
            processing = true;
            try
            {
                // Commit the one-way transition before any reward or observer can re-enter.
                State = NetworkCampState.Cleared;
                CompletionCount++;
                NextSpawnTimer = ConfiguredRespawn > 0 ? TickTimer.CreateFromSeconds(Runner, ConfiguredRespawn) : TickTimer.None;
                AwardedExperience = AvailableExperience;
                string rewardId = "camp:" + Object.Id + ":" + CompletionCount;
                var balance = NetworkMatchPacing.BalanceFor(Runner);
                if (balance != null) RewardContributors(balance, rewardId);
                // Snapshot recipients at completion. No late-join replay or distance/client claims.
                else foreach (var avatar in Runner.GetComponentInParent<MultiplayerBootstrap>().Avatars)
                    if (avatar != null)
                    {
                        if (experienceRadius > 0)
                        {
                            var stats = avatar.GetComponent<CharacterStats>();
                            Vector3 distance = avatar.transform.position - transform.position; distance.y = 0;
                            if (stats == null || stats.IsDead || distance.sqrMagnitude > experienceRadius * experienceRadius) continue;
                        }
                        var progression = avatar.GetComponent<NetworkSessionProgression>();
                        MatchSessionData.Credit(avatar, new MatchStatistics { CampsCleared = 1 });
                        if (progression != null && progression.TryGrantExperience(rewardExperience, rewardId)) RewardedPlayers++;
                    }
                SpawnChest();
                if (Cleared != null) foreach (Action<NetworkCamp> handler in Cleared.GetInvocationList())
                    try { handler(this); } catch (Exception exception) { Debug.LogException(exception, this); }
                return true;
            }
            finally { processing = false; }
        }
        private void RewardContributors(MatchBalanceConfig balance, string rewardId)
        {
            var contributors = new Dictionary<NetworkId, CombatContributionHistory<NetworkObject>.Contribution>();
            double now = Runner.SimulationTime, a = 0, b = 0;
            foreach (var member in members)
                foreach (var entry in member.Contributions.Recent(now, balance.CampContributionWindowSeconds, balance.MinimumContributionDamage))
                {
                    if (entry.Actor.GetComponent<NetworkTestPlayer>() is not { } player) continue;
                    if (player.Team == TeamId.TeamA) a += entry.Damage;
                    else if (player.Team == TeamId.TeamB) b += entry.Damage;
                    else continue;
                    contributors[entry.Actor.Id] = entry;
                }
            // Equal contribution is contested: no team gets XP; the physical loot remains available.
            TeamId winner = a > b ? TeamId.TeamA : b > a ? TeamId.TeamB : TeamId.Unassigned;
            var pacing = NetworkMatchPacing.MatchFor(Runner)?.GetComponent<NetworkMatchPacing>();
            foreach (var entry in contributors.Values)
            {
                var actor = entry.Actor;
                if (actor.GetComponent<NetworkTestPlayer>().Team != winner || actor.GetComponent<CharacterStats>().IsDead) continue;
                Vector3 offset = actor.transform.position - transform.position; offset.y = 0;
                if (offset.sqrMagnitude > ConfiguredRadius * ConfiguredRadius) continue;
                MatchSessionData.Credit(actor, new MatchStatistics { CampsCleared = 1 });
                int reward = Mathf.RoundToInt(AwardedExperience * (pacing != null ? pacing.CampMultiplier(winner) : 1));
                if (actor.GetComponent<NetworkSessionProgression>()?.TryGrantExperience(reward, rewardId) == true) RewardedPlayers++;
            }
        }
        private void SpawnChest()
        {
            if (!CanMutateCamp || State != NetworkCampState.Cleared || ChestId != default) return;
            chestRetry = TickTimer.CreateFromSeconds(Runner, 1);
            try
            {
                var chest = Runner.Spawn(rewardPrefab.GetComponent<NetworkObject>(), chestPosition, transform.rotation,
                    PlayerRef.None, (runner, obj) => obj.GetComponent<NetworkLootChest>().Initialize(Object.Id));
                if (chest == null) throw new InvalidOperationException("Camp chest spawn returned null.");
                ChestId = chest.Id;
            }
            catch (Exception exception) { Debug.LogWarning("Camp chest spawn will retry: " + exception.Message, this); }
        }
        private void ReleaseMembers(bool despawn)
        {
            foreach (var member in members)
            {
                if (member.Stats != null) member.Stats.Died -= member.OnDeath;
                if (member.Enemy != null)
                {
                    member.Enemy.GetComponent<DamageReceiver>().DamageTaken -= member.OnDamage;
                    member.Enemy.GetComponent<EnemyBrain>().StateChanged -= member.OnState;
                }
                if (despawn && member.Enemy != null && member.Enemy.Object != null && member.Enemy.Object.IsValid)
                    Runner.Despawn(member.Enemy.Object);
            }
            members.Clear();
        }
        public override void Despawned(NetworkRunner runner, bool hasState) => ReleaseMembers(false);
    }
}
