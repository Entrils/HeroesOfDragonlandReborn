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
        [Networked] public float AttackSwingDuration { get; private set; }
        [Networked] public Vector3 AttackFacing { get; private set; }
        [Networked] public NetworkBool HeavyAttack { get; private set; }
        [Networked] public int AttackMoveIndex { get; private set; }
        [Networked] public NetworkString<_32> AttackWeaponId { get; private set; }
        [Networked] public NetworkBool Blocking { get; private set; }
        [Networked] public NetworkBool Staggered { get; private set; }
        [Networked] public int GuardBreakSequence { get; private set; }
        [Networked] public NetworkBool Parrying { get; private set; }
        [Networked] public NetworkBool ParryBusy { get; private set; }
        [Networked] public int ParrySequence { get; private set; }
        [Networked] public int ParrySuccessSequence { get; private set; }
        [Networked] public int ParryStartTick { get; private set; }
        [Networked] public float Poise { get; private set; }
        [Networked] public float MaxPoise { get; private set; }
        [Networked] public float PoiseRegenRemaining { get; private set; }
        [Networked] public float StaggerRemaining { get; private set; }
        [Networked] public float StaggerImmunityRemaining { get; private set; }
        [Networked] public int StaggerSequence { get; private set; }
        [Networked] public DefenseInterrupt LastDefenseInterrupt { get; private set; }
        private CombatDefense Defense => GetComponent<CombatDefense>();
        public bool ActionsBlocked => Defense != null && Defense.ActionsBlocked;
        private MeleeAttack melee;
        private CharacterStats stats;
        private NetworkTestPlayer player;
        private readonly AttackPressSequence presses = new AttackPressSequence();
        private readonly AttackPressSequence parryPresses = new AttackPressSequence();
        public bool CanDodgeCancel => melee != null && melee.CanDodgeCancel;
        public bool ResolvingAttack { get; private set; }
        public MeleeAttackDefinition Definition => melee.Definition;
        public bool IsBusy => melee.IsBusy;
        public float MovementMultiplier => melee.MovementMultiplier * (Defense != null ? Defense.MovementMultiplier : 1f);
        private void Awake()
        {
            melee = GetComponent<MeleeAttack>();
            stats = GetComponent<CharacterStats>();
            player = GetComponent<NetworkTestPlayer>();
            melee.AttackStarted += PublishAcceptedAttack;
        }
        private void OnDestroy() { if (melee != null) melee.AttackStarted -= PublishAcceptedAttack; }
        public override void Spawned()
        {
            GetComponent<TeamIdentity>().TryConfigure(player.Team, CombatantKind.Player, false);
        }
        internal bool IsAdmitted()
            => NetworkParticipant.IsAdmitted(Object);

        // Read each press once, including rejected presses; missing/repeated packets cannot replay it.
        internal void TickIntent(NetworkPlayerInput input, Vector3 facing, bool dodgeBusy)
        {
            if (!IsAdmitted()) return;
            bool pressed = presses.TryRead(input.LightAttackPress, input.HeavyAttackPress, out var attack);
            bool parryPressed = parryPresses.TryRead(input.ParryPress, 0, out _);
            if (!NetworkMatch.AllowsGameplay(Runner) || input.SuppressAttacks || stats.IsDead)
            {
                melee.DiscardBufferedAttack(); Defense?.SetBlockIntent(false, dodgeBusy || melee.IsBusy);
                PublishDefense(); return;
            }
            if (parryPressed && !dodgeBusy && GetComponent<ParryDefense>() is { } parry && parry.TryStart(facing))
                ParryStartTick = Runner.Tick.Raw;
            Defense?.SetBlockIntent(input.Block, dodgeBusy || melee.IsBusy);
            PublishDefense();
            if (dodgeBusy || ActionsBlocked) { melee.DiscardBufferedAttack(); return; }
            if (pressed) melee.RequestAttack(attack, facing);
        }

        private void PublishAcceptedAttack()
        {
            if (!IsAdmitted()) return;
            HeavyAttack = melee.IsHeavy;
            PublishMoveset();
            AttackSwingDuration = melee.SwingDuration;
            AttackSequence++;
            AttackStartTick = Runner.Tick.Raw;
            AttackFacing = melee.FacingDirection ?? transform.forward;
            AttackBusy = true;
        }

        internal bool TryStartSkillStrike(float multiplier, float manaCost)
        {
            if (!IsAdmitted() || !NetworkMatch.AllowsGameplay(Runner) || !melee.TrySkillAttack(multiplier, manaCost)) return false;
            return true;
        }

        internal bool CancelAttackForDodge()
        {
            if (!IsAdmitted() || !NetworkMatch.AllowsGameplay(Runner) || !melee.TryCancelForDodge()) return false;
            AttackBusy = false; return true;
        }

        public void AfterTick()
        {
            if (!IsAdmitted()) return;
            if (!NetworkMatch.AllowsGameplay(Runner)) { melee.Cancel(); Defense?.ResetState(); PublishDefense(); AttackBusy = false; return; }
            // Every player's movement tick has completed before server hit geometry is sampled.
            Physics.SyncTransforms();
            ResolvingAttack = true;
            try { melee.TickAttack(Runner.DeltaTime); }
            finally { ResolvingAttack = false; }
            AttackBusy = melee.IsBusy && !stats.IsDead;
            PublishDefense();
        }

        private void PublishMoveset()
        {
            AttackMoveIndex = melee.MoveIndex;
            AttackWeaponId = GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>()
                ?.GetItemId(HeroesOfDragonland.Items.EquipmentSlot.MainHand) ?? "";
        }

        internal void CancelForRespawn()
        {
            if (!IsAdmitted()) return;
            melee.Cancel(); Defense?.ResetState(); PublishDefense(); AttackBusy = false;
        }

        private void PublishDefense()
        {
            Blocking = Defense != null && Defense.IsBlocking && !stats.IsDead;
            Staggered = Defense != null && Defense.IsStaggered && !stats.IsDead;
            GuardBreakSequence = Defense != null ? Defense.GuardBreakSequence : 0;
            var parry = GetComponent<ParryDefense>(); var poise = GetComponent<CharacterPoise>();
            Parrying = parry != null && parry.IsActive && !stats.IsDead;
            ParryBusy = parry != null && parry.IsBusy && !stats.IsDead;
            ParrySequence = parry != null ? parry.AttemptSequence : 0;
            ParrySuccessSequence = parry != null ? parry.SuccessSequence : 0;
            Poise = poise != null ? poise.Current : 0; MaxPoise = poise != null ? poise.Maximum : 0;
            PoiseRegenRemaining = poise != null ? poise.RegenDelayRemaining : 0;
            StaggerRemaining = Defense != null ? Defense.StaggerRemaining : 0;
            StaggerImmunityRemaining = Defense != null ? Defense.ImmunityRemaining : 0;
            StaggerSequence = Defense != null ? Defense.StaggerSequence : 0;
            LastDefenseInterrupt = Defense != null ? Defense.LastInterrupt : DefenseInterrupt.None;
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
                && attacker.ResolvingAttack && attacker.melee.IsResolvingMeleeHit && !attacker.stats.IsDead
                && player.Team != TeamId.Unassigned && attacker.player.Team != TeamId.Unassigned
                && player.Team != attacker.player.Team;
        }
    }
}
