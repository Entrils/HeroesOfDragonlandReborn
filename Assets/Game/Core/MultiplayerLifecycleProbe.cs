using UnityEngine;
#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Explicit development acceptance only; never runs in ordinary play.
    public sealed class MultiplayerLifecycleProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        private bool active, seeded, campKilled, chestOpened, finished, sawReturn, sawSecondStart, finalCastStarted;
        private NetworkMatch observed;
        private float playingAt;
        private int checkedRound, checkedFinishRound, endEvents;
        private readonly Dictionary<PlayerRef, NetworkId> oldPlayers = new();
        private readonly Dictionary<PlayerRef, TeamId> oldTeams = new();
        private readonly List<NetworkId> oldWorld = new();
        private string oldSession, previousResultId;
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
                        Check(currentCamp.State == (currentCamp.ConfiguredFirstSpawn > match.PlayingSeconds
                            ? NetworkCampState.Pending : NetworkCampState.Active)
                            && currentCamp.DeadCount == 0 && currentCamp.CompletionCount == 0, "camp-reset-and-scheduled");
                    if (!runner.IsServer) Check(!match.RetryPreparation(), "client-transition-rejected");
                }
                if (!runner.IsServer) return;
                // Allow every client to inspect the pristine replicated round first.
                if (Time.realtimeSinceStartup - playingAt < 3) return;
                if (match.RoundNumber == 2)
                {
                    if (!finalCastStarted) CastFinalHit(runner, players);
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
                    camp = InSession<NetworkCamp>(runner).First(c => c.State == NetworkCampState.Active);
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
                    var chest = chestObject.GetComponent<NetworkLootChest>();
                    Check(chest.TryOpenFor(player.InputAuthority, out _), "open-round-chest");
                    Check(!chest.TryOpenFor(player.InputAuthority, out _), "reject-duplicate-chest-claim");
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
                CheckDetailedResult(match);
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

        private void CastFinalHit(NetworkRunner runner, NetworkObject[] players)
        {
            finalCastStarted = true;
            foreach (var enemy in InSession<NetworkEnemy>(runner)) enemy.GetComponent<EnemyBrain>().enabled = false;
            var king = InSession<NetworkKingObjective>(runner).First(k => k.Team == TeamId.TeamB);
            var attacker = players.First(player => player.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA);
            var target = king.GetComponentInChildren<CapsuleCollider>().bounds.center;
            var direction = new[] { king.transform.forward, -king.transform.forward, Vector3.left, Vector3.right }
                .FirstOrDefault(candidate => Physics.SphereCast(target + candidate * 2f, .2f, -candidate, out var hit, 2f,
                    ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<NetworkKingObjective>() == king);
            Check(direction != Vector3.zero, "unobstructed-finisher-approach");
            var position = target + direction * 2f - Vector3.up;
            attacker.GetComponent<NetworkTransform>().Teleport(position, Quaternion.LookRotation(-direction));
            Check(king.Stats.ApplyDamage(king.Stats.CurrentHealth - 1f) > 0, "seed-king-health-for-real-finisher");
            var skills = attacker.GetComponent<SessionSkills>();
            Check(skills.TryLearn(3, skills.SessionId, out _) && skills.TryActivate(3, skills.SessionId, out _),
                "cast-confirmed-projectile-at-king");
        }

        private void CheckDetailedResult(NetworkMatch match)
        {
            var result = match.LatestResult;
            Check(result != null && result.RoundNumber == match.RoundNumber && result.Winner == match.Winner
                && result.DurationSeconds >= 3 && result.Players.Count == 2
                && Guid.TryParseExact(result.Id, "N", out _) && result.Id != previousResultId, "authoritative-detailed-result");
            previousResultId = result.Id;
            Check(result.Players.Count(row => row.Team == TeamId.TeamA) == 1
                && result.Players.Count(row => row.Team == TeamId.TeamB) == 1, "result-has-both-teams");
            if (match.RoundNumber == 1)
            {
                Check(result.Players.All(row => row.Statistics.ExperienceEarned == 1000 && row.Statistics.HighestLevel > 1
                    && row.Statistics.ItemsAcquired >= 2 && row.Statistics.DamageTaken >= 20), "round-one-earned-totals");
                Check(result.TeamA.ChestsOpened + result.TeamB.ChestsOpened == 1, "chest-not-credited-twice");
                Check(result.TeamA.KingDamage + result.TeamB.KingDamage == 0, "unattributed-king-fixture-does-not-credit-damage");
            }
            else
            {
                Check(result.Players.All(row => row.Statistics.ExperienceEarned == 0 && row.Statistics.ItemsAcquired == 0
                    && row.Statistics.ChestsOpened == 0 && row.Statistics.HighestLevel == 1), "new-round-counters-reset");
                Check(Mathf.Approximately(result.TeamA.KingDamage, 1f) && result.TeamA.KingsKilled == 1
                    && result.TeamB.KingDamage == 0, "real-fatal-king-hit-included-in-result");
            }
        }
#endif
    }
}
