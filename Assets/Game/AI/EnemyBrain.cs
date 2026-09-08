using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    public enum EnemyState { Idle = 0, Patrol = 1, Alert = 2, Chase = 3, Attack = 4, Return = 5, Dead = 6 }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyNavigation), typeof(EnemyPerception), typeof(EnemyMeleeAttack))]
    [RequireComponent(typeof(DamageReceiver), typeof(Targetable))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private bool hasAuthority = true;
        private CharacterStats stats;
        private DamageReceiver receiver;
        private EnemyNavigation navigation;
        private EnemyPerception perception;
        private EnemyMeleeAttack attack;
        private float stateTime;
        private float navigationTimer;
        private float unseenTime;
        private int patrolIndex;
        private bool initialized;
        // Camp/factory configures an inactive instance before Awake initializes stats.
        public bool TryConfigureSpawn(EnemyDefinition value, bool authority)
        {
            if (initialized || gameObject.activeInHierarchy || value == null || value.Stats == null) return false;
            definition = value;
            hasAuthority = authority;
            return true;
        }
        public EnemyDefinition Definition => definition;
        public bool HasAuthority => hasAuthority;
        public EnemyState State { get; private set; }
        public EnemyThreatTarget AggroTarget { get; private set; }
        public Vector3 SpawnPosition { get; private set; }
        public event Action<EnemyState> StateChanged;
        public void SetAuthority(bool value)
        {
            hasAuthority = value;
            if (!value) { navigation.Stop(); attack.Cancel(); }
        }

        private void Awake()
        {
            initialized = true;
            stats = GetComponent<CharacterStats>();
            receiver = GetComponent<DamageReceiver>();
            navigation = GetComponent<EnemyNavigation>();
            perception = GetComponent<EnemyPerception>();
            attack = GetComponent<EnemyMeleeAttack>();
            SpawnPosition = transform.position;
            if (definition == null || definition.Stats == null)
            { Debug.LogError("Enemy needs an EnemyDefinition with a stats profile.", this); enabled = false; return; }
            stats.ConfigureForSpawn(definition.Stats);
            attack.Configure(definition);
        }
        private void OnEnable()
        {
            stats.Died += Die;
            receiver.DamageTaken += OnDamaged;
            if (stats.IsDead) Die();
        }
        private void OnDisable()
        {
            stats.Died -= Die;
            receiver.DamageTaken -= OnDamaged;
            navigation.Stop();
            attack.Cancel();
        }
        private void Update() => Tick(Time.deltaTime);
        // Manual ticking is for deterministic checks; don't also run Update that frame.
        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled || !hasAuthority || definition == null || deltaTime <= 0
                || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || State == EnemyState.Dead) return;
            if (stats.IsDead) { Die(); return; }
            stateTime += deltaTime;
            navigationTimer -= deltaTime;
            if (State != EnemyState.Return && AggroTarget != null && (!AggroTarget.IsAlive
                || Vector3.Distance(AggroTarget.transform.position, SpawnPosition) > definition.LeashDistance
                || Vector3.Distance(transform.position, SpawnPosition) > definition.LeashDistance)) BeginReturn();
            if (State == EnemyState.Return)
            {
                attack.Tick(deltaTime);
                if (Vector3.Distance(transform.position, SpawnPosition) < 0.6f) { navigation.Stop(); SetState(EnemyState.Idle); return; }
                if (navigationTimer <= 0) { navigation.MoveTo(SpawnPosition, definition.ChaseSpeed, 0.2f); navigationTimer = 0.25f; }
                return;
            }
            // Unity destroyed references compare null but must still release combat state.
            if (AggroTarget == null && (State == EnemyState.Alert || State == EnemyState.Chase || State == EnemyState.Attack)) { BeginReturn(); return; }
            if (AggroTarget != null)
            {
                bool visible = perception.CanSee(AggroTarget);
                unseenTime = visible ? 0 : unseenTime + deltaTime;
                if (unseenTime >= definition.LostTargetTimeout) { BeginReturn(); return; }
                attack.Tick(deltaTime);
                if (State == EnemyState.Alert && stateTime < definition.AlertDuration) return;
                if (attack.IsBusy) return;
                if (visible && Vector3.Distance(transform.position, AggroTarget.transform.position) <= definition.AttackRange)
                {
                    navigation.Stop();
                    navigation.Face(AggroTarget.transform.position, deltaTime);
                    SetState(EnemyState.Attack);
                    attack.TryStart(AggroTarget);
                }
                else
                {
                    SetState(EnemyState.Chase);
                    if (navigationTimer <= 0)
                    {
                        if (!navigation.MoveTo(AggroTarget.transform.position, definition.ChaseSpeed, definition.AttackRange * 0.8f)) { BeginReturn(); return; }
                        navigationTimer = 0.25f;
                    }
                }
                return;
            }
            attack.Tick(deltaTime);
            if (definition.AggroOnSight)
            {
                var target = perception.FindTarget(definition.DetectionRadius, SpawnPosition, definition.LeashDistance);
                if (target != null) { Acquire(target); return; }
            }
            if (State == EnemyState.Idle && stateTime >= definition.IdleDuration && definition.PatrolRadius > 0)
            {
                float angle = patrolIndex++ * 137.5f * Mathf.Deg2Rad;
                var point = SpawnPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * definition.PatrolRadius;
                if (navigation.MoveTo(point, definition.PatrolSpeed, 0.2f)) SetState(EnemyState.Patrol);
                else stateTime = 0;
            }
            else if (State == EnemyState.Patrol && (navigation.HasArrived || stateTime > 10)) { navigation.Stop(); SetState(EnemyState.Idle); }
        }
        private void OnDamaged(float amount, GameObject source)
        {
            if (!isActiveAndEnabled || !hasAuthority || definition == null || stats.IsDead
                || State == EnemyState.Return || source == null) return;
            var target = source.GetComponentInParent<EnemyThreatTarget>();
            if (target != null && target.IsAlive && Vector3.Distance(target.transform.position, SpawnPosition) <= definition.LeashDistance) Acquire(target);
        }
        private void Acquire(EnemyThreatTarget target)
        {
            if (AggroTarget == target) return;
            attack.Cancel(); navigation.Stop();
            AggroTarget = target; unseenTime = 0;
            SetState(EnemyState.Alert);
        }
        private void BeginReturn()
        {
            AggroTarget = null; attack.Cancel(); navigation.Stop(); navigationTimer = 0;
            SetState(EnemyState.Return);
        }
        private void Die()
        {
            if (State == EnemyState.Dead) return;
            AggroTarget = null; attack.Cancel(); navigation.Die();
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            SetState(EnemyState.Dead);
        }
        private void SetState(EnemyState value)
        {
            if (State == value) return;
            State = value; stateTime = 0;
            StateChanged?.Invoke(value);
        }
    }
}
