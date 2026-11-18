#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Observes server events; owns and releases every subscription at round boundaries.
    public sealed class MatchSimulationDiagnostics : IDisposable
    {
        private readonly MultiplayerBootstrap bootstrap;
        private readonly Action<string, string, NetworkTestPlayer> emit;
        private readonly List<Action> release = new();
        private readonly HashSet<EntityId> observed = new();
        private readonly HashSet<EntityId> dead = new();
        private readonly Dictionary<EntityId, MatchSimulationStall> stalls = new();
        private readonly Dictionary<EntityId, int> levels = new();
        private readonly HashSet<EntityId> invalid = new();
        private readonly MatchSimulationLootLedger loot = new();
        private readonly ConcurrentQueue<(string message, LogType type)> logs = new();
        private int queuedLogs, droppedLogs;
        public MatchSimulationCounters Total { get; } = new();
        public MatchSimulationCounters Round { get; private set; } = new();
        public MatchSimulationDiagnostics(MultiplayerBootstrap owner, Action<string, string, NetworkTestPlayer> sink)
        {
            bootstrap = owner; emit = sink;
            Application.logMessageReceivedThreaded += Log;
            MatchSimulationLootAudit.Recorded += Loot;
            bootstrap.ConnectionStateChanged += ConnectionChanged;
        }
        private void ConnectionChanged(NetworkConnectionState state)
        {
            bool failed = state is NetworkConnectionState.FailedJoin or NetworkConnectionState.SessionFull
                or NetworkConnectionState.SessionClosed or NetworkConnectionState.SessionNotFound or NetworkConnectionState.JoinRejected
                or NetworkConnectionState.Interrupted or NetworkConnectionState.ConnectionLost or NetworkConnectionState.ReconnectExpired;
            if (failed) { Total.networkErrors++; Round.networkErrors++; }
            emit(failed ? "network-failure" : "network-state", state + ": " + bootstrap.NetworkMessage, null);
        }
        private void Log(string condition, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (System.Threading.Interlocked.Increment(ref queuedLogs) > 256)
            { System.Threading.Interlocked.Decrement(ref queuedLogs); System.Threading.Interlocked.Increment(ref droppedLogs); return; }
            string message = condition + "\n" + trace;
            logs.Enqueue((message.Length > 8192 ? message.Substring(0, 8192) : message, type));
        }
        private void Loot(string id, long amount, string owner, bool claim)
        {
            bool accepted = claim ? loot.Claim(id, amount) : loot.RegisterRoll(id, amount);
            if (!accepted) { Total.duplicateLoot++; Round.duplicateLoot++; emit("duplicate-loot", id + " count=" + amount + " owner=" + owner, null); }
            else if (claim) { Total.itemsClaimed += amount; Round.itemsClaimed += amount; }
            else { Total.itemsGenerated += amount; Round.itemsGenerated += amount; }
            emit(claim ? "loot-claim" : "loot-roll", id + " count=" + amount + " owner=" + owner, null);
        }
        public void BeginRound()
        {
            ClearSubscriptions(); loot.Clear(); Round = new MatchSimulationCounters();
            Total.started++; Round.started = 1;
        }
        public void Finished(bool success)
        {
            if (success) { Total.completed++; Round.completed++; }
            else { Total.failed++; Round.failed++; }
        }
        public void Sample(double now, float threshold, bool playing)
        {
            DrainLogs();
            SampleActors(now, threshold, playing);
        }
        public void DrainLogs()
        {
            while (logs.TryDequeue(out var log))
            {
                System.Threading.Interlocked.Decrement(ref queuedLogs);
                Total.exceptions++; Round.exceptions++;
                if (log.message.IndexOf("Fusion", StringComparison.OrdinalIgnoreCase) >= 0)
                { Total.networkErrors++; Round.networkErrors++; }
                emit("error", log.type + ": " + log.message, null);
            }
            int dropped = System.Threading.Interlocked.Exchange(ref droppedLogs, 0);
            if (dropped > 0)
            {
                Total.droppedLogs += dropped; Round.droppedLogs += dropped;
                emit("log-overflow", "Dropped " + dropped + " messages; diagnosis incomplete", null);
            }
        }
        private void SampleActors(double now, float threshold, bool playing)
        {
            foreach (var actor in bootstrap.Avatars.ToArray())
            {
                var player = actor.GetComponent<NetworkTestPlayer>();
                var id = player.GetEntityId();
                var stats = player.GetComponent<CharacterStats>();
                if (!stats.IsDead) dead.Remove(id);
                var progression = actor.GetComponent<NetworkSessionProgression>();
                if (progression != null)
                {
                    Total.highestLevel = Math.Max(Total.highestLevel, progression.Level);
                    Round.highestLevel = Math.Max(Round.highestLevel, progression.Level);
                    if (!levels.TryGetValue(id, out int level) || level != progression.Level)
                    { levels[id] = progression.Level; emit("level", progression.Level.ToString(), player); }
                }
                if (observed.Add(id))
                {
                    var receiver = actor.GetComponent<DamageReceiver>();
                    Action<float, GameObject> damage = (amount, source) =>
                    {
                        if (!stats.IsDead || !dead.Add(id)) return;
                        Total.deaths++; Round.deaths++; emit("death", "Damage source=" + (source != null ? source.name : "none"), player);
                        var killer = source != null ? source.GetComponentInParent<NetworkTestPlayer>() : null;
                        if (killer != null)
                        {
                            Total.kills++; Round.kills++; Total.playerKills++; Round.playerKills++;
                            emit("kill", "victim=" + player.Object.Id + " type=player", killer);
                        }
                    };
                    if (receiver != null) { receiver.DamageTaken += damage; release.Add(() => { if (receiver != null) receiver.DamageTaken -= damage; }); }
                    stalls[id] = new MatchSimulationStall();
                }
                ValidateInventory(player);
                var brain = player.BotBrain;
                bool eligible = playing && !stats.IsDead && brain != null && brain.WantsTravel
                    && !player.GetComponent<NetworkPlayerCombat>().IsBusy;
                if (stalls[id].Observe(player.transform.position, eligible, now, threshold))
                {
                    Total.stuckAI++; Round.stuckAI++;
                    emit("stuck-ai", "destination=" + brain.Destination + " path=" + brain.HasNavigationPath, player);
                }
            }
            foreach (var receiver in UnityEngine.Object.FindObjectsByType<DamageReceiver>(FindObjectsSortMode.None))
            {
                var actor = receiver.GetComponent<Fusion.NetworkObject>();
                if (actor == null || !actor.IsValid || actor.Runner != bootstrap.Runner || receiver.GetComponent<NetworkTestPlayer>() != null
                    || !observed.Add(receiver.GetEntityId())) continue;
                var stats = receiver.GetComponent<CharacterStats>();
                bool gate = receiver.GetComponent<NetworkFortressGate>() != null;
                bool king = receiver.GetComponent<NetworkKingObjective>() != null;
                bool killed = false;
                Action<float, GameObject> damage = (amount, source) =>
                {
                    if (killed || !stats.IsDead) return;
                    killed = true;
                    if (gate) { Total.gatesDestroyed++; Round.gatesDestroyed++; }
                    if (king) { Total.kingsKilled++; Round.kingsKilled++; }
                    var killer = source != null ? source.GetComponentInParent<NetworkTestPlayer>() : null;
                    if (killer != null) { Total.kills++; Round.kills++; emit("kill", "victim=" + actor.Id + " type=" + actor.name, killer); }
                };
                receiver.DamageTaken += damage; release.Add(() => { if (receiver != null) receiver.DamageTaken -= damage; });
            }
            foreach (var camp in UnityEngine.Object.FindObjectsByType<NetworkCamp>(FindObjectsSortMode.None))
            {
                if (camp.Object == null || !camp.Object.IsValid || camp.Runner != bootstrap.Runner || !observed.Add(camp.GetEntityId())) continue;
                Action<NetworkCamp> cleared = c => { Total.campsCleared++; Round.campsCleared++; emit("camp-cleared", c.Object.Id.ToString(), null); };
                camp.Cleared += cleared; release.Add(() => { if (camp != null) camp.Cleared -= cleared; });
            }
        }
        private void ValidateInventory(NetworkTestPlayer player)
        {
            var inventory = player.GetComponent<Inventory>();
            bool valid = inventory != null && inventory.SlotCount > 0 && inventory.SlotCount <= NetworkInventory.Capacity;
            if (inventory != null) for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (string.IsNullOrEmpty(slot.ItemId)) { if (slot.Amount != 0) valid = false; }
                else if (!inventory.TryResolveItem(slot.ItemId, out var item) || slot.Amount <= 0 || slot.Amount > item.MaximumStackSize) valid = false;
            }
            var equipment = player.GetComponent<HeroesOfDragonland.EquipmentSystem.Equipment>();
            if (equipment != null && inventory != null) for (int i = 1; i <= 6; i++)
            {
                var slot = (HeroesOfDragonland.Items.EquipmentSlot)i;
                string id = equipment.GetItemId(slot);
                if (!string.IsNullOrEmpty(id) && (!inventory.TryResolveItem(id, out var item) || item.EquipmentSlot != slot)) valid = false;
            }
            if (!valid && invalid.Add(player.GetEntityId())) { Total.invalidInventories++; Round.invalidInventories++; emit("invalid-inventory", "Malformed authoritative inventory/equipment snapshot", player); }
            if (valid) invalid.Remove(player.GetEntityId());
        }
        private void ClearSubscriptions()
        {
            foreach (var unsubscribe in release) unsubscribe();
            release.Clear(); observed.Clear(); stalls.Clear(); dead.Clear(); levels.Clear(); invalid.Clear();
        }
        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= Log;
            MatchSimulationLootAudit.Recorded -= Loot;
            bootstrap.ConnectionStateChanged -= ConnectionChanged;
            ClearSubscriptions();
        }
    }
}
#endif
