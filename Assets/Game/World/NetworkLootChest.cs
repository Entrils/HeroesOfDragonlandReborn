using Fusion;
using System;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(LootChest), typeof(NetworkObject))]
    public sealed class NetworkLootChest : NetworkBehaviour, ILootChestAuthority
    {
        [Networked] public NetworkId CampId { get; private set; }
        [Networked] public NetworkBool IsOpen { get; private set; }
        [Networked] public PlayerRef OpenedBy { get; private set; }
        public event Action<bool, string> OpenResolved;
        private LootChest chest;
        public bool CanMutateChest => isActiveAndEnabled && Object != null && Object.IsValid
            && Runner.IsServer && HasStateAuthority && Object.InputAuthority == PlayerRef.None && NetworkMatch.AllowsGameplay(Runner);
        private void Awake() => chest = GetComponent<LootChest>();
        // onBeforeSpawned runs before the object's valid/spawned lifecycle is complete.
        internal void Initialize(NetworkId campId) { if (Runner.IsServer) CampId = campId; }
        public override void Spawned()
        {
            if (Runner.IsServer && HasStateAuthority) chest.Changed += Publish;
            else chest.ApplyReplicatedOpen(IsOpen);
        }
        private void Publish() { if (CanMutateChest) IsOpen = chest.IsOpen; }
        public bool AllowsClaimant(Inventory inventory)
        {
            var owner = inventory != null ? inventory.GetComponent<NetworkInventory>() : null;
            return CanMutateChest && owner != null && owner.Runner == Runner && owner.CanMutateInventory;
        }
        // Intent contains no recipient, loot table, seed or rewards. Resolve the connection on the server.
        public bool RequestOpen()
        {
            if (!isActiveAndEnabled || Object == null || !Object.IsValid || Runner.LocalPlayer == PlayerRef.None) return false;
            RPC_Open();
            return true;
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_Open(RpcInfo info = default)
        {
            if (!CanMutateChest || !Runner.TryGetPlayerObject(info.Source, out var player) || player == null) return;
            var inventory = player.GetComponent<NetworkInventory>();
            if (inventory == null || !inventory.AdmitRequest(info.Source)) return;
            bool accepted = TryOpenFor(info.Source, out string message);
            RPC_OpenResult(info.Source, accepted, message);
        }
        internal bool TryOpenFor(PlayerRef sender, out string message)
        {
            message = "Chest is unavailable.";
            if (!CanMutateChest || sender == PlayerRef.None
                || !Runner.TryGetPlayerObject(sender, out var player) || player == null) return false;
            if (IsOpen || chest.IsOpen) { message = "Chest has already been looted."; return false; }
            // No team ownership restriction: first accepted server request wins.
            bool accepted = chest.TryInteract(player.GetComponent<Inventory>(), out message);
            if (accepted) OpenedBy = sender;
            return accepted;
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OpenResult([RpcTarget] PlayerRef recipient, bool accepted, string message)
            => OpenResolved?.Invoke(accepted, message);
        public override void Render() { if (!HasStateAuthority) chest.ApplyReplicatedOpen(IsOpen); }
        public override void Despawned(NetworkRunner runner, bool hasState) => chest.Changed -= Publish;
    }
}
