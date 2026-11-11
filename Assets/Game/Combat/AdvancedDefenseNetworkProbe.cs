using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.Combat
{
    // Explicit development acceptance only. Never installed during normal matches.
    [DefaultExecutionOrder(210)]
    public sealed class AdvancedDefenseNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrDefenseCheck")) return;
            var bootstrap = FindAnyObjectByType<MultiplayerBootstrap>();
            if (bootstrap != null) bootstrap.gameObject.AddComponent<AdvancedDefenseNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private float began = -1, nextReport, lowestPoise = float.PositiveInfinity;
        private int serverStage = -10;
        private int reactedAttack;
        private bool parryA, parryB, remoteParry, attackerStagger, guardBreak, poiseBreak, regenerated, deniedReplica, complete;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("[DefenseNetwork] FAIL " + label); }
        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)) return;
            var humans = FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 2) return;
            if (began < 0) began = Time.realtimeSinceStartup;
            float age = Time.realtimeSinceStartup - began;
            int stage = age < 1 ? -1 : age < 13 ? 0 : age < 25 ? 1 : age < 29 ? 2 : age < 34 ? 3 : 4;
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
            {
                bool teamA = local.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA;
                float phase = stage == 0 ? (age - 1) % 2 : stage == 1 ? (age - 13) % 2 : (age - 25) % 2;
                bool tap = phase > .2f && phase < .35f;
                bootstrap.TestInput = Vector2.zero; bootstrap.TestFacing = new Vector2(teamA ? 1 : -1, 0);
                bootstrap.TestDodge = bootstrap.TestHeavyAttack = false;
                bootstrap.TestAttack = stage == 0 ? teamA && tap : stage == 1 ? !teamA && tap
                    : stage == 2 ? teamA && phase > .5f && phase < .65f
                    : stage == 3 ? teamA && (int)(age * 10) % 2 == 0
                    : stage == 4 && teamA && age > 34.5f && age < 34.65f;
                bootstrap.TestParry = (stage == 0 ? !teamA : stage == 1 && teamA) && tap;
                if (stage == 1 && teamA)
                {
                    // React to the accepted remote strike rather than assuming symmetric input transit time.
                    var incoming = humans.First(p => p.Team != TeamId.TeamA).GetComponent<NetworkPlayerCombat>();
                    bool react = incoming.AttackBusy && incoming.AttackSequence != reactedAttack;
                    bootstrap.TestParry = react;
                    if (react) reactedAttack = incoming.AttackSequence;
                }
                bootstrap.TestBlock = stage == 2 && !teamA;
            }
            if (runner.IsServer)
            {
                // Isolate timing from autonomous third-party hits; bots still satisfy normal lobby admission.
                foreach (var bot in FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && p.IsBot))
                    bot.GetComponent<MeleeAttack>().enabled = false;
                foreach (var enemy in FindObjectsByType<NetworkEnemy>().Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner))
                    enemy.GetComponent<EnemyMeleeAttack>().enabled = false;
                bool reset = serverStage != stage; serverStage = stage;
                foreach (var human in humans)
                {
                    var stats = human.GetComponent<CharacterStats>(); var inventory = human.GetComponent<Inventory>(); var gear = human.GetComponent<Equipment>();
                    if (reset)
                    {
                        human.GetComponent<NetworkPlayerCombat>().CancelForRespawn(); stats.ResetToFull();
                        bool teamA = human.Team == TeamId.TeamA;
                        human.GetComponent<NetworkTransform>().Teleport(new Vector3(teamA ? -.7f : .7f, 1, -18), Quaternion.LookRotation(teamA ? Vector3.right : Vector3.left));
                        if (stage == 2 && !teamA) stats.TrySpendStamina(Mathf.Max(0, stats.CurrentStamina - 10));
                    }
                    stats.Heal(stats.MaxHealth); // HP fixture only; preserves live poise/stamina and real attack costs.
                    if (gear.EquippedWeapon == null)
                    {
                        var sword = inventory.Catalog.Items.First(i => i.Weapon != null && i.Weapon.Archetype == WeaponArchetype.Sword && i.Rarity == ItemRarity.Common);
                        if (inventory.GetAmount(sword.Id) == 0) human.GetComponent<NetworkInventory>().TryAddItem(sword.Id);
                        for (int slot = 0; slot < inventory.SlotCount; slot++) if (inventory.GetSlot(slot).ItemId == sword.Id)
                        { Check(gear.TryEquip(slot, EquipmentSlot.MainHand, out _), "normal server sword equip"); break; }
                    }
                }
            }
            foreach (var human in humans)
            {
                var combat = human.GetComponent<NetworkPlayerCombat>();
                Check(combat.MaxPoise > 0 && combat.Poise >= 0 && combat.Poise <= combat.MaxPoise, "bounded replicated poise");
                if (combat.ParrySuccessSequence > 0) { if (human.Team == TeamId.TeamA) parryA = true; else parryB = true; }
                if (!human.HasInputAuthority && combat.ParryBusy) remoteParry = true;
                if (combat.Staggered && combat.LastDefenseInterrupt == DefenseInterrupt.Parried && !combat.AttackBusy) attackerStagger = true;
                if (combat.GuardBreakSequence > 0) guardBreak = true;
                if (combat.Staggered && combat.LastDefenseInterrupt == DefenseInterrupt.PoiseBreak) poiseBreak = true;
                if (stage == 4 && age > 35 && human.Team == TeamId.TeamB)
                {
                    lowestPoise = Mathf.Min(lowestPoise, combat.Poise);
                    if (lowestPoise < combat.MaxPoise - 1 && combat.Poise > lowestPoise + 1) regenerated = true;
                }
                if (!runner.IsServer && !human.HasInputAuthority && !deniedReplica)
                {
                    var parry = human.GetComponent<ParryDefense>(); var poise = human.GetComponent<CharacterPoise>();
                    float before = poise.Current;
                    Check(!parry.TryStart(Vector3.forward), "replica cannot start a parry");
                    human.GetComponent<CombatDefense>().ApplyPoiseDamage(10000);
                    Check(poise.Current == before && !human.GetComponent<CombatDefense>().IsStaggered, "replica cannot damage poise or force a stagger");
                    deniedReplica = true;
                }
            }
            if (!complete && age >= 39 && parryA && parryB && remoteParry && attackerStagger && guardBreak && poiseBreak && regenerated && (runner.IsServer || deniedReplica))
            {
                complete = true;
                Debug.Log("[DefenseNetwork] PASS real two-way parries, remote guard pose/attacker interruption, guard break, poise break/regen and replica authority rejection; server=" + runner.IsServer);
            }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                Debug.Log("[DefenseNetwork] server=" + runner.IsServer + " age=" + age.ToString("F1") + " parry=" + parryA + "/" + parryB
                    + " remote=" + remoteParry + " interrupt=" + attackerStagger + " guard=" + guardBreak + " poise=" + poiseBreak + " regen=" + regenerated + " complete=" + complete);
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
