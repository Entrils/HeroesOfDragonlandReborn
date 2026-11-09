using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.World;
using HeroesOfDragonland.Stats;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Opt-in standalone acceptance fixtures; never active in normal gameplay.
    public sealed class MultiplayerLootProbe : MonoBehaviour
    {
        [SerializeField] private NetworkLootChest chestPrefab;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerBootstrap bootstrap;
        private bool active, prepared, requested, complete;
        private NetworkLootChest race;
        private float readyAt, nextLog;
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            active = Environment.GetCommandLineArgs().Contains("-hodrLoot");
        }
        private void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("[LootCheck] FAIL " + label);
            Debug.Log("[LootCheck] PASS " + label);
        }
        private NetworkLootChest Spawn(NetworkRunner runner, Vector3? position = null)
        {
            var spawned = runner.Spawn(chestPrefab, position ?? new Vector3(0, 1, 0), Quaternion.identity, PlayerRef.None);
            Physics.SyncTransforms();
            return spawned;
        }
        private void Update()
        {
            var runner = bootstrap.Runner;
            if (!active || runner == null || !runner.IsRunning) return;
            if (runner.IsServer && !prepared && runner.ActivePlayers.Count() == 2)
            {
                var players = runner.ActivePlayers.Select(p => { runner.TryGetPlayerObject(p, out var obj); return obj; }).ToArray();
                if (players.Any(p => p == null || !p.IsValid)) return;
                prepared = true;
                var a = players[0]; var b = players[1];
                Vector3 remoteSpawn = b.transform.position;
                Check(a.GetComponent<NetworkTestPlayer>().Team != b.GetComponent<NetworkTestPlayer>().Team, "opposing-teams");
                foreach (var player in players) player.GetComponent<Inventory>().Clear();
                var chest = Spawn(runner);
                Check(!chest.TryOpenFor(a.InputAuthority, out _) && !chest.IsOpen, "server-distance-rejection");
                Check(!chest.TryOpenFor(PlayerRef.None, out _) && !chest.IsOpen, "unregistered-rejection");
                a.transform.position = new Vector3(-1.5f, 1, 0);
                b.transform.position = new Vector3(1.5f, 1, 0);
                Physics.SyncTransforms();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(-0.75f, 1.5f, 0);
                wall.transform.localScale = new Vector3(0.1f, 4, 4);
                Physics.SyncTransforms();
                Check(!chest.TryOpenFor(a.InputAuthority, out _) && !chest.IsOpen, "server-obstruction-rejection");
                wall.SetActive(false); Destroy(wall); Physics.SyncTransforms();
                var stats = a.GetComponent<CharacterStats>();
                stats.ApplyDamage(stats.MaxHealth);
                Check(stats.IsDead && !chest.TryOpenFor(a.InputAuthority, out _) && !chest.IsOpen, "dead-player-rejection");
                stats.ResetToFull();
                var inv = a.GetComponent<Inventory>();
                for (int i = 0; i < inv.SlotCount; i++) Check(inv.TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "fill-slot");
                long revision = inv.Revision;
                Check(!chest.TryOpenFor(a.InputAuthority, out _) && !chest.IsOpen && inv.Revision == revision, "full-inventory-atomic-rejection");
                var roll = chest.GetComponent<LootChest>().PendingRewards.ToArray();
                Check(roll.Length > 0, "server-roll-retained");
                Check(chest.TryOpenFor(b.InputAuthority, out _) && chest.OpenedBy == b.InputAuthority, "team-b-can-win");
                foreach (var reward in roll.GroupBy(r => r.ItemId))
                    Check(b.GetComponent<Inventory>().GetAmount(reward.Key) == reward.Sum(r => r.Amount), "winner-receives-exact-retained-roll");
                Check(!chest.TryOpenFor(a.InputAuthority, out _) && !chest.TryOpenFor(b.InputAuthority, out _), "both-repeat-requests-rejected");
                foreach (var collider in chest.GetComponentsInChildren<Collider>()) collider.enabled = false;
                runner.Despawn(chest.Object);
                inv.Clear(); b.GetComponent<Inventory>().Clear();
                chest = Spawn(runner);
                bool reentered = false;
                inv.Changed += OnDeposit;
                void OnDeposit(InventorySnapshot snapshot)
                {
                    reentered = true;
                    Check(chest.IsOpen && !chest.TryOpenFor(b.InputAuthority, out _), "reentrant-competitor-rejected");
                }
                bool opened = chest.TryOpenFor(a.InputAuthority, out string reason);
                Check(opened && chest.OpenedBy == a.InputAuthority && reentered, "team-a-can-win " + reason);
                inv.Changed -= OnDeposit;
                foreach (var collider in chest.GetComponentsInChildren<Collider>()) collider.enabled = false;
                runner.Despawn(chest.Object);
                inv.Clear(); b.GetComponent<Inventory>().Clear();
                // The earlier direct checks use temporary transforms within this frame.
                // Fusion restores avatar simulation positions next tick, so place the
                // RPC fixture beside the remote avatar's actual authoritative spawn.
                race = Spawn(runner, remoteSpawn + Vector3.left * 1.5f);
                Debug.Log("[LootFixture] ready");
            }
            if (race == null)
                race = FindObjectsByType<NetworkLootChest>().FirstOrDefault(c => c.Object != null && c.Object.IsValid && c.Runner == runner);
            if (race == null) return;
            if (readyAt == 0) readyAt = Time.realtimeSinceStartup;
            if (!runner.IsServer && !requested && Time.realtimeSinceStartup - readyAt > 2)
            {
                requested = true;
                runner.TryGetPlayerObject(runner.LocalPlayer, out var player);
                var chest = race.GetComponent<LootChest>();
                chest.SetAuthority(true);
                Check(!chest.TryInteract(player.GetComponent<Inventory>(), out _) && !chest.HasAuthority, "client-direct-mutation-rejected");
                Check(!player.GetComponent<Inventory>().TryAdd("b4d4a0612c1acd74d98f66ef3969959f"), "client-item-add-rejected");
                race.OpenResolved += (ok, message) => Debug.Log("[LootResponse] accepted=" + ok + " message=" + message);
                Check(race.RequestOpen(), "remote-rpc-submitted");
                race.RequestOpen(); // Duplicate delivery intent must never duplicate the reward.
            }
            // Give the remote request priority for this fixture; same-tick contention was
            // exercised reentrantly above. Host retries must preserve the winner's grant.
            if (runner.IsServer && race.IsOpen && !complete)
            {
                complete = true;
                var winner = race.OpenedBy;
                Check(winner != runner.LocalPlayer, "remote-rpc-winner");
                runner.TryGetPlayerObject(winner, out var player);
                long revision = player.GetComponent<Inventory>().Revision;
                Check(!race.TryOpenFor(runner.LocalPlayer, out _) && !race.TryOpenFor(winner, out _)
                    && revision == player.GetComponent<Inventory>().Revision, "post-rpc-double-open-rejected");
                Debug.Log("[LootComplete] server");
            }
            if (Time.realtimeSinceStartup > nextLog && race.IsOpen)
            {
                nextLog = Time.realtimeSinceStartup + 1;
                runner.TryGetPlayerObject(runner.LocalPlayer, out var player);
                var inv = player.GetComponent<Inventory>();
                long items = inv.CaptureSnapshot().Slots.Sum(s => (long)s.Amount);
                Debug.Log($"[LootState] server={runner.IsServer} id={race.Object.Id} open={race.IsOpen} mirror={race.GetComponent<LootChest>().IsOpen} winner={race.OpenedBy} items={items}");
                if (!runner.IsServer && race.GetComponent<LootChest>().IsOpen && items > 0 && !complete)
                { complete = true; Debug.Log("[LootComplete] client"); }
            }
        }
#endif
    }
}

