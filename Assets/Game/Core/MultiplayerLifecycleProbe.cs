using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Explicit development acceptance only; never runs in ordinary play.
    public sealed class MultiplayerLifecycleProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, seeded, campKilled, chestOpened, finished, sawReturn, sawSecondStart;
        private NetworkMatch observed;
        private float playingAt;
        private int checkedRound, checkedFinishRound, endEvents;
        private readonly Dictionary<PlayerRef, NetworkId> oldPlayers = new();
        private readonly Dictionary<PlayerRef, TeamId> oldTeams = new();
        private readonly List<NetworkId> oldWorld = new();
        private string oldSession;
        private NetworkCamp camp;
        private void Awake()
        {
            active = Environment.GetCommandLineArgs().Contains("-hodrLifecycle");
            if (active) Application.targetFrameRate = 60;
        }
        private void Check(bool ok, string label)
        {
            if (!ok) { active = false; throw new InvalidOperationException("[LifecycleCheck] FAIL " + label); }
            Debug.Log("[LifecycleCheck] PASS " + label);
        }
        private T[] InSession<T>(NetworkRunner runner) where T : NetworkBehaviour
            => FindObjectsByType<T>().Where(o => o.Object != null && o.Object.IsValid && o.Runner == runner).ToArray();
        private void Update()
        {
            if (!active) return;
            var bootstrap = GetComponent<MultiplayerBootstrap>();
            var runner = bootstrap.Runner;
            var match = GetComponent<NetworkFortressSpawner>().Match;
            if (runner == null || !runner.IsRunning || match == null || !match.Object.IsValid) return;
            if (observed != match)
            {
                observed = match;
                match.Ended += team => { endEvents++; Check(team == (endEvents == 1 ? TeamId.TeamB : TeamId.TeamA), "winner-event-per-round"); };
                match.StateChanged += phase => Debug.Log("[LifecycleObserved] server=" + runner.IsServer + " state=" + phase);
            }
            if (match.PreparationError.ToString().Length > 0) Check(false, match.PreparationError.ToString());
            if (match.State == MatchPhase.ReturningToLobby) sawReturn = true;
            if (match.State == MatchPhase.Starting && match.RoundNumber == 2) sawSecondStart = true;
            var players = runner.ActivePlayers.Select(p => { runner.TryGetPlayerObject(p, out var obj); return obj; })
                .Where(o => o != null && o.IsValid).ToArray();
            if (match.State != MatchPhase.Playing) playingAt = 0;
            if (match.State == MatchPhase.Playing && players.Length == 2)
            {
                if (playingAt == 0) playingAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - playingAt < 1) return;
                if (checkedRound != match.RoundNumber)
                {
                    checkedRound = match.RoundNumber;
                    foreach (var player in players)
                    {
                        var progression = player.GetComponent<SessionProgression>();
                        Check(progression.Level == 1 && progression.CurrentXP == 0 && progression.ProgressionPoints == 0, "level-xp-points-reset");
                        foreach (var stat in new[] { ItemStat.Vitality, ItemStat.Endurance, ItemStat.Mind, ItemStat.Strength, ItemStat.Dexterity })
                            Check(progression.GetAllocatedPoints(stat) == 0, "allocated-attribute-reset");
                        for (int i = 1; i <= 6; i++) Check(string.IsNullOrEmpty(player.GetComponent<Equipment>().GetItemId((EquipmentSlot)i)), "equipment-reset");
                        if (runner.IsServer || player.HasInputAuthority)
                            for (int i = 0; i < player.GetComponent<Inventory>().SlotCount; i++)
                                Check(player.GetComponent<Inventory>().GetSlot(i).Amount == 0, "inventory-reset");
                        var stats = player.GetComponent<NetworkCharacterStats>();
                        Check(stats.Health == stats.MaxHealth && !stats.Dead, "player-health-reset");
                        if (match.RoundNumber == 2)
                        {
                            Check(oldPlayers[player.InputAuthority] != player.Id, "new-avatar-invalidates-old-requests");
                            Check(oldTeams[player.InputAuthority] == player.GetComponent<NetworkTestPlayer>().Team, "team-preserved");
                            Check(progression.SessionId != oldSession, "new-progression-session");
                        }
                        else
                        {
                            oldPlayers[player.InputAuthority] = player.Id;
                            oldTeams[player.InputAuthority] = player.GetComponent<NetworkTestPlayer>().Team;
                            oldSession = progression.SessionId;
                        }
                    }
                    if (match.RoundNumber == 2)
                    {
                        Check(sawReturn && sawSecondStart, "return-and-new-countdown-observed");
                        foreach (var id in oldWorld) Check(!runner.TryFindObject(id, out _), "old-round-world-removed");
                        Check(InSession<NetworkLootChest>(runner).Length == 0, "old-chests-removed");
                    }
                    foreach (var king in InSession<NetworkKingObjective>(runner))
                        Check(king.GetComponent<NetworkCharacterStats>().Health == king.GetComponent<NetworkCharacterStats>().MaxHealth, "king-health-reset");
                    foreach (var enemy in InSession<NetworkEnemy>(runner)) Check(!enemy.GetComponent<NetworkCharacterStats>().Dead, "ai-alive-after-reset");
                    foreach (var currentCamp in InSession<NetworkCamp>(runner))
                        Check(currentCamp.State == NetworkCampState.Active && currentCamp.DeadCount == 0 && currentCamp.CompletionCount == 0, "camp-reset-and-active");
                    if (!runner.IsServer) Check(!match.RetryPreparation(), "client-transition-rejected");
                }
                if (!runner.IsServer) return;
                // Allow every client to inspect the pristine replicated round first.
                if (Time.realtimeSinceStartup - playingAt < 3) return;
                if (match.RoundNumber == 2)
                {
                    InSession<NetworkKingObjective>(runner).First(k => k.Team == TeamId.TeamB).Stats.ApplyDamage(99999);
                    return;
                }
                if (!seeded)
                {
                    seeded = true;
                    foreach (var player in players)
                    {
                        var inv = player.GetComponent<Inventory>();
                        Check(inv.TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "seed-item");
                        Check(player.GetComponent<Equipment>().TryEquip(0, EquipmentSlot.MainHand, out _), "seed-equipment");
                        Check(inv.TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "seed-retained-inventory");
                        Check(player.GetComponent<NetworkSessionProgression>().TryGrantExperience(1000, "round-reset-test"), "seed-xp-level");
                        var progression = player.GetComponent<SessionProgression>();
                        Check(progression.TryIncreaseAttribute(ItemStat.Vitality, progression.SessionId, out _), "seed-attribute");
                        Check(player.GetComponent<CharacterStats>().ApplyDamage(20) > 0, "damage-player");
                    }
                    oldWorld.AddRange(InSession<NetworkEnemy>(runner).Select(o => o.Object.Id));
                    oldWorld.AddRange(InSession<NetworkKingObjective>(runner).Select(o => o.Object.Id));
                    oldWorld.AddRange(InSession<NetworkCamp>(runner).Select(o => o.Object.Id));
                    camp = InSession<NetworkCamp>(runner).First();
                }
                if (!campKilled)
                {
                    campKilled = true;
                    foreach (var enemy in InSession<NetworkEnemy>(runner))
                        if (enemy.GetComponent<NetworkFortressGuard>() == null) enemy.GetComponent<CharacterStats>().ApplyDamage(99999);
                }
                if (!chestOpened && camp.State == NetworkCampState.Cleared && camp.ChestId != default
                    && runner.TryFindObject(camp.ChestId, out var chestObject))
                {
                    var player = players.First(p => p.HasInputAuthority);
                    player.GetComponent<NetworkTransform>().Teleport(chestObject.transform.position + Vector3.up);
                    Check(chestObject.GetComponent<NetworkLootChest>().TryOpenFor(player.InputAuthority, out _), "open-round-chest");
                    chestOpened = true; oldWorld.Add(chestObject.Id);
                    InSession<NetworkKingObjective>(runner).First(k => k.Team == TeamId.TeamA).Stats.ApplyDamage(99999);
                }
            }
            if (match.State == MatchPhase.Finished && !finished && checkedFinishRound != match.RoundNumber)
            {
                // Let the normal replica Render and event notifications consume the snapshot.
                if (match.SecondsRemaining > match.Configuration.FinishedDelay - 1) return;
                checkedFinishRound = match.RoundNumber;
                Check(!match.CanPlay && match.FinishCount == 1, "finished-gate-and-single-result");
                if (runner.IsServer)
                {
                    foreach (var player in players)
                    {
                        Check(!player.GetComponent<NetworkSessionProgression>().TryGrantExperience(100, "after-end"), "finished-xp-rejected");
                        Check(!player.GetComponent<Inventory>().TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "finished-loot-rejected");
                        Check(player.GetComponent<CharacterStats>().ApplyDamage(100) == 0, "finished-damage-rejected");
                    }
                }
                if (match.RoundNumber == 2)
                {
                    Check(endEvents == 2, "two-round-end-events");
                    finished = true; active = false;
                    Debug.Log("[LifecycleComplete] server=" + runner.IsServer);
                }
            }
        }
#endif
    }
}
