using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkCharacterStats), typeof(MeleeAttack), typeof(DamageReceiver))]
    [RequireComponent(typeof(TeamIdentity))]
    public sealed class NetworkPlayerCombat : NetworkBehaviour, IAfterTick, IDamagePolicy
    {
        [Networked] public int AttackSequence { get; private set; }
        [Networked] public int AttackStartTick { get; private set; }
        [Networked] public NetworkBool AttackBusy { get; private set; }
        [Networked] public Vector3 AttackFacing { get; private set; }
        private MeleeAttack melee;
        private CharacterStats stats;
        private NetworkTestPlayer player;
        public bool ResolvingAttack { get; private set; }
        public MeleeAttackDefinition Definition => melee.Definition;
        public bool IsBusy => melee.IsBusy;
        public float MovementMultiplier => melee.MovementMultiplier;
        private void Awake()
        {
            melee = GetComponent<MeleeAttack>();
            stats = GetComponent<CharacterStats>();
            player = GetComponent<NetworkTestPlayer>();
        }
        public override void Spawned()
        {
            GetComponent<TeamIdentity>().TryConfigure(player.Team, CombatantKind.Player, false);
        }
        internal bool IsAdmitted()
            => Object != null && Object.IsValid && Runner.IsServer && HasStateAuthority
               && Runner.TryGetPlayerObject(Object.InputAuthority, out var avatar) && avatar == Object;

        // Called by the server motor before movement; a held input may repeat only
        // after the full recovery and another successful server stamina payment.
        internal void TickIntent(NetworkPlayerInput input, Vector3 facing, bool dodgeBusy)
        {
            if (!IsAdmitted() || dodgeBusy || !input.Attack || !melee.TryAttack(facing)) return;
            AttackSequence++;
            AttackStartTick = Runner.Tick.Raw;
            AttackFacing = facing;
            AttackBusy = true;
        }

        public void AfterTick()
        {
            if (!IsAdmitted()) return;
            // Every player's movement tick has completed before server hit geometry is sampled.
            Physics.SyncTransforms();
            ResolvingAttack = true;
            try { melee.TickAttack(Runner.DeltaTime); }
            finally { ResolvingAttack = false; }
            AttackBusy = melee.IsBusy && !stats.IsDead;
        }

        public bool AllowsDamage(GameObject source)
        {
            if (!IsAdmitted() || stats.IsDead || source == null || source == gameObject) return false;
            var attacker = source.GetComponent<NetworkPlayerCombat>();
            return attacker != null && attacker.Runner == Runner && attacker.IsAdmitted()
                && attacker.ResolvingAttack && !attacker.stats.IsDead
                && player.Team != TeamId.Unassigned && attacker.player.Team != TeamId.Unassigned
                && player.Team != attacker.player.Team;
        }
    }
}
