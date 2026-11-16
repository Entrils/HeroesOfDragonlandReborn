using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [DefaultExecutionOrder(230), DisallowMultipleComponent]
    public sealed class ActorAudioPresentation : MonoBehaviour
    {
        private NetworkObject actor;
        private NetworkCharacterStats stats;
        private NetworkTestPlayer player;
        private NetworkPlayerCombat combat;
        private NetworkPlayerDodge dodge;
        private NetworkEnemy enemy;
        private NetworkKingObjective king;
        private CharacterVisual visual;
        private NetworkImpactVfx impacts;
        private readonly VfxSequence attacks = new(), dodges = new();
        private bool seeded, dead, airborne;
        private float health;
        private EnemyState enemyState;
        private int round = -1;
        private GameAudioService Service => actor != null && actor.IsValid ? AudioAudience.Service(actor.Runner) : null;
        private bool Ready => actor != null && actor.IsValid && stats != null && stats.MaxHealth > 0 && (player == null || !player.IsSuspended);
        private void Awake()
        {
            actor = GetComponent<NetworkObject>(); stats = GetComponent<NetworkCharacterStats>(); player = GetComponent<NetworkTestPlayer>();
            combat = GetComponent<NetworkPlayerCombat>(); dodge = GetComponent<NetworkPlayerDodge>(); enemy = GetComponent<NetworkEnemy>(); king = GetComponent<NetworkKingObjective>();
            visual = GetComponentInChildren<CharacterVisual>(true); impacts = GetComponent<NetworkImpactVfx>();
        }
        private void OnEnable()
        {
            ResetBaseline();
            if (visual != null) visual.PresentationCue += Animation;
            if (impacts != null) impacts.Presented += Impact;
        }
        private void OnDisable()
        {
            if (visual != null) visual.PresentationCue -= Animation;
            if (impacts != null) impacts.Presented -= Impact;
            ResetBaseline();
        }
        private void ResetBaseline() { seeded = false; round = -1; attacks.Reset(); dodges.Reset(); }
        private void Play(AudioCue cue) => Service?.Play(cue, transform.position + Vector3.up * .6f, this);
        private void Animation(CharacterAnimationCue cue)
        {
            if (!Ready || !seeded || stats.Dead) return;
            if (cue == CharacterAnimationCue.Footstep) Play(AudioCue.Footstep);
            // Dodge/attack use accepted replicated sequences, not predicted animation requests.
        }
        private void Impact(VfxKind kind, Vector3 point)
        {
            if (!Ready || stats.Dead) return;
            if (kind == VfxKind.BlockedHit) Service?.Play(AudioCue.Block, point, this);
            else if (kind == VfxKind.Parry) Service?.Play(AudioCue.Parry, point, this);
        }
        private void LateUpdate()
        {
            if (!Ready) { ResetBaseline(); return; }
            var match = NetworkMatchPacing.MatchFor(actor.Runner);
            if (match != null && match.Object != null && match.Object.IsValid && match.RoundNumber != round)
            { ResetBaseline(); round = match.RoundNumber; }
            bool inAir = visual != null && visual.State.Airborne;
            if (seeded)
            {
                if (!dead && stats.Dead) Play(king != null ? AudioCue.KingDeath : enemy != null ? AudioCue.EnemyDeath : AudioCue.Death);
                else if (!dead && !stats.Dead && stats.Health < health - .001f)
                {
                    Play(enemy != null ? AudioCue.EnemyDamage : AudioCue.Hit);
                    if (king != null && king.Team == AudioAudience.LocalTeam(actor.Runner) && king.Team != TeamId.Unassigned)
                        Service?.Play(AudioCue.KingUnderAttack, transform.position, this);
                }
                if (!dead && !stats.Dead && !airborne && inAir && visual.State.VerticalSpeed > .1f) Play(AudioCue.Jump);
                if (enemy != null && !stats.Dead && (enemyState == EnemyState.Idle || enemyState == EnemyState.Patrol || enemyState == EnemyState.Return)
                    && (enemy.State == EnemyState.Alert || enemy.State == EnemyState.Chase || enemy.State == EnemyState.Attack)) Play(AudioCue.EnemyAggro);
            }
            health = stats.Health; dead = stats.Dead; airborne = inAir; enemyState = enemy != null ? enemy.State : EnemyState.Idle; seeded = true;
            if (dead) { attacks.Reset(); dodges.Reset(); return; }
            if (combat != null && AudioAudience.Action(attacks, actor, combat.AttackSequence, combat.AttackStartTick,
                combat.AttackBusy, combat.AttackSwingDuration)) Play(AudioCue.Attack);
            else if (combat == null && enemy != null && enemy.Definition != null
                && AudioAudience.Action(attacks, actor, enemy.AttackSequence, enemy.AttackStartTick, enemy.AttackBusy, enemy.AttackWindup)) Play(AudioCue.EnemyAttack);
            if (dodge != null && AudioAudience.Action(dodges, actor, dodge.DodgeSequence, dodge.StartTick, dodge.Busy, dodge.ActiveDuration)) Play(AudioCue.Dodge);
        }
    }
}
