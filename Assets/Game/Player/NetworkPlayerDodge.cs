using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using UnityEngine;
using HeroesOfDragonland.EquipmentSystem;

namespace HeroesOfDragonland.Multiplayer
{
    // Uses the offline dodge's data, but never installs the offline movement/input stack.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkCharacterStats))]
    public sealed class NetworkPlayerDodge : NetworkBehaviour, IInvulnerabilityProvider
    {
        [SerializeField] private DodgeDefinition definition;
        [Networked] private float DistanceMultiplier { get; set; }
        [Networked] private float DurationMultiplier { get; set; }
        [Networked] private float RecoveryMultiplier { get; set; }
        private float RollDuration => definition.Duration * Mathf.Max(0.01f, DurationMultiplier);
        private float RollRecovery => definition.Recovery * Mathf.Max(0.01f, RecoveryMultiplier);
        private float RollDistance => definition.Distance * Mathf.Max(0.01f, DistanceMultiplier);
        [Networked] public int DodgeSequence { get; private set; }
        [Networked] public int StartTick { get; private set; }
        [Networked] public NetworkBool Busy { get; private set; }
        [Networked] public Vector3 Direction { get; private set; }
        [Networked] public Vector3 Facing { get; private set; }
        private CharacterStats stats;
        private NetworkPlayerCombat combat;
        public DodgeDefinition Definition => definition;
        public float ActiveDuration => RollDuration;
        public float InvulnerabilityStartTime => definition.InvulnerabilityStartTime / definition.Duration * RollDuration;
        public float InvulnerabilityEndTime => definition.InvulnerabilityEndTime / definition.Duration * RollDuration;
        private float Elapsed => (Runner.Tick.Raw - StartTick) * Runner.DeltaTime;
        public bool IsInvulnerable => isActiveAndEnabled && stats != null && stats.CanMutate
            && !stats.IsDead && Busy && definition != null
            && Elapsed >= InvulnerabilityStartTime && Elapsed < InvulnerabilityEndTime;

        private void Awake()
        {
            stats = GetComponent<CharacterStats>();
            combat = GetComponent<NetworkPlayerCombat>();
        }

        internal void TickIntent(NetworkPlayerInput input, Vector3 move, Vector3 facing)
        {
            if (combat == null || !combat.IsAdmitted()) return;
            if (!NetworkMatch.AllowsGameplay(Runner)) { Busy = false; return; }
            if (!isActiveAndEnabled || stats.IsDead) { Busy = false; return; }
            if (Busy && Elapsed >= RollDuration + RollRecovery) Busy = false;
            if (Busy || !input.Dodge || (combat.IsBusy && !combat.CanDodgeCancel) || combat.ActionsBlocked || definition == null) return;
            var rollModifiers = GetComponent<Equipment>()?.DodgeModifiers ?? WeightDodgeModifiers.Light;
            if (!stats.TrySpendStamina(definition.StaminaCost * Mathf.Max(0.01f, rollModifiers.stamina))) return;
            if (combat.IsBusy && !combat.CancelAttackForDodge()) return;
            DistanceMultiplier = rollModifiers.distance;
            DurationMultiplier = rollModifiers.duration;
            RecoveryMultiplier = rollModifiers.recovery;
            Direction = move.sqrMagnitude > 0.0001f ? move.normalized
                : (input.Facing.sqrMagnitude > 0.0001f ? -facing : facing);
            Facing = facing;
            StartTick = Runner.Tick.Raw;
            DodgeSequence++;
            Busy = true;
        }

        internal void CancelForRespawn()
        {
            if (combat != null && combat.IsAdmitted()) Busy = false;
        }

        internal Vector3 StepDisplacement()
        {
            if (!isActiveAndEnabled || !stats.CanMutate || stats.IsDead || !Busy || definition == null) return Vector3.zero;
            float time = Mathf.Clamp(RollDuration - Elapsed, 0f, Runner.DeltaTime);
            return Direction * (RollDistance / RollDuration * time);
        }
    }
}
