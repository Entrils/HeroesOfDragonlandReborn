using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Uses the offline dodge's data, but never installs the offline movement/input stack.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkCharacterStats))]
    public sealed class NetworkPlayerDodge : NetworkBehaviour, IInvulnerabilityProvider
    {
        [SerializeField] private DodgeDefinition definition;
        [Networked] public int DodgeSequence { get; private set; }
        [Networked] public int StartTick { get; private set; }
        [Networked] public NetworkBool Busy { get; private set; }
        [Networked] public Vector3 Direction { get; private set; }
        [Networked] public Vector3 Facing { get; private set; }
        private CharacterStats stats;
        private NetworkPlayerCombat combat;
        public DodgeDefinition Definition => definition;
        private float Elapsed => (Runner.Tick.Raw - StartTick) * Runner.DeltaTime;
        public bool IsInvulnerable => isActiveAndEnabled && stats != null && stats.CanMutate
            && !stats.IsDead && Busy && definition != null
            && Elapsed >= definition.InvulnerabilityStartTime && Elapsed < definition.InvulnerabilityEndTime;

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
            if (Busy && Elapsed >= definition.Duration + definition.Recovery) Busy = false;
            if (Busy || !input.Dodge || combat.IsBusy || definition == null) return;
            if (!stats.TrySpendStamina(definition.StaminaCost)) return;
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
            float time = Mathf.Clamp(definition.Duration - Elapsed, 0f, Runner.DeltaTime);
            return Direction * (definition.Distance / definition.Duration * time);
        }
    }
}
