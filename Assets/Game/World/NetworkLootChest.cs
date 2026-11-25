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
        [Networked] public NetworkId OpenedByBot { get; private set; }
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
            if (!CanMutateChest) return;
            if (!NetworkParticipant.TryResolveSender(Runner, info.Source, out var player))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.Sender);
                return;
            }
            var inventory = player.GetComponent<NetworkInventory>();
            if (inventory == null || !inventory.AdmitRequest(info.Source)) return;
            bool accepted = TryOpenFor(info.Source, out string message);
            // Allow for a normal late reach rejection before logging markedly distant requests.
            float auditRange = chest.InteractionRange + 1.5f;
            if (!accepted && (chest.InteractionPoint - player.transform.position).sqrMagnitude > auditRange * auditRange)
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.ChestReach);
            RPC_OpenResult(info.Source, accepted, message);
        }
        internal bool TryOpenFor(PlayerRef sender, out string message)
        {
            message = "Chest is unavailable.";
            if (!CanMutateChest || !NetworkParticipant.TryResolveSender(Runner, sender, out var player)) return false;
            if (IsOpen || chest.IsOpen) { message = "Chest has already been looted."; return false; }
            // No team ownership restriction: first accepted server request wins.
            bool accepted = chest.TryInteract(player.GetComponent<Inventory>(), out message);
            if (accepted)
            {
                OpenedBy = sender;
                MatchSessionData.Credit(player, new MatchStatistics { ChestsOpened = 1 });
            }
            return accepted;
        }
#if UNITY_EDITOR || DEBUG
        internal bool TryOpenForBot(NetworkObject actor, out string message)
        {
            message = "Chest is unavailable.";
            if (!CanMutateChest || actor == null || actor.Runner != Runner || actor.InputAuthority != PlayerRef.None
                || !NetworkParticipant.IsAdmitted(actor) || !actor.GetComponent<NetworkTestPlayer>().IsBot || IsOpen || chest.IsOpen) return false;
            bool accepted = chest.TryInteract(actor.GetComponent<Inventory>(), out message);
            if (accepted)
            {
                OpenedByBot = actor.Id;
                MatchSessionData.Credit(actor, new MatchStatistics { ChestsOpened = 1 });
            }
            return accepted;
        }
#endif
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OpenResult([RpcTarget] PlayerRef recipient, bool accepted, string message)
            => OpenResolved?.Invoke(accepted, message);
        public override void Render() { if (!HasStateAuthority) chest.ApplyReplicatedOpen(IsOpen); }
        public override void Despawned(NetworkRunner runner, bool hasState) => chest.Changed -= Publish;
    }
}
