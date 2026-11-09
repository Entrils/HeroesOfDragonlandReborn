using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private CharacterStatsConfig stats;
        [SerializeField] private bool aggroOnSight = true;
        [SerializeField, Min(0.1f)] private float detectionRadius = 7;
        [SerializeField, Min(0.1f)] private float leashDistance = 12;
        [SerializeField, Min(0)] private float patrolRadius = 2;
        [SerializeField, Min(0.1f)] private float idleDuration = 2;
        [SerializeField, Min(0)] private float alertDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float lostTargetTimeout = 3;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 3.5f;
        [SerializeField, Min(0.2f)] private float attackRange = 1.7f;
        [SerializeField, Min(0)] private float damage = 12;
        [SerializeField, Min(0.05f)] private float attackWindup = 0.4f;
        [SerializeField, Min(0.05f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0)] private float staggerPower = 20f;
        public float StaggerPower => Mathf.Max(0, staggerPower);
        public CharacterStatsConfig Stats => stats;
        public bool AggroOnSight => aggroOnSight;
        public float DetectionRadius => Mathf.Max(0.1f, detectionRadius);
        public float LeashDistance => Mathf.Max(1, leashDistance);
        public float PatrolRadius => Mathf.Clamp(patrolRadius, 0, LeashDistance * 0.5f);
        public float IdleDuration => Mathf.Max(0.1f, idleDuration);
        public float AlertDuration => Mathf.Max(0, alertDuration);
        public float LostTargetTimeout => Mathf.Max(0.1f, lostTargetTimeout);
        public float PatrolSpeed => Mathf.Max(0.1f, patrolSpeed);
        public float ChaseSpeed => Mathf.Max(0.1f, chaseSpeed);
        public float AttackRange => Mathf.Max(0.2f, attackRange);
        public float Damage => Mathf.Max(0, damage);
        public float AttackWindup => Mathf.Max(0.05f, attackWindup);
        // Minimum interval between starts, never shorter than the windup.
        public float AttackCooldown => Mathf.Max(AttackWindup + 0.05f, attackCooldown);
    }
}
