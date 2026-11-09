using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain), typeof(NetworkCharacterStats), typeof(NetworkTransform))]
    public sealed class NetworkEnemy : NetworkBehaviour, IAfterTick, IEnemyAuthority, IEnemyTargetPolicy, IDamagePolicy
    {
        [Networked] public NetworkBool Staggered { get; private set; }
        [Networked] public EnemyState State { get; private set; }
        [Networked] public NetworkId TargetId { get; private set; }
        [Networked] public int AttackSequence { get; private set; }
        [Networked] public int AttackStartTick { get; private set; }
        [Networked] public int AttackEndTick { get; private set; }
        [Networked] public NetworkBool AttackBusy { get; private set; }
        [Networked] public int SpawnIndex { get; private set; }
        private EnemyBrain brain;
        private EnemyMeleeAttack attack;
        private CharacterStats stats;
        private NavMeshAgent agent;
        private Collider[] colliders;
        private bool replicaDeathApplied;
        private bool initializing;
        public int SimulationTicks { get; private set; }
        public EnemyDefinition Definition => brain.Definition;
        private bool IsServerOwner => isActiveAndEnabled && Object != null && Object.IsValid
            && Runner.IsServer && HasStateAuthority && Object.InputAuthority == PlayerRef.None;
        public bool CanSimulateEnemy => IsServerOwner && (initializing || NetworkMatch.AllowsGameplay(Runner));
        public bool IsResolvingAttack => CanSimulateEnemy && !stats.IsDead && attack.ResolvingHit;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            attack = GetComponent<EnemyMeleeAttack>();
            stats = GetComponent<CharacterStats>();
            agent = GetComponent<NavMeshAgent>();
            colliders = GetComponentsInChildren<Collider>();
            // Saved prefabs also have the agent disabled, before any spawn/authority callbacks.
            agent.enabled = false;
        }
        internal void Initialize(int index) { if (Runner.IsServer) SpawnIndex = index; }
        public override void Spawned()
        {
            Object.ForceRemoteRenderTimeframe = !HasStateAuthority;
            if (!IsServerOwner)
            {
                brain.enabled = false;
                attack.enabled = false;
                GetComponent<EnemyPerception>().enabled = false;
                GetComponent<EnemyNavigation>().enabled = false;
                agent.enabled = false;
                return;
            }
            // Authoring initialization is allowed during Starting; gameplay remains gated.
            initializing = true;
            try
            {
                GetComponent<NetworkCharacterStats>().InitializeForSpawn(brain.Definition.Stats);
                brain.InitializeNetworkSpawn();
            }
            finally { initializing = false; }
            attack.AttackStarted += OnAttackStarted;
            attack.AttackFinished += OnAttackFinished;
            stats.Died += OnDeath;
            // Keep NavMesh simulation separate from Fusion's render/restoration writes.
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                Debug.LogError("Network enemy spawn is not on its NavMesh.", this);
                enabled = false;
            }
            Publish();
        }
        public override void FixedUpdateNetwork()
        {
            if (IsServerOwner && !NetworkMatch.AllowsGameplay(Runner))
            {
                GetComponent<EnemyNavigation>().Stop();
                attack.Cancel();
                AttackBusy = false;
                TargetId = default;
                return;
            }
            if (!CanSimulateEnemy || stats.IsDead || !brain.enabled || !agent.enabled || !agent.isOnNavMesh) return;
            transform.position = agent.nextPosition;
            if (!attack.IsBusy && agent.velocity.sqrMagnitude > 0.0001f)
                GetComponent<EnemyNavigation>().Face(transform.position + agent.velocity, Runner.DeltaTime);
        }
        public void AfterTick()
        {
            if (!CanSimulateEnemy || stats.IsDead || !brain.enabled) return;
            // Server navigation pose was committed in FixedUpdateNetwork. Only the
            // server chooses targets and advances combat after player movement ticks.
            Physics.SyncTransforms();
            SimulationTicks++;
            brain.Tick(Runner.DeltaTime);
            Publish();
        }
        private void Publish()
        {
            Staggered = GetComponent<CombatDefense>() is { IsStaggered: true };
            State = brain.State;
            var target = brain.AggroTarget != null ? brain.AggroTarget.GetComponent<NetworkObject>() : null;
            TargetId = target != null && target.IsValid ? target.Id : default;
            AttackBusy = attack.IsBusy && !stats.IsDead;
        }
        private void OnAttackStarted()
        {
            AttackSequence++;
            AttackStartTick = Runner.Tick.Raw;
            AttackEndTick = 0;
            AttackBusy = true;
        }
        private void OnAttackFinished() { AttackEndTick = Runner.Tick.Raw; AttackBusy = false; }
        private void OnDeath()
        {
            State = EnemyState.Dead; TargetId = default; AttackBusy = false;
            attack.Cancel();
            agent.enabled = false;
            foreach (var collider in colliders) if (collider != null) collider.enabled = false;
        }

        public bool AllowsTarget(EnemyThreatTarget target)
        {
            if (!CanSimulateEnemy || target == null) return false;
            var player = target.GetComponent<NetworkPlayerCombat>();
            return player != null && player.Runner == Runner && player.IsAdmitted() && target.IsAlive;
        }
        public bool AllowsDamage(GameObject source)
        {
            if (!CanSimulateEnemy || stats.IsDead || source == null) return false;
            var player = source.GetComponent<NetworkPlayerCombat>();
            return player != null && player.Runner == Runner && player.IsAdmitted()
                && player.ResolvingAttack && !source.GetComponent<CharacterStats>().IsDead;
        }
        public override void Render()
        {
            if (HasStateAuthority || replicaDeathApplied || !GetComponent<NetworkCharacterStats>().Dead) return;
            replicaDeathApplied = true;
            foreach (var collider in colliders) if (collider != null) collider.enabled = false;
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            attack.AttackStarted -= OnAttackStarted;
            attack.AttackFinished -= OnAttackFinished;
            stats.Died -= OnDeath;
            agent.enabled = false;
        }
        private void OnDisable()
        {
            if (agent != null) agent.enabled = false;
            if (attack != null) attack.Cancel();
        }
    }
}
