using System.Collections.Generic;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Combat;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    public enum BossPhase { Dormant, PhaseOne, Transition, PhaseTwo, Defeated }

    // A shared-brain extension, not a second AI controller. EnemyBrain owns its clock.
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain))]
    public sealed class BossEncounter : MonoBehaviour, IInvulnerabilityProvider
    {
        private static readonly HashSet<BossEncounter> active = new();
        public static IEnumerable<BossEncounter> Active => active;
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
        private EnemyBrain brain;
        private CharacterStats stats;
        private EnemyMeleeAttack attack;
        private EnemyPerception perception;
        private EnemyThreatTarget challenger;
        private BossPhase phase;
        private float transitionRemaining, switchRemaining;
        public bool IsInvulnerable => isActiveAndEnabled && brain != null && brain.State == EnemyState.Return;
        public BossEncounterDefinition Definition => brain.Definition.Boss;
        public BossPhase Phase => stats != null && stats.IsDead ? BossPhase.Defeated : phase;
        public float TransitionRemaining => transitionRemaining;
        public float SpeedMultiplier => Phase == BossPhase.PhaseTwo ? Definition.EnragedSpeedMultiplier : 1;
        public EnemyAttackSet AttackSet => Phase == BossPhase.PhaseTwo ? Definition.PhaseTwoAttacks : brain.Definition.AttackSet;
        private void Awake()
        {
            brain = GetComponent<EnemyBrain>(); stats = GetComponent<CharacterStats>();
            attack = GetComponent<EnemyMeleeAttack>(); perception = GetComponent<EnemyPerception>();
        }
        internal void RegisterAttacker(EnemyThreatTarget target)
        {
            if (!brain.HasAuthority || Definition == null || !perception.CanTarget(target)) return;
            challenger = target;
        }
        internal bool Tick(float dt)
        {
            if (!brain.HasAuthority || Definition == null || stats.IsDead || brain.State == EnemyState.Return) return false;
            if (brain.AggroTarget == null) return false;
            if (phase == BossPhase.Dormant) { phase = BossPhase.PhaseOne; switchRemaining = Definition.TargetSwitchInterval; }
            // Complete committed attacks before changing their timing/shape. No surprise impacts.
            if (phase == BossPhase.PhaseOne && stats.CurrentHealth <= stats.MaxHealth * Definition.PhaseTwoHealth && !attack.IsBusy)
            {
                phase = BossPhase.Transition; transitionRemaining = Definition.TransitionDuration;
                GetComponent<EnemyNavigation>().Stop(); return true;
            }
            if (phase == BossPhase.Transition)
            {
                transitionRemaining = Mathf.Max(0, transitionRemaining - dt);
                if (transitionRemaining > 0) return true;
                phase = BossPhase.PhaseTwo; attack.ResetPattern();
            }
            switchRemaining -= dt;
            if (switchRemaining <= 0 && !attack.IsBusy)
            {
                switchRemaining = Definition.TargetSwitchInterval;
                EnemyThreatTarget next = IsCandidate(challenger) ? challenger : null;
                if (next == null)
                {
                    float nearest = float.PositiveInfinity;
                    foreach (var collider in Physics.OverlapSphere(transform.position, brain.Definition.DetectionRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        var candidate = collider.GetComponentInParent<EnemyThreatTarget>();
                        if (!IsCandidate(candidate)) continue;
                        float distance = (candidate.NavigationPosition - transform.position).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; next = candidate; }
                    }
                }
                if (next != null) brain.TrySwitchBossTarget(next);
                challenger = null;
            }
            return false;
        }
        internal bool TryReplaceLostTarget()
        {
            if (!brain.HasAuthority || Definition == null) return false;
            foreach (var collider in Physics.OverlapSphere(transform.position, brain.Definition.DetectionRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var candidate = collider.GetComponentInParent<EnemyThreatTarget>();
                if (IsCandidate(candidate) && brain.TrySwitchBossTarget(candidate)) return true;
            }
            return false;
        }
        private bool IsCandidate(EnemyThreatTarget target) => target != null && target != brain.AggroTarget
            && Vector3.Distance(target.NavigationPosition, brain.SpawnPosition) <= brain.Definition.LeashDistance && perception.CanSee(target);
        internal void ResetForEvade()
        {
            if (!brain.HasAuthority || stats.IsDead) return;
            phase = BossPhase.Dormant; transitionRemaining = switchRemaining = 0; challenger = null;
            attack.Cancel(); attack.ResetPattern(); stats.ResetToFull();
        }
    }
}
