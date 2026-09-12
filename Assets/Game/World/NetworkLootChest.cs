using Fusion;
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
        private LootChest chest;
        public bool CanMutateChest => isActiveAndEnabled && Object != null && Object.IsValid
            && Runner.IsServer && HasStateAuthority && Object.InputAuthority == PlayerRef.None;
        private void Awake() => chest = GetComponent<LootChest>();
        // onBeforeSpawned runs before the object's valid/spawned lifecycle is complete.
        internal void Initialize(NetworkId campId) { if (Runner.IsServer) CampId = campId; }
        public override void Spawned() { if (CanMutateChest) chest.Changed += Publish; }
        private void Publish() { if (CanMutateChest) IsOpen = chest.IsOpen; }
        public bool AllowsClaimant(Inventory inventory)
        {
            var owner = inventory != null ? inventory.GetComponent<NetworkInventory>() : null;
            return CanMutateChest && owner != null && owner.Runner == Runner && owner.CanMutateInventory;
        }
        // Intent contains no recipient, loot table, seed or rewards. Resolve the connection on the server.
        public bool RequestOpen()
        {
            if (Object == null || !Object.IsValid || Runner.LocalPlayer == PlayerRef.None) return false;
            RPC_Open();
            return true;
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_Open(RpcInfo info = default)
        {
            if (!CanMutateChest || !Runner.TryGetPlayerObject(info.Source, out var player) || player == null) return;
            var inventory = player.GetComponent<NetworkInventory>();
            if (inventory == null || !inventory.AdmitRequest(info.Source)) return;
            chest.TryInteract(player.GetComponent<Inventory>(), out _);
        }
        public override void Render() { if (!HasStateAuthority) chest.ApplyReplicatedOpen(IsOpen); }
        public override void Despawned(NetworkRunner runner, bool hasState) => chest.Changed -= Publish;
    }
}
