using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.Abilities
{
    // Explicit development fixture only. Requests use the ordinary owning-player RPC.
    public sealed class MagicNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrMagicCheck")) return;
            var bootstrap = FindAnyObjectByType<MultiplayerBootstrap>();
            if (bootstrap != null) bootstrap.gameObject.AddComponent<MagicNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private readonly HashSet<NetworkObject> seeded = new();
        private readonly HashSet<int> requested = new();
        private float began = -1, nextReport, lowestA = float.PositiveInfinity;
        private bool shield, absorbed, damageA, healed, damageB, cast, denied, complete;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("[MagicNetwork] FAIL " + label); }
        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            if (complete || runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)) return;
            var humans = FindObjectsByType<NetworkTestPlayer>(FindObjectsSortMode.None)
                .Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 2) return;
            if (began < 0) began = Time.realtimeSinceStartup;
            float age = Time.realtimeSinceStartup - began;
            if (runner.IsServer)
            {
                foreach (var bot in FindObjectsByType<NetworkTestPlayer>(FindObjectsSortMode.None).Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && p.IsBot))
                    bot.GetComponent<MeleeAttack>().enabled = false;
                foreach (var enemy in FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None).Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner))
                    enemy.GetComponent<EnemyMeleeAttack>().enabled = false;
                foreach (var human in humans)
                {
                    if (!seeded.Add(human.Object)) continue;
                    human.GetComponent<NetworkPlayerCombat>().CancelForRespawn();
                    var progression = human.GetComponent<SessionProgression>();
                    Check(human.GetComponent<NetworkSessionProgression>().TryGrantExperience(10000, "m45-network"), "server XP grant");
                    var skills = human.GetComponent<SessionSkills>();
                    for (int i = 3; i < 6; i++) Check(skills.TryLearn(i, skills.SessionId, out _), "normal skill unlock " + i);
                    human.GetComponent<CharacterStats>().ResetToFull();
                    bool a = human.Team == TeamId.TeamA;
                    human.GetComponent<NetworkTransform>().Teleport(new Vector3(a ? -2 : 2, 1, -18), Quaternion.LookRotation(a ? Vector3.right : Vector3.left));
                }
            }
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
            {
                bool a = local.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA;
                bootstrap.TestInput = Vector2.zero; bootstrap.TestFacing = new Vector2(a ? 1 : -1, 0);
                bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestParry = bootstrap.TestBlock = bootstrap.TestDodge = false;
                int step = age < 1 ? -1 : age < 3 ? 0 : age < 6 ? 1 : age < 9 ? 2 : age < 13 ? 3 : 4;
                bool mine = step == 0 ? !a : step == 1 ? a : step == 2 ? !a : step >= 3 && a;
                int index = step == 0 ? 5 : step == 3 ? 4 : 3;
                var skills = local.GetComponent<SessionSkills>(); var network = local.GetComponent<NetworkSessionSkills>();
                if (step >= 0 && mine && !requested.Contains(step) && network.CanRequest && skills.IsLearned(index))
                { Check(network.RequestActivate(index), "owning player request"); requested.Add(step); }
            }
            foreach (var human in humans)
            {
                var stats = human.GetComponent<CharacterStats>(); var magic = human.GetComponent<NetworkAbilities>();
                if (magic.CastRemaining > 0) cast = true;
                if (human.Team == TeamId.TeamB)
                {
                    if (magic.ShieldRemaining > 0 && magic.ShieldCapacity > 0) shield = true;
                    if (magic.ShieldRemaining > 0 && magic.ShieldCapacity > 0 && magic.ShieldCapacity < 40 && stats.CurrentHealth == stats.MaxHealth) absorbed = true;
                    if (age > 13 && stats.CurrentHealth < stats.MaxHealth) damageB = true;
                }
                else
                {
                    lowestA = Mathf.Min(lowestA, stats.CurrentHealth);
                    if (stats.CurrentHealth < stats.MaxHealth) damageA = true;
                    if (age > 9 && damageA && stats.CurrentHealth > lowestA + 1) healed = true;
                }
                if (!runner.IsServer && !denied)
                {
                    var skills = human.GetComponent<SessionSkills>(); var caster = human.GetComponent<AbilityCaster>();
                    float hp = stats.CurrentHealth, mana = stats.CurrentMana;
                    Check(!skills.TryActivate(3, skills.SessionId, out _) && !caster.TryStart(skills.GetDefinition(3).Ability, null, out _), "replica casting rejected");
                    Check(stats.ApplyDamage(1000) == 0 && stats.Heal(1000) == 0 && !stats.TrySpendMana(1), "replica effects rejected");
                    Check(stats.CurrentHealth == hp && stats.CurrentMana == mana, "replica resources unchanged"); denied = true;
                }
            }
            if (age > 17 && shield && absorbed && damageA && healed && damageB && cast && (runner.IsServer || denied))
            {
                complete = true; Debug.Log("[MagicNetwork] PASS owning RPC casts, replicated cast/shield, fire PvP absorption/overflow, healing and replica effect rejection; server=" + runner.IsServer);
            }
            if (age > 26) { complete = true; Check(false, "timeout waiting for replicated magic effects"); }
            if (Time.realtimeSinceStartup > nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 4;
                Debug.Log("[MagicNetwork] server=" + runner.IsServer + " age=" + age.ToString("F1") + " cast=" + cast + " shield=" + shield + " absorbed=" + absorbed + " damageA=" + damageA + " healed=" + healed + " damageB=" + damageB + " denied=" + denied);
            }
        }
        private void OnDisable()
        {
            if (bootstrap == null) return;
            bootstrap.TestInput = bootstrap.TestFacing = null;
            bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestParry = bootstrap.TestBlock = bootstrap.TestDodge = null;
        }
#endif
    }
}
