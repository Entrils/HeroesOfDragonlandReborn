using System;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(CombatDefense))]
    public sealed class ParryDefense : MonoBehaviour
    {
        [SerializeField] private ParryDefinition definition;
        private CharacterStats stats;
        private CombatDefense defense;
        private float elapsed, cooldown;
        private bool consumed;
        private Vector3 facing;
        public ParryDefinition Definition => definition;
        public bool IsBusy { get; private set; }
        public bool IsActive => isActiveAndEnabled && IsBusy && !consumed && definition != null
            && elapsed >= definition.Startup && elapsed < definition.Startup + definition.Window;
        public float Elapsed => elapsed;
        public float CooldownRemaining => cooldown;
        public int AttemptSequence { get; private set; }
        public int SuccessSequence { get; private set; }
        public Vector3 Facing => facing;
        public event Action AttemptStarted;
        public event Action Successful;
        private void Awake() { stats = GetComponent<CharacterStats>(); defense = GetComponent<CombatDefense>(); }
        public bool TryStart(Vector3 direction)
        {
            var networkDodge = GetComponent<HeroesOfDragonland.Multiplayer.NetworkPlayerDodge>();
            if (!isActiveAndEnabled || !defense.isActiveAndEnabled || !stats.CanMutate || stats.IsDead || definition == null || !definition.IsValid
                || IsBusy || cooldown > 0 || defense.IsStaggered || GetComponent<MeleeAttack>() is { IsBusy: true }
                || GetComponent<HeroesOfDragonland.Player.PlayerDodge>() is { IsBusy: true }
                || (networkDodge != null && networkDodge.Busy)) return false;
            direction.y = 0;
            if (!Finite(direction.x) || !Finite(direction.z) || direction.sqrMagnitude < .001f || stats.CurrentStamina < definition.StaminaCost) return false;
            // A parry may start from guard, but paying for it must not accidentally exhaust an active block.
            defense.SetBlockIntent(false, false);
            if (!stats.TrySpendStamina(definition.StaminaCost)) return false;
            facing = direction.normalized; elapsed = 0; cooldown = definition.Cooldown;
            consumed = false; IsBusy = true; AttemptSequence++; AttemptStarted?.Invoke(); return true;
        }
        internal void Tick(float deltaTime)
        {
            if (!stats.CanMutate) return;
            cooldown = Mathf.Max(0, cooldown - deltaTime);
            if (!IsBusy) return;
            if (stats.IsDead || defense.IsStaggered) { Cancel(); return; }
            elapsed += deltaTime;
            if (definition == null || elapsed >= definition.Duration) IsBusy = false;
        }
        internal bool TryResolve(IMeleeImpactSource attack)
        {
            if (!stats.CanMutate || stats.IsDead || !IsActive || attack == null || !attack.IsResolvingMeleeHit) return false;
            Vector3 toward = attack.ImpactOrigin - transform.position; toward.y = 0;
            if (toward.sqrMagnitude < .0001f || Vector3.Angle(facing, toward) > definition.Arc * .5f) return false;
            consumed = true; SuccessSequence++;
            attack.InterruptByParry(definition.AttackerStagger);
            Successful?.Invoke(); return true;
        }
        internal void Cancel() { IsBusy = false; consumed = true; }
        internal void ResetForSpawn() { Cancel(); elapsed = cooldown = 0; }
        private void OnDisable() => Cancel();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
