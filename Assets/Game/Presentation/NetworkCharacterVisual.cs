using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    // Runs on every replica, using rendered transforms and existing accepted action/health snapshots.
    [DefaultExecutionOrder(150), DisallowMultipleComponent, RequireComponent(typeof(NetworkTestPlayer))]
    public sealed class NetworkCharacterVisual : MonoBehaviour
    {
        [SerializeField] private CharacterVisual visual;
        private NetworkTestPlayer player;
        private CharacterAppearanceVisual appearance;
        private NetworkPlayerCombat combat;
        private NetworkPlayerDodge dodge;
        private NetworkCharacterStats stats;
        private NetworkCharacterAnimationState animationState;
        private Vector3 previousPosition;
        private bool sampled;
        private TeamId shownTeam = TeamId.Unassigned;
        public CharacterVisual Visual => visual;
        private void Awake()
        {
            player = GetComponent<NetworkTestPlayer>(); combat = GetComponent<NetworkPlayerCombat>();
            dodge = GetComponent<NetworkPlayerDodge>(); stats = GetComponent<NetworkCharacterStats>();
            animationState=GetComponent<NetworkCharacterAnimationState>();
            appearance = visual != null ? visual.GetComponent<CharacterAppearanceVisual>() : null;
        }
        private void OnEnable() => sampled = false;
        private void LateUpdate()
        {
            if (visual == null || player.Object == null || !player.Object.IsValid
                || !player.Runner.IsRunning) { sampled = false; return; }
            double tickSeconds;
            try { tickSeconds = player.Runner.DeltaTime; }
            catch (System.InvalidOperationException error) when (error.Message.StartsWith("RuntimeConfig can only be read", System.StringComparison.Ordinal))
            { sampled = false; return; }
            var delta = sampled ? transform.position - previousPosition : Vector3.zero;
            previousPosition = transform.position; sampled = true; delta.y = 0;
            var velocity = delta.magnitude < 3 && Time.deltaTime > .0001f ? delta / Time.deltaTime : Vector3.zero;
            var local = transform.InverseTransformDirection(velocity);
            bool playing = NetworkMatch.AllowsGameplay(player.Runner);
            double time = player.HasStateAuthority ? player.Runner.LocalRenderTime : player.Runner.RemoteRenderTime;
            float attackAge = (float)(time - combat.AttackStartTick * tickSeconds);
            float dodgeAge = (float)(time - dodge.StartTick * tickSeconds);
            bool attacking = playing && combat.AttackBusy && combat.AttackSequence > 0 && attackAge >= 0 && attackAge < combat.AttackSwingDuration;
            bool dodging = playing && dodge.Busy && dodge.DodgeSequence > 0 && dodgeAge >= 0 && dodgeAge < dodge.ActiveDuration;
            var gear = GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>();
            var weapon = gear != null ? gear.EquippedWeapon : null;
            int moveIndex = 0;
            if (attacking)
            {
                weapon = null;
                var inventory = GetComponent<HeroesOfDragonland.InventorySystem.Inventory>();
                if (inventory != null && inventory.TryResolveItem(combat.AttackWeaponId.ToString(), out var item)) weapon = item.Weapon;
                moveIndex = combat.AttackMoveIndex;
            }
            visual.SetWeaponPresentation(weapon, attacking && combat.HeavyAttack, moveIndex);
            float hitAge=animationState!=null?(float)(time-animationState.HitTick*tickSeconds):100;
            var state=new CharacterVisualState {
                Speed = playing ? Mathf.Min(velocity.magnitude, 8) : 0,
                LocalVelocity = playing ? new Vector2(local.x, local.z) : Vector2.zero,
                Dead = stats.Dead, Attacking = attacking, Dodging = dodging, Blocking = playing && (combat.Blocking || combat.ParryBusy),
                Staggered = playing && combat.Staggered, LockedOn = animationState!=null&&animationState.LockedOn,
                Airborne=playing&&animationState!=null&&!animationState.Grounded,
                VerticalSpeed=animationState!=null?animationState.VerticalSpeed:0,
                HeavyAttack=combat.HeavyAttack,
                HitReacting=playing&&animationState!=null&&animationState.HitSequence>0&&hitAge>=0&&hitAge<.25f
            };
            switch(CharacterAnimationModel.Action(state))
            {
                case CharacterAction.LightAttack:case CharacterAction.HeavyAttack:
                    state.ActionAge=attackAge;state.ActionDuration=combat.AttackSwingDuration;state.ActionSequence=combat.AttackSequence;break;
                case CharacterAction.Dodge:
                    state.ActionAge=dodgeAge;state.ActionDuration=dodge.ActiveDuration;state.ActionSequence=dodge.DodgeSequence;break;
                case CharacterAction.HitReaction:
                    state.ActionAge=hitAge;state.ActionDuration=.25f;state.ActionSequence=animationState.HitSequence;break;
                case CharacterAction.Stagger:
                    state.ActionAge=animationState!=null?(float)(time-animationState.StaggerTick*tickSeconds):0;
                    state.ActionSequence=animationState!=null?animationState.StaggerTick:0;break;
                case CharacterAction.Death:
                    state.ActionAge=animationState!=null?(float)(time-animationState.DeathTick*tickSeconds):0;
                    state.ActionSequence=animationState!=null?animationState.DeathTick:0;break;
            }
            visual.Apply(state, Time.deltaTime);
            appearance?.Apply(player.Appearance);
            if (!state.Dead && player.CosmeticState != 0
                && NetworkMatchPacing.MatchFor(player.Runner) is { State: MatchPhase.Finished } match && match.Winner == player.Team)
                CosmeticPresentation.Pose(visual, player.Cosmetics.Get(CosmeticCategory.VictoryPose)?.Gesture ?? VictoryGesture.None);
            if (shownTeam != player.Team)
            {
                shownTeam = player.Team;
                visual.SetTint(shownTeam == TeamId.TeamA ? new Color(.15f,.45f,1) : new Color(1,.25f,.2f));
            }
        }
    }
}
