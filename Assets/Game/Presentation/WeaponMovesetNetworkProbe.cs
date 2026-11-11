using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
#endif

namespace HeroesOfDragonland.Presentation
{
    // Opt-in development fixture only. Normal sessions never install it.
    [DefaultExecutionOrder(200)]
    public sealed class WeaponMovesetNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrMovesetCheck") && !Environment.GetCommandLineArgs().Contains("-hodrChainCheck")) return;
            var bootstrap = FindFirstObjectByType<MultiplayerBootstrap>();
            if (bootstrap != null) bootstrap.gameObject.AddComponent<WeaponMovesetNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private float began = -1, nextReport;
        private readonly HashSet<string> observed = new();
        private readonly HashSet<uint> prepared = new();
        private bool deniedRemote, complete;
        private bool heldVerified;
        private bool sawBufferedHeavy;
        private readonly HashSet<uint> interrupted = new();
        private readonly Dictionary<uint, (int sequence, int tick, float swing, float recovery, bool heavy, WeaponMoveset moveset, int index)> previous = new();
        private bool ChainCheck => Environment.GetCommandLineArgs().Contains("-hodrChainCheck");
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException("[MovesetNetwork] FAIL " + label); }
        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)) return;
            var humans = FindObjectsByType<NetworkTestPlayer>(FindObjectsSortMode.None)
                .Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length < 2) return;
            if (began < 0) began = Time.realtimeSinceStartup;
            float age = Time.realtimeSinceStartup - began;
            int kind = Mathf.Min(3, (int)(age / 16)); float phase = age % 16;
            bootstrap.TestInput = Vector2.zero; bootstrap.TestFacing = Vector2.up;
            bool pulse = (int)(age * 8) % 2 == 0;
            bootstrap.TestAttack = age < 64 && ((ChainCheck && age > .25f && age < 2) || (phase > 2 && phase < 10 && pulse)
                || (ChainCheck && phase > 11 && phase < 11.25f));
            bootstrap.TestHeavyAttack = age < 64 && phase > (ChainCheck ? 11.5f : 11) && phase < 14 && pulse;
            bootstrap.TestDodge = bootstrap.TestBlock = false;
            foreach (var human in humans)
            {
                var inventory = human.GetComponent<Inventory>(); var gear = human.GetComponent<Equipment>();
                var melee = human.GetComponent<MeleeAttack>(); var stats = human.GetComponent<CharacterStats>();
                if (runner.IsServer)
                {
                    if (prepared.Add(human.Object.Id.Raw))
                    {
                        if (ChainCheck) melee.AttackStarted += () => Debug.Log("[ChainAccepted] actor=" + human.ParticipantNumber
                            + " sequence=" + human.GetComponent<NetworkPlayerCombat>().AttackSequence + " move=" + melee.MoveIndex
                            + " heavy=" + melee.IsHeavy + " tick=" + runner.Tick.Raw);
                        if (ChainCheck) melee.AttackFinished += () => Debug.Log("[ChainFinished] actor=" + human.ParticipantNumber
                            + " sequence=" + human.GetComponent<NetworkPlayerCombat>().AttackSequence + " elapsed=" + melee.AttackElapsed
                            + " stagger=" + human.GetComponent<CombatDefense>().IsStaggered + " tick=" + runner.Tick.Raw);
                        // Test setup grants normal progression and inventory; no shared asset mutation.
                        var progression = human.GetComponent<SessionProgression>();
                        progression.TryAddExperience(1000000, progression.SessionId, "moveset-fixture", out _);
                        var attributes = human.GetComponent<CharacterAttributes>();
                        // Intelligence has no allocation path yet; configure only this server test actor's innate values.
                        JsonUtility.FromJsonOverwrite("{\"baseAttributes\":[{\"stat\":3,\"value\":25},{\"stat\":4,\"value\":25},{\"stat\":5,\"value\":25},{\"stat\":8,\"value\":1}]}", attributes);
                        foreach (var stat in new[] { ItemStat.Strength, ItemStat.Dexterity, ItemStat.Intelligence })
                            for (int i = 0; i < 20 && attributes.TryGetBaseValue(stat, out float value) && value < (stat == ItemStat.Strength ? 16 : 12); i++)
                                if (!progression.TryIncreaseAttribute(stat, progression.SessionId, out _)) break;
                        human.GetComponent<NetworkTransform>().Teleport(new Vector3(human.ParticipantNumber % 2 == 0 ? 6 : -6, 1, -18));
                    }
                    stats.ResetToFull(); // Resource fixture; each accepted swing still pays through MeleeAttack.
                    var item = inventory.Catalog.Items.First(i => i.Weapon != null && (int)i.Weapon.Archetype == kind && i.Rarity == ItemRarity.Common);
                    if (gear.EquippedWeapon != item.Weapon && !melee.IsBusy)
                    {
                        if (inventory.GetAmount(item.Id) == 0) human.GetComponent<NetworkInventory>().TryAddItem(item.Id);
                        for (int slot = 0; slot < inventory.SlotCount; slot++)
                            if (inventory.GetSlot(slot).ItemId == item.Id)
                            { Check(gear.TryEquip(slot, EquipmentSlot.MainHand, out _), "server equip " + item.DisplayName); break; }
                    }
                }
                if (human.HasInputAuthority) continue;
                if (!runner.IsServer && !deniedRemote)
                {
                    Check(!melee.TryAttack(), "replica cannot start gameplay attack");
                    Check(!gear.TryUnequip(EquipmentSlot.MainHand, out _), "replica cannot mutate equipment"); deniedRemote = true;
                }
                var combat = human.GetComponent<NetworkPlayerCombat>();
                var visual = human.GetComponent<NetworkCharacterVisual>().Visual;
                // Autonomous bots remain active in this fixture. A replicated forced interruption legally ends a chain.
                if (combat.Staggered || human.GetComponent<NetworkCharacterStats>().Dead || human.GetComponent<NetworkPlayerDodge>().Busy)
                    interrupted.Add(human.Object.Id.Raw);
                if (ChainCheck && age > 1.7f && age < 2 && combat.AttackSequence == 1) heldVerified = true;
                if (combat.AttackSequence > 0 && inventory.TryResolveItem(combat.AttackWeaponId.ToString(), out var committed))
                {
                    var moveSet = committed.Weapon.Moveset;
                    bool changed = !previous.TryGetValue(human.Object.Id.Raw, out var prior) || combat.AttackSequence != prior.sequence;
                    bool forced = changed && interrupted.Remove(human.Object.Id.Raw);
                    if (!forced && prior.sequence > 0 && combat.AttackSequence == prior.sequence + 1)
                    {
                        float gap = (combat.AttackStartTick - prior.tick) * runner.DeltaTime;
                        var priorRule = prior.moveset.Combo.GetRule(prior.heavy, prior.index);
                        float earliest = prior.swing + prior.recovery * priorRule.MinimumRecovery;
                        Check(gap + runner.DeltaTime >= earliest, "server enforced mandatory swing/recovery under spam");
                        if (gap < prior.swing + prior.recovery - runner.DeltaTime)
                        {
                            Check(moveSet == prior.moveset && !prior.heavy
                                && (combat.HeavyAttack ? priorRule.AllowHeavy : combat.AttackMoveIndex == priorRule.NextLightIndex),
                                "server legal buffered transition seq=" + prior.sequence + "->" + combat.AttackSequence
                                + " move=" + prior.index + "/" + prior.heavy + "->" + combat.AttackMoveIndex + "/" + combat.HeavyAttack
                                + " expected=" + priorRule.NextLightIndex + " same=" + (moveSet == prior.moveset)
                                + " gap=" + gap + " duration=" + (prior.swing + prior.recovery));
                            if (combat.HeavyAttack) sawBufferedHeavy = true;
                        }
                    }
                    var acceptedMove = moveSet.GetMove(combat.HeavyAttack, combat.AttackMoveIndex);
                    previous[human.Object.Id.Raw] = (combat.AttackSequence, combat.AttackStartTick, combat.AttackSwingDuration,
                        acceptedMove.Profile.RecoveryDuration / acceptedMove.AttackSpeed, combat.HeavyAttack, moveSet, combat.AttackMoveIndex);
                }
                if (!visual.State.Attacking || visual.Action != (combat.HeavyAttack ? CharacterAction.HeavyAttack : CharacterAction.LightAttack)) continue;
                if (!inventory.TryResolveItem(combat.AttackWeaponId.ToString(), out var accepted)) continue;
                var moveset = accepted.Weapon.Moveset; var move = moveset.GetMove(combat.HeavyAttack, combat.AttackMoveIndex);
                Check(move != null && Mathf.Abs(combat.AttackSwingDuration - move.Profile.AttackDuration / move.AttackSpeed) < .001f, "accepted duration");
                var controller = visual.Animator.runtimeAnimatorController as AnimatorOverrideController;
                var clip = visual.AnimationLibrary.Get(combat.HeavyAttack ? CharacterMotion.HeavyAttack : CharacterMotion.LightAttack);
                Check(controller != null && controller[clip] == moveset.GetAnimation(combat.HeavyAttack, combat.AttackMoveIndex), "remote clip matches accepted move");
                observed.Add(accepted.Weapon.Archetype + ":" + (combat.HeavyAttack ? "H" : combat.AttackMoveIndex.ToString()));
            }
            var required = humans[0].GetComponent<Inventory>().Catalog.Items.Where(i => i.Weapon != null && i.Rarity == ItemRarity.Common)
                .SelectMany(i => Enumerable.Range(0, i.Weapon.Moveset.LightAttackCount).Select(n => i.Weapon.Archetype + ":" + n).Append(i.Weapon.Archetype + ":H")).Distinct().ToArray();
            if (!complete && required.All(observed.Contains) && (runner.IsServer || deniedRemote) && (!ChainCheck || (heldVerified && sawBufferedHeavy)))
            { complete = true; Debug.Log((ChainCheck ? "[AttackChainNetwork]" : "[MovesetNetwork]") + " PASS four real remote finite chains/heavies, accepted durations/clips, mandatory recovery under repeated presses, held-input deduplication and replica authority rejection; server=" + runner.IsServer); }
            if (Time.realtimeSinceStartup > nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 8;
                Debug.Log("[MovesetNetwork] server=" + runner.IsServer + " age=" + age.ToString("F1") + " missing=" + string.Join(",", required.Where(k => !observed.Contains(k))));
            }
        }
        private void OnDisable()
        {
            if (bootstrap == null) return;
            bootstrap.TestInput = bootstrap.TestFacing = null;
            bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestDodge = bootstrap.TestBlock = null;
        }
#endif
    }
}
