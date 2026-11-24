using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(MeleeAttack), typeof(CharacterStats))]
    public sealed class LocalCharacterVisual : MonoBehaviour
    {
        [SerializeField] private CharacterVisual visual;
        private CharacterController movement;
        private MeleeAttack attack;
        private CharacterStats stats;
        private CombatDefense defense;
        private int attackSequence;

        private void Awake()
        {
            movement = GetComponent<CharacterController>();
            attack = GetComponent<MeleeAttack>();
            stats = GetComponent<CharacterStats>();
            defense = GetComponent<CombatDefense>();
        }

        private void OnEnable() => attack.AttackStarted += StartAttack;
        private void OnDisable() => attack.AttackStarted -= StartAttack;
        private void StartAttack() => attackSequence++;

        private void LateUpdate()
        {
            var local = transform.InverseTransformDirection(movement.velocity);
            var planar = new Vector2(local.x, local.z);
            visual.SetWeaponPresentation(attack.CurrentWeapon, attack.IsHeavy, attack.MoveIndex);
            visual.Apply(new CharacterVisualState
            {
                Speed = planar.magnitude,
                LocalVelocity = planar,
                Dead = stats.IsDead,
                Attacking = attack.IsSwinging,
                Blocking = defense != null && defense.IsBlocking,
                Staggered = defense != null && defense.IsStaggered,
                Airborne = !movement.isGrounded,
                VerticalSpeed = local.y,
                HeavyAttack = attack.IsHeavy,
                ActionAge = attack.AttackElapsed,
                ActionDuration = attack.SwingDuration,
                ActionSequence = attackSequence
            }, Time.deltaTime);
        }
    }
}
