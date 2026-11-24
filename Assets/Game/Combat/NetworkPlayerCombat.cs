using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Targeting;
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
        [Networked] public NetworkString<_32> BowItemId { get; private set; }
        [Networked] public NetworkBool BowDrawing { get; private set; }
        [Networked] public float BowDrawProgress { get; private set; }
        [Networked] public float BowReleaseProgress { get; private set; }
        [Networked] public NetworkBool ArrowInFlight { get; private set; }
        [Networked] public Vector3 ArrowPosition { get; private set; }
        [Networked] public Vector3 ArrowDirection { get; private set; }
        [Networked] private TickTimer TrapRoot { get; set; }
        [Networked] private TickTimer TrapSlow { get; set; }
        [Networked] private float TrapSlowMultiplier { get; set; }
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
        private Equipment gear;
        private BowShot bow;
        private readonly AttackPressSequence presses = new AttackPressSequence();
        private readonly AttackPressSequence parryPresses = new AttackPressSequence();
        public bool CanDodgeCancel => melee != null && melee.CanDodgeCancel;
        public bool ResolvingAttack { get; private set; }
        public MeleeAttackDefinition Definition => melee.Definition;
        public bool IsBusy => melee.IsBusy || (bow != null && (bow.Drawing || bow.ReleaseRemaining > 0));
        public Vector3 ActionFacing => bow.Drawing && bow.Facing.sqrMagnitude > .01f ? bow.Facing
            : melee.IsBusy ? AttackFacing : transform.forward;
        public float MovementMultiplier => (bow.Drawing ? .55f : melee.MovementMultiplier)
            * (Defense != null ? Defense.MovementMultiplier : 1f);
        public float DeployableMovementMultiplier => !TrapRoot.ExpiredOrNotRunning(Runner) ? 0
            : !TrapSlow.ExpiredOrNotRunning(Runner) ? TrapSlowMultiplier : 1;

        internal void ApplyDeployableMovement(float multiplier, float seconds)
        {
            if (!IsAdmitted() || stats.IsDead || !NetworkMatch.AllowsGameplay(Runner)) return;
            if (multiplier <= 0)
                TrapRoot = TickTimer.CreateFromSeconds(Runner, Mathf.Max(seconds, TrapRoot.RemainingTime(Runner) ?? 0));
            else if (multiplier < 1 && (TrapSlow.ExpiredOrNotRunning(Runner) || multiplier <= TrapSlowMultiplier))
            {
                float duration = multiplier == TrapSlowMultiplier ? Mathf.Max(seconds, TrapSlow.RemainingTime(Runner) ?? 0) : seconds;
                TrapSlow = TickTimer.CreateFromSeconds(Runner, duration);
                TrapSlowMultiplier = multiplier;
            }
        }
        private void Awake()
        {
            melee = GetComponent<MeleeAttack>();
            stats = GetComponent<CharacterStats>();
            player = GetComponent<NetworkTestPlayer>();
            gear = GetComponent<Equipment>();
            bow = new BowShot(gameObject);
            bow.Impact += BroadcastArrowImpact;
            if (GetComponent<BowPresentation>() == null) gameObject.AddComponent<BowPresentation>();
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
                bow.CancelDraw();
                melee.DiscardBufferedAttack(); Defense?.SetBlockIntent(false, dodgeBusy || IsBusy);
                PublishDefense(); return;
            }
            bool bowEquipped = gear != null && gear.EquippedWeapon != null && gear.EquippedWeapon.Archetype == WeaponArchetype.Bow;
            if (parryPressed && !dodgeBusy && !bow.Drawing && GetComponent<ParryDefense>() is { } parry && parry.TryStart(facing))
                ParryStartTick = Runner.Tick.Raw;
            Defense?.SetBlockIntent(input.Block && !bowEquipped, dodgeBusy || IsBusy);
            PublishDefense();
            if (dodgeBusy || ActionsBlocked) { bow.CancelDraw(); melee.DiscardBufferedAttack(); return; }
            if (bowEquipped)
            {
                melee.DiscardBufferedAttack();
                Targetable target = null;
                if (input.LockedOn && input.BowTarget != default && Runner.TryFindObject(input.BowTarget, out var candidate))
                    target = candidate.GetComponentInChildren<Targetable>();
                Vector3 aim = input.BowAim;
                if (aim == Vector3.zero) aim = facing;
                bow.UpdateAim(aim, target);
                if (pressed && attack == AttackInputKind.Light && !melee.IsBusy && bow.TryStart(aim, target))
                    BowItemId = gear.GetItemId(EquipmentSlot.MainHand);
                return;
            }
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
            if (!NetworkMatch.AllowsGameplay(Runner)) { melee.Cancel(); ResetBow(); Defense?.ResetState(); PublishDefense(); AttackBusy = false; return; }
            // Every player's movement tick has completed before server hit geometry is sampled.
            Physics.SyncTransforms();
            ResolvingAttack = true;
            try { melee.TickAttack(Runner.DeltaTime); }
            finally { ResolvingAttack = false; }
            bow.Tick(Runner.DeltaTime, ActionsBlocked);
            PublishBow();
            AttackBusy = melee.IsBusy && !stats.IsDead;
            PublishDefense();
        }

        private void PublishBow()
        {
            BowDrawing = bow.Drawing;
            BowDrawProgress = bow.DrawProgress;
            BowReleaseProgress = bow.ReleaseRemaining > 0 ? bow.ReleaseRemaining / bow.ReleaseDuration : 0;
            ArrowInFlight = bow.InFlight;
            if (bow.InFlight) { ArrowPosition = bow.Position; ArrowDirection = bow.Direction; }
        }

        private void ResetBow() { bow.Cancel(); PublishBow(); }

        public bool AllowsArrowDamage(NetworkRunner runner) => runner == Runner && Runner.IsServer && HasStateAuthority
            && IsAdmitted() && NetworkMatch.AllowsGameplay(Runner) && !stats.IsDead && bow.ResolvingHit;

        private void BroadcastArrowImpact(Vector3 point)
        {
            if (Runner.IsForward) RPC_ArrowImpact(point);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ArrowImpact(Vector3 point)
        {
            GetComponent<BowPresentation>()?.PresentImpact(point, Runner, player.Team);
        }

        private void PublishMoveset()
        {
            AttackMoveIndex = melee.MoveIndex;
            AttackWeaponId = GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>()
                ?.GetItemId(HeroesOfDragonland.Items.EquipmentSlot.MainHand) ?? "";
        }

        internal void SuspendConnection()
        {
            if (!Runner.IsServer || !HasStateAuthority) return;
            melee.Cancel(); ResetBow(); Defense?.ResetState(); PublishDefense(); AttackBusy = false;
            ResetConnectionInput();
        }
        internal void ResetConnectionInput() { presses.Reset(); parryPresses.Reset(); }

        internal void CancelForRespawn()
        {
            if (!IsAdmitted()) return;
            TrapRoot = TrapSlow = TickTimer.None;
            melee.Cancel(); ResetBow(); Defense?.ResetState(); PublishDefense(); AttackBusy = false;
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
            if (source.TryGetComponent<NetworkDeployable>(out var trap)) return trap.AllowsTrapDamage(Runner, gameObject);
            var magic = source.GetComponent<HeroesOfDragonland.Abilities.NetworkAbilities>();
            if (magic != null && magic.AllowsProjectileDamage(Runner))
                return TeamIdentity.AreEnemies(TeamIdentity.Find(source), GetComponent<TeamIdentity>());
            var enemy = source.GetComponent<NetworkEnemy>();
            if (enemy != null)
            {
                var guard = enemy.GetComponent<NetworkFortressGuard>();
                return enemy.Runner == Runner && enemy.IsResolvingAttack && (guard == null || guard.IsEnemy(player));
            }
            var attacker = source.GetComponent<NetworkPlayerCombat>();
            if (attacker != null && attacker.AllowsArrowDamage(Runner))
                return TeamIdentity.AreEnemies(TeamIdentity.Find(source), GetComponent<TeamIdentity>());
            return attacker != null && attacker.Runner == Runner && attacker.IsAdmitted()
                && attacker.ResolvingAttack && attacker.melee.IsResolvingMeleeHit && !attacker.stats.IsDead
                && player.Team != TeamId.Unassigned && attacker.player.Team != TeamId.Unassigned
                && player.Team != attacker.player.Team;
        }
    }
}
