using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Collections.Generic;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Development automation submits the same movement/attack intent as local input.
    // No teleport, damage, resource or team command is exposed over the network.
    public sealed class MultiplayerCombatProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerBootstrap bootstrap;
        private string scenario;
        private float nextReport;
        private bool tamperChecked;
        private bool deathTamperChecked;
        private bool disconnectIssued;
        private readonly HashSet<Fusion.NetworkObject> observedActors = new HashSet<Fusion.NetworkObject>();
        private readonly HashSet<string> checkedWindows = new HashSet<string>();
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-hodrCombat") scenario = args[i + 1];
        }
        private void Update()
        {
            if (string.IsNullOrEmpty(scenario) || bootstrap == null) return;
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning) return;
            if (scenario == "weapon" && runner.IsServer)
                foreach (var peer in runner.ActivePlayers)
                {
                    if (!runner.TryGetPlayerObject(peer, out var actor) || actor.GetComponent<NetworkTestPlayer>().ParticipantNumber != 2) continue;
                    var equipment = actor.GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>();
                    var inventory = actor.GetComponent<HeroesOfDragonland.InventorySystem.Inventory>();
                    if (equipment == null || equipment.EquippedWeapon != null || actor.GetComponent<NetworkPlayerCombat>().IsBusy) continue;
                    var sword = inventory.Catalog.Items.First(item => item.name == "IronSword");
                    if (inventory.GetAmount(sword.Id) == 0) actor.GetComponent<NetworkInventory>().TryAddItem(sword.Id);
                    for (int i = 0; i < inventory.SlotCount; i++)
                        if (inventory.GetSlot(i).ItemId == sword.Id && equipment.TryEquip(i, HeroesOfDragonland.Items.EquipmentSlot.MainHand, out _))
                        { Debug.Log("[WeaponCheck] server equipped client sword"); break; }
                }
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var obj) && obj != null)
            {
                var local = obj.GetComponent<NetworkTestPlayer>();
                var state = obj.GetComponent<NetworkCharacterStats>();
                int attacker = scenario == "client_hits" || scenario == "weapon" ? 2 : 1;
                int victim = scenario == "friendly" ? 3 : (attacker == 1 ? 2 : 1);
                bootstrap.TestInput = Vector2.zero;
                bootstrap.TestAttack = false;
                bootstrap.TestDodge = false;
                bootstrap.TestHeavyAttack = false;
                bootstrap.TestBlock = scenario == "block" && local.ParticipantNumber == victim;
                if (scenario == "dodge" && local.ParticipantNumber == 2)
                {
                    // Holding both requests exercises action priority and exhausted-resource rejection.
                    bootstrap.TestDodge = true;
                    bootstrap.TestAttack = true;
                    bootstrap.TestInput = new Vector2(1, 0);
                }
                if (local.ParticipantNumber == attacker && !state.Dead)
                {
                    foreach (var peer in runner.ActivePlayers)
                    {
                        if (!runner.TryGetPlayerObject(peer, out var target) || target == null
                            || target.GetComponent<NetworkTestPlayer>().ParticipantNumber != victim) continue;
                        Vector3 delta = target.transform.position - obj.transform.position;
                        Vector2 direction = new Vector2(delta.x, delta.z);
                        if (direction.magnitude > 1.65f) bootstrap.TestInput = direction.normalized;
                        // Also exercises repeated/out-of-range intent: server owns costs and hits.
                        bootstrap.TestAttack = scenario != "heavy";
                        bootstrap.TestHeavyAttack = scenario == "heavy";
                    }
                }
                if (scenario == "disconnect" && local.ParticipantNumber == victim && state.Health < state.MaxHealth && !disconnectIssued)
                {
                    disconnectIssued = true;
                    Debug.Log("[CombatProbe] disconnect-during-combat");
                    _ = bootstrap.Disconnect();
                    return;
                }
                if (!runner.IsServer && ((!tamperChecked && !state.Dead && (state.Health < state.MaxHealth
                        || (scenario == "weapon" && obj.GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>()?.EquippedWeapon != null)))
                    || (!deathTamperChecked && state.Dead)))
                {
                    if (state.Dead) deathTamperChecked = true; else tamperChecked = true;
                    var stats = obj.GetComponent<CharacterStats>();
                    float hp = stats.CurrentHealth, sp = stats.CurrentStamina, mp = stats.CurrentMana;
                    stats.ApplyDamage(10000); stats.Heal(10000); stats.ResetToFull(); stats.ConfigureForSpawn(null);
                    bool staminaSpent = stats.TrySpendStamina(1), manaSpent = stats.TrySpendMana(1);
                    stats.TickRegeneration(10000);
                    bool safe = hp == stats.CurrentHealth && sp == stats.CurrentStamina && mp == stats.CurrentMana
                        && !staminaSpent && !manaSpent;
                    if (scenario == "weapon") safe = safe && !obj.GetComponent<MeleeAttack>().TryAttack()
                        && !obj.GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>().TryUnequip(HeroesOfDragonland.Items.EquipmentSlot.MainHand, out _);
                    Debug.Log("[CombatProbe] client-mutations-blocked=" + safe + " dead=" + state.Dead);
                }
            }
            foreach (var peer in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(peer, out var actor) || actor == null) continue;
                if (runner.IsServer && observedActors.Add(actor))
                {
                    var victim = actor;
                    actor.GetComponent<DamageReceiver>().DamageTaken += (amount, source) =>
                    {
                        var attack = source != null ? source.GetComponent<NetworkPlayerCombat>() : null;
                        if (attack != null)
                            Debug.Log(FormattableString.Invariant($"[ActionHit] source={attack.Object.Id} victim={victim.Id} sequence={attack.AttackSequence} amount={amount:F2}"));
                    };
                }
                if (scenario != "dodge" || !runner.IsServer) continue;
                var dodge = actor.GetComponent<NetworkPlayerDodge>();
                if (!dodge.Busy || actor.GetComponent<NetworkCharacterStats>().Dead) continue;
                float age = (runner.Tick.Raw - dodge.StartTick) * runner.DeltaTime;
                string phase = age < dodge.Definition.InvulnerabilityStartTime ? "before"
                    : age < dodge.Definition.InvulnerabilityEndTime ? "inside" : "after";
                if (!checkedWindows.Add(actor.Id + "/" + dodge.DodgeSequence + "/" + phase)) continue;
                var stats = actor.GetComponent<CharacterStats>();
                // Development-only authoritative test damage, immediately healed outside
                // the window. Never accepts client damage values or changes release behavior.
                float damage = stats.ApplyDamage(1);
                bool passed = damage == (phase == "inside" ? 0f : 1f);
                if (damage > 0) stats.Heal(damage);
                Debug.Log($"[DodgeWindow] phase={phase} pass={passed} sequence={dodge.DodgeSequence}");
            }
            if (Time.realtimeSinceStartup < nextReport) return;
            nextReport = Time.realtimeSinceStartup + 0.5f;
            foreach (var peer in runner.ActivePlayers.OrderBy(p => p.RawEncoded))
            {
                if (!runner.TryGetPlayerObject(peer, out var actor) || actor == null) continue;
                var state = actor.GetComponent<NetworkCharacterStats>();
                var resources = actor.GetComponent<NetworkPlayerResources>();
                var combat = actor.GetComponent<NetworkPlayerCombat>();
                var dodgeState = actor.GetComponent<NetworkPlayerDodge>();
                var actionView = actor.GetComponent<NetworkActionPresentation>();
                Debug.Log(FormattableString.Invariant($"[CombatProbe] local={runner.LocalPlayer} owner={actor.InputAuthority} id={actor.Id} hp={state.Health:F2} max={state.MaxHealth:F2} dead={state.Dead} stamina={resources.Stamina:F2} mana={resources.Mana:F2} privateMax={resources.MaxStamina:F2} attacks={combat.AttackSequence}"));
                Debug.Log(FormattableString.Invariant($"[ActionProbe] owner={actor.InputAuthority} attack={combat.AttackSequence} attackTick={combat.AttackStartTick} attacking={combat.AttackBusy} dodge={dodgeState.DodgeSequence} dodgeTick={dodgeState.StartTick} dodging={dodgeState.Busy} x={actor.transform.position.x:F3} z={actor.transform.position.z:F3} dt={runner.DeltaTime:F6}"));
                var equipmentState = actor.GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>();
                Debug.Log(FormattableString.Invariant($"[WeaponProbe] owner={actor.InputAuthority} item={equipmentState?.GetItemId(HeroesOfDragonland.Items.EquipmentSlot.MainHand) ?? "none"} archetype={equipmentState?.EquippedWeapon?.Archetype.ToString() ?? "Unarmed"} duration={combat.AttackSwingDuration:F3}"));
                Debug.Log($"[DefenseProbe] owner={actor.InputAuthority} heavy={combat.HeavyAttack} block={combat.Blocking} stagger={combat.Staggered} breaks={combat.GuardBreakSequence}");
                Debug.Log($"[ActionView] owner={actor.InputAuthority} swing={actionView.SwingVisible} roll={actionView.RollVisible}");
            }
        }
#endif
    }
}
