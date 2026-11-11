using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
#endif

namespace HeroesOfDragonland.Skills
{
    // Explicit test mode only. Gameplay and UI never grant XP or allocate attributes here.
    public sealed class SessionSkillsAcceptanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, complete;
        private int localStep, serverStep;
        private float nextAction, serverActionAt;
        private string initialSession;
        private readonly HashSet<NetworkObject> seeded = new(), guardsChecked = new();
        private readonly HashSet<NetworkSessionSkills> monitored = new();
        private NetworkObject attacker, victim;
        private bool hitObserved;
        private void Awake() => active = Environment.GetCommandLineArgs().Contains("-hodrSkills");
        private void Check(bool condition, string label)
        {
            if (!condition) { active = false; throw new InvalidOperationException("[SkillCheck] FAIL " + label); }
            Debug.Log("[SkillCheck] PASS " + label);
        }
        private void ServerFixtures(NetworkRunner runner)
        {
            var actors = runner.ActivePlayers.Select(player => runner.TryGetPlayerObject(player, out var actor) ? actor : null)
                .Where(actor => actor != null && actor.IsValid).ToArray();
            foreach (var actor in actors)
            {
                var skills = actor.GetComponent<SessionSkills>(); var stats = actor.GetComponent<CharacterStats>();
                if (skills.IsLearned(0) && seeded.Add(actor))
                {
                    var progression = actor.GetComponent<SessionProgression>();
                    Check(actor.GetComponent<NetworkSessionProgression>().TryGrantExperience(2000, "skills-fixture"), "server-grants-test-levels");
                    Check(progression.TryIncreaseAttribute(ItemStat.Vitality, progression.SessionId, out _)
                        && progression.TryIncreaseAttribute(ItemStat.Vitality, progression.SessionId, out _), "server-allocates-test-prerequisites");
                }
                if (skills.GuardRemaining > 0 && guardsChecked.Add(actor))
                {
                    float before = stats.CurrentHealth;
                    Check(stats.ApplyDamage(10) == 0 && stats.CurrentHealth == before, "server-guard-prevents-damage");
                    Check(Mathf.Approximately(skills.ModifyMeleeDamage(25), 30), "server-passive-adds-five-once");
                }
            }
            if (actors.Length != 2 || guardsChecked.Count != 2 || serverStep > 3) return;
            if (serverStep == 0)
            {
                if (actors.Any(actor => actor.GetComponent<SessionSkills>().GuardRemaining > 0
                    || actor.GetComponent<SessionSkills>().CooldownRemaining(0) > 0)) return;
                // Isolate hit geometry from the world AI without changing authored assets.
                foreach (var enemy in FindObjectsByType<NetworkEnemy>(FindObjectsInactive.Exclude)) if (enemy.Runner == runner) enemy.enabled = false;
                attacker = actors[0]; victim = actors[1];
                attacker.GetComponent<NetworkTransform>().Teleport(new Vector3(-5, 1, -5), Quaternion.identity);
                victim.GetComponent<NetworkTransform>().Teleport(new Vector3(-5, 1, -3.5f), Quaternion.identity);
                attacker.GetComponent<CharacterStats>().ResetToFull(); victim.GetComponent<CharacterStats>().ResetToFull();
                victim.GetComponent<DamageReceiver>().DamageTaken += ObserveHit;
                serverStep = 1; serverActionAt = Time.unscaledTime + .4f;
            }
            else if (serverStep == 1 && Time.unscaledTime > serverActionAt)
            {
                var skills = attacker.GetComponent<SessionSkills>();
                Debug.Log("[SkillFixture] attacker=" + attacker.transform.position + " victim=" + victim.transform.position);
                Check(skills.TryActivate(0, skills.SessionId, out _), "server-starts-real-skill-swing");
                serverStep = 2; serverActionAt = Time.unscaledTime + 1.5f;
            }
            else if (serverStep == 2 && Time.unscaledTime > serverActionAt)
            {
                Check(hitObserved, "server-skill-hit-passes-existing-damage-policy");
                victim.GetComponent<DamageReceiver>().DamageTaken -= ObserveHit;
                hitObserved = false;
                victim.GetComponent<CharacterStats>().ResetToFull();
                victim.GetComponent<DamageReceiver>().DamageTaken += ObserveNormalHit;
                attacker.GetComponent<NetworkPlayerCombat>().TickIntent(new NetworkPlayerInput { Attack = true, LightAttackPress = 1 }, Vector3.forward, false);
                serverStep = 3; serverActionAt = Time.unscaledTime + 1.5f;
            }
            else if (serverStep == 3 && Time.unscaledTime > serverActionAt)
            {
                Check(hitObserved, "passive-applies-to-normal-melee");
                victim.GetComponent<DamageReceiver>().DamageTaken -= ObserveNormalHit;
                GetComponent<MultiplayerBootstrap>().ResetRoundPlayers();
                serverStep = 4;
            }
        }
        private void ObserveHit(float amount, GameObject source)
        { if (source == attacker.gameObject) { Check(Mathf.Approximately(amount, 60), "strike-damage-is-two-times-base-plus-passive"); hitObserved = true; } }
        private void ObserveNormalHit(float amount, GameObject source)
        { if (source == attacker.gameObject) { Check(Mathf.Approximately(amount, 30), "normal-melee-includes-passive-once"); hitObserved = true; } }
        private void LateUpdate()
        {
            if (!active || complete) return;
            var bootstrap = GetComponent<MultiplayerBootstrap>(); var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)) return;
            if (runner.IsServer) ServerFixtures(runner);
            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var actor) || actor == null || !actor.IsValid) return;
            var skills = actor.GetComponent<SessionSkills>(); var network = actor.GetComponent<NetworkSessionSkills>();
            CheckComponents(skills, network);
            if (monitored.Add(network)) network.RequestResolved += (accepted, reason) => Check(accepted, "owner-rpc-result: " + reason);
            if (string.IsNullOrEmpty(skills.SessionId) || Time.unscaledTime < nextAction) return;
            nextAction = Time.unscaledTime + .4f;
            switch (localStep)
            {
                case 0:
                    initialSession = skills.SessionId;
                    Check(actor.HasInputAuthority && skills.LearnedMask == 0 && skills.SkillPoints == 1, "fresh-session-owned-skill-state");
                    Check(!skills.CanLearn(1, out _) && skills.MissingRequirements(1).Contains("Level"), "level-prerequisite");
                    Check(skills.MissingRequirements(2).Contains("Vitality"), "stat-prerequisite");
                    if (!runner.IsServer)
                        Check(!skills.TryLearn(0, skills.SessionId, out _) && skills.SkillPoints == 1, "client-cannot-directly-learn");
                    Check(network.RequestLearn(0), "owner-requests-first-skill"); localStep++; break;
                case 1:
                    if (!skills.IsLearned(0) || skills.SkillPoints < 2) return;
                    Check(!skills.TryLearn(0, "old-session", out _), "stale-session-rejected");
                    Check(network.RequestLearn(1), "owner-requests-defense"); localStep++; break;
                case 2:
                    if (!skills.IsLearned(1)) return;
                    Check(network.RequestLearn(2), "owner-requests-passive"); localStep++; break;
                case 3:
                    if (skills.LearnedMask != 7) return;
                    Check(!skills.CanActivate(2, out _), "passive-cannot-be-cast");
                    if (!runner.IsServer)
                        Check(!skills.TryActivate(0, skills.SessionId, out _) && Mathf.Approximately(skills.ModifyMeleeDamage(25), 25), "replica-cannot-apply-effects");
                    Check(network.RequestActivate(0), "owner-requests-strike"); localStep++; break;
                case 4:
                    if (skills.CooldownRemaining(0) <= 0 || actor.GetComponent<NetworkPlayerCombat>().AttackBusy
                        || actor.GetComponent<NetworkPlayerCombat>().IsBusy) return;
                    Check(actor.GetComponent<CharacterStats>().CurrentMana < actor.GetComponent<CharacterStats>().MaxMana, "mana-payment-replicated");
                    Check(!skills.CanActivate(0, out _), "cooldown-prevents-recast");
                    Check(network.RequestActivate(1), "owner-requests-guard"); localStep++; break;
                case 5:
                    if (skills.GuardRemaining <= 0) return;
                    Check(skills.CooldownRemaining(1) > 0, "defensive-effect-and-cooldown-replicated"); localStep++; break;
                case 6:
                    if (skills.SessionId == initialSession) return;
                    Check(skills.LearnedMask == 0 && skills.SkillPoints == 1 && skills.GuardRemaining == 0
                        && skills.CooldownRemaining(0) == 0 && skills.CooldownRemaining(1) == 0, "new-session-clears-all-skills-and-cooldowns");
                    complete = true; Debug.Log("[SkillsComplete] server=" + runner.IsServer); break;
            }
        }
        private void CheckComponents(SessionSkills skills, NetworkSessionSkills network)
        { if (skills == null || network == null) Check(false, "spawn-prefab-has-skill-components"); }
#endif
    }
}
