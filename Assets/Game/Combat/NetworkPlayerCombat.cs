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
            if (!IsAdmitted() || !NetworkMatch.AllowsGameplay(Runner) || dodgeBusy || !input.Attack || !melee.TryAttack(facing)) return;
            AttackSequence++;
            AttackStartTick = Runner.Tick.Raw;
            AttackFacing = facing;
            AttackBusy = true;
        }

        internal bool TryStartSkillStrike(float multiplier, float manaCost)
        {
            if (!IsAdmitted() || !NetworkMatch.AllowsGameplay(Runner) || !melee.TrySkillAttack(multiplier, manaCost)) return false;
            AttackSequence++; AttackStartTick = Runner.Tick.Raw;
            AttackFacing = transform.forward; AttackBusy = true; return true;
        }

        public void AfterTick()
        {
            if (!IsAdmitted()) return;
            if (!NetworkMatch.AllowsGameplay(Runner)) { melee.Cancel(); AttackBusy = false; return; }
            // Every player's movement tick has completed before server hit geometry is sampled.
            Physics.SyncTransforms();
            ResolvingAttack = true;
            try { melee.TickAttack(Runner.DeltaTime); }
            finally { ResolvingAttack = false; }
            AttackBusy = melee.IsBusy && !stats.IsDead;
        }

        internal void CancelForRespawn()
        {
            if (!IsAdmitted()) return;
            melee.Cancel(); AttackBusy = false;
        }

        public bool AllowsDamage(GameObject source)
        {
            if (!IsAdmitted() || !NetworkMatch.AllowsGameplay(Runner) || stats.IsDead || source == null || source == gameObject) return false;
            var enemy = source.GetComponent<NetworkEnemy>();
            if (enemy != null)
            {
                var guard = enemy.GetComponent<NetworkFortressGuard>();
                return enemy.Runner == Runner && enemy.IsResolvingAttack && (guard == null || guard.IsEnemy(player));
            }
            var attacker = source.GetComponent<NetworkPlayerCombat>();
            return attacker != null && attacker.Runner == Runner && attacker.IsAdmitted()
                && attacker.ResolvingAttack && !attacker.stats.IsDead
                && player.Team != TeamId.Unassigned && attacker.player.Team != TeamId.Unassigned
                && player.Team != attacker.player.Team;
        }
    }
}
