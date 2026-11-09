using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Items;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Explicit -hodrRespawn development fixture, with no normal gameplay behavior.
    public sealed class MultiplayerRespawnProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, prepared, killed, finished, sawReplicaRestore;
        private float readyAt, finishedAt;
        private readonly Dictionary<NetworkPlayerRespawn, int> deaths = new();
        private readonly Dictionary<NetworkPlayerRespawn, int> revivals = new();
        private readonly Dictionary<NetworkPlayerRespawn, long[]> revisions = new();
        private void Awake() => active = Environment.GetCommandLineArgs().Contains("-hodrRespawn");
        private void Check(bool ok, string label)
        {
            if (!ok) { active = false; throw new InvalidOperationException("[RespawnCheck] FAIL " + label); }
            Debug.Log("[RespawnCheck] PASS " + label);
        }
        private void Update()
        {
            if (!active) return;
            var runner = GetComponent<MultiplayerBootstrap>().Runner;
            var match = GetComponent<NetworkFortressSpawner>().Match;
            if (runner == null || !runner.IsRunning || match == null || !match.Object.IsValid) return;
            if (match.State != MatchPhase.Playing && match.State != MatchPhase.Finished) return;
            var actors = runner.ActivePlayers.Select(p => { runner.TryGetPlayerObject(p, out var obj); return obj; })
                .Where(o => o != null).Select(o => o.GetComponent<NetworkPlayerRespawn>()).ToArray();
            if (actors.Length != 2) return;
            foreach (var actor in actors)
            {
                if (deaths.ContainsKey(actor)) continue;
                deaths[actor] = 0; revivals[actor] = 0;
                actor.Died += () => { deaths[actor]++; Check(!runner.IsServer || actor.WaitingForRespawn, "death-hook"); };
                actor.RespawnCountdownChanged += seconds => { if (seconds < 0) Check(false, "countdown-nonnegative"); };
                actor.Respawned += () =>
                {
                    revivals[actor]++;
                    Check(!actor.WaitingForRespawn, "respawn-hook");
                    if (!runner.IsServer) return;
                    var stats = actor.GetComponent<CharacterStats>();
                    Check(stats.CurrentHealth == stats.MaxHealth && stats.CurrentMana == stats.MaxMana
                        && stats.CurrentStamina == stats.MaxStamina, "full-resource-restore");
                    Check(actor.IsSpawnProtected && stats.ApplyDamage(9999) == 0, "spawn-protection");
                    var player = actor.GetComponent<NetworkTestPlayer>();
                    Check(GetComponent<TeamSpawnService>().GetArea(player.Team).IsSpawnPoint(actor.transform.position), "own-fortress-position");
                    Check(runner.TryGetPlayerObject(actor.Object.InputAuthority, out var same) && same == actor.Object, "same-avatar-and-owner");
                    var old = revisions[actor];
                    Check(old[0] == actor.GetComponent<Inventory>().Revision
                        && old[1] == actor.GetComponent<Equipment>().Revision
                        && old[2] == actor.GetComponent<SessionProgression>().Revision, "session-state-preserved");
                };
            }
            if (readyAt == 0) readyAt = Time.realtimeSinceStartup;
            if (!runner.IsServer)
            {
                if (!prepared) { prepared = true; Check(actors.All(a => a.GetComponent<CharacterStats>().ApplyDamage(9999) == 0), "client-damage-rejected"); }
                if (!sawReplicaRestore && actors.All(a => a.RespawnSequence == 1 && a.IsSpawnProtected))
                {
                    sawReplicaRestore = true;
                    Check(actors.All(a => a.GetComponent<NetworkCharacterStats>().Health == a.GetComponent<NetworkCharacterStats>().MaxHealth), "replicated-health-restore");
                    var owner = actors.First(a => a.HasInputAuthority).GetComponent<NetworkPlayerResources>();
                    Check(owner.Mana == owner.MaxMana && owner.Stamina == owner.MaxStamina, "owner-resource-restore");
                }
                if (match.MatchEnded && finishedAt == 0) finishedAt = Time.realtimeSinceStartup;
                if (match.MatchEnded && actors.All(a => !a.WaitingForRespawn && a.GetComponent<NetworkCharacterStats>().Dead))
                {
                    // The probe's Update may precede one replica's Render notification.
                    if (Time.realtimeSinceStartup - finishedAt < 1) return;
                    Check(sawReplicaRestore && actors.All(a => a.RespawnSequence == 2 && revivals[a] == 2 && deaths[a] == 3), "replicated-two-cycles-and-cancel");
                    Debug.Log("[RespawnComplete] server=False"); active = false;
                }
                return;
            }
            if (!prepared && Time.realtimeSinceStartup - readyAt > 3)
            {
                prepared = true;
                foreach (var actor in actors)
                {
                    var inv = actor.GetComponent<Inventory>();
                    Check(inv.TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "seed-inventory");
                    Check(actor.GetComponent<Equipment>().TryEquip(0, EquipmentSlot.MainHand, out _), "seed-equipment");
                    Check(inv.TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "retained-inventory-item");
                    var progression = actor.GetComponent<SessionProgression>();
                    Check(actor.GetComponent<NetworkSessionProgression>().TryGrantExperience(1000, "respawn-test"), "seed-level");
                    Check(progression.TryIncreaseAttribute(ItemStat.Vitality, progression.SessionId, out _), "seed-attribute");
                    revisions[actor] = new[] { inv.Revision, actor.GetComponent<Equipment>().Revision, progression.Revision };
                }
            }
            if (!prepared) return;
            if (!killed && actors.All(a => !a.IsSpawnProtected && !a.WaitingForRespawn))
            {
                killed = true;
                foreach (var actor in actors)
                {
                    var stats = actor.GetComponent<CharacterStats>();
                    stats.TrySpendMana(25); stats.TrySpendStamina(25);
                    Check(stats.ApplyDamage(99999) > 0 && actor.WaitingForRespawn && stats.IsDead, "server-death-scheduled");
                }
            }
            if (killed && actors.All(a => a.RespawnSequence == 1 && !a.WaitingForRespawn)) killed = false;
            if (!finished && actors.All(a => a.RespawnSequence == 2 && !a.IsSpawnProtected))
            {
                finished = true; finishedAt = Time.realtimeSinceStartup;
                foreach (var actor in actors) Check(actor.GetComponent<CharacterStats>().ApplyDamage(99999) > 0, "third-death");
                FindObjectsByType<NetworkKingObjective>().First().Stats.ApplyDamage(99999);
                Check(match.MatchEnded, "end-match-with-pending-respawn");
            }
            if (finished && Time.realtimeSinceStartup - finishedAt > 6)
            {
                Check(actors.All(a => a.RespawnSequence == 2 && !a.WaitingForRespawn && a.GetComponent<CharacterStats>().IsDead), "no-respawn-after-match-end");
                Debug.Log("[RespawnComplete] server=True"); active = false;
            }
        }
#endif
    }
}
