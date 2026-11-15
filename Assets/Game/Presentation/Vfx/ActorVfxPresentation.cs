using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    [DefaultExecutionOrder(220), DisallowMultipleComponent]
    public sealed class ActorVfxPresentation : MonoBehaviour
    {
        private NetworkObject actor;
        private NetworkCharacterStats stats;
        private NetworkPlayerCombat combat;
        private NetworkTestPlayer player;
        private NetworkEnemy enemy;
        private NetworkSessionProgression progression;
        private AbilityCaster caster;
        private CharacterVisual visual;
        private readonly VfxSequence attacks = new(), guardBreaks = new(), levels = new();
        private bool seededDeath, dead;
        private int round = -1;
        private string session;
        private VfxLease shield, mana;
        private sealed class Flight
        {
            internal AbilityDefinition Definition;
            internal VfxLease Lease;
            internal Vector3 Origin, Direction;
            internal float Travel;
        }
        private readonly List<Flight> flights = new();
        private VfxPool Pool => actor != null && actor.IsValid ? VfxAudience.Pool(actor.Runner) : null;
        private VfxRelation Relation(bool damaging = false) => VfxAudience.Relation(actor.Runner, VfxAudience.Team(gameObject), damaging);
        private Vector3 Point => visual != null && visual.GetSocket(CharacterSocket.Chest) != null ? visual.GetSocket(CharacterSocket.Chest).position : transform.position + Vector3.up * .8f;
        private void Awake()
        {
            actor = GetComponent<NetworkObject>(); stats = GetComponent<NetworkCharacterStats>();
            combat = GetComponent<NetworkPlayerCombat>(); player = GetComponent<NetworkTestPlayer>(); enemy = GetComponent<NetworkEnemy>();
            progression = GetComponent<NetworkSessionProgression>(); caster = GetComponent<AbilityCaster>();
            visual = GetComponentInChildren<CharacterVisual>(true);
        }
        private void OnEnable() { if (caster != null) caster.Presentation += Ability; Reset(); }
        private void Reset()
        {
            attacks.Reset(); guardBreaks.Reset(); levels.Reset(); seededDeath = false; round = -1; session = null;
            StopPersistent();
        }
        private void StopPersistent()
        {
            shield.Stop(); mana.Stop(); shield = default; mana = default;
            foreach (var flight in flights) flight.Lease.Stop();
            flights.Clear();
        }
        private void OnDisable() { if (caster != null) caster.Presentation -= Ability; Reset(); }
        private void LateUpdate()
        {
            if (actor == null || !actor.IsValid || stats == null || stats.MaxHealth <= 0) return;
            var match = NetworkMatchPacing.MatchFor(actor.Runner);
            if (match != null && match.Object != null && match.Object.IsValid && match.RoundNumber != round)
            {
                attacks.Reset(); guardBreaks.Reset(); levels.Reset(); seededDeath = false;
                StopPersistent(); round = match.RoundNumber;
            }
            if (player != null && player.IsSuspended)
            {
                StopPersistent(); attacks.Reset(); guardBreaks.Reset(); levels.Reset(); seededDeath = false;
                return;
            }
            if (!seededDeath) { seededDeath = true; dead = stats.Dead; }
            else if (!dead && stats.Dead)
            {
                var kind = GetComponent<NetworkKingObjective>() != null ? VfxKind.KingDeath
                    : enemy != null && (enemy.Definition?.Boss != null || GetComponent<BossEncounter>() != null) ? VfxKind.BossDeath : VfxKind.PlayerDeath;
                Pool?.Play(kind, Point, Relation()); dead = true;
            }
            else if (!stats.Dead) dead = false;
            if (stats.Dead) { StopPersistent(); return; }
            if (combat != null)
            {
                if (attacks.Observe(combat.AttackSequence) && combat.HeavyAttack && combat.AttackBusy)
                {
                    double now = combat.HasStateAuthority ? actor.Runner.LocalRenderTime : actor.Runner.RemoteRenderTime;
                    double age = now - combat.AttackStartTick * (double)actor.Runner.DeltaTime;
                    if (age >= 0 && age < combat.AttackSwingDuration) Pool?.Play(VfxKind.HeavyAttack, Point + transform.forward * .35f, Relation(true));
                }
                if (guardBreaks.Observe(combat.GuardBreakSequence)) Pool?.Play(VfxKind.GuardBreak, Point, Relation());
            }
            if (progression != null && progression.Revision > 0 && progression.Level > 0)
            {
                string current = progression.SessionId.ToString();
                if (session != current) { session = current; levels.Reset(); }
                if (levels.Observe(progression.Level)) Pool?.Play(VfxKind.LevelUp, Point, Relation());
            }
            if (caster != null)
            {
                if (caster.ShieldRemaining > 0 && !shield.IsAlive)
                    shield = Pool != null ? Pool.Play(VfxKind.MagicShield, Point, Relation(), follow: visual != null ? visual.GetSocket(CharacterSocket.Chest) : transform,
                        lifetime: caster.ShieldRemaining + .1f) : default;
                if (caster.ShieldRemaining <= 0) shield.Stop();
                if (caster.CastRemaining > 0 && !mana.IsAlive)
                    mana = Pool != null ? Pool.Play(VfxKind.ManaCast, Point, Relation(), follow: visual != null ? visual.GetSocket(CharacterSocket.RightHand) : transform,
                        lifetime: caster.CastRemaining + .1f) : default;
                if (caster.CastRemaining <= 0) mana.Stop();
            }
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i];
                flight.Travel += flight.Definition.ProjectileSpeed * Time.deltaTime;
                if (!flight.Lease.IsAlive || flight.Travel >= flight.Definition.Range) { flight.Lease.Stop(); flights.RemoveAt(i); continue; }
                flight.Lease.Move(flight.Origin + flight.Direction * flight.Travel);
            }
        }
        private void Ability(AbilityDefinition definition, AbilityCue cue, Vector3 position, Vector3 direction)
        {
            if (actor == null || !actor.IsValid || definition == null || Pool == null || (player != null && player.IsSuspended)) return;
            if (cue == AbilityCue.CastStarted)
            {
                if (definition.CastTime <= 0) Pool.Play(VfxKind.ManaCast, Point, Relation(), lifetime: .12f);
                else if (!mana.IsAlive)
                    mana = Pool.Play(VfxKind.ManaCast, Point, Relation(), follow: visual != null ? visual.GetSocket(CharacterSocket.RightHand) : transform,
                        lifetime: definition.CastTime + .1f);
                return;
            }
            if (cue == AbilityCue.Interrupted) { mana.Stop(); return; }
            if (cue == AbilityCue.ShieldEnded) { shield.Stop(); return; }
            if (cue == AbilityCue.Released)
            {
                mana.Stop();
                if (definition.Effect == AbilityEffect.FireProjectile)
                {
                    var lease = Pool.Play(VfxKind.FireProjectile, position, Relation(true), direction: direction, lifetime: definition.Range / definition.ProjectileSpeed + .2f);
                    lease.Radius(definition.ProjectileRadius);
                    if (lease.IsAlive) flights.Add(new Flight { Definition = definition, Lease = lease, Origin = position, Direction = direction.normalized });
                }
            }
            if (cue != AbilityCue.Impact) return;
            if (definition.Effect == AbilityEffect.Heal) Pool.Play(VfxKind.Healing, position, Relation());
            else if (definition.Effect == AbilityEffect.FireProjectile)
            {
                // Existing cues carry no projectile ID. Retire the nearest matching visual only.
                int nearest = -1; float distance = float.PositiveInfinity;
                for (int i = 0; i < flights.Count; i++)
                {
                    if (flights[i].Definition != definition) continue;
                    float delta = (flights[i].Origin + flights[i].Direction * flights[i].Travel - position).sqrMagnitude;
                    if (delta < distance) { distance = delta; nearest = i; }
                }
                if (nearest >= 0) { flights[nearest].Lease.Stop(); flights.RemoveAt(nearest); }
                Pool.Play(VfxKind.MeleeHit, position, Relation(true));
            }
        }
    }
}
