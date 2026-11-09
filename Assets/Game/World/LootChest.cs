using System;
using System.Collections.Generic;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Interaction;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.World
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class LootChest : MonoBehaviour, IInteractable
    {
        [SerializeField] private LootTable lootTable;
        [SerializeField, Min(0.1f)] private float interactionRange = 3f;
        [SerializeField] private bool hasAuthority = true;
        private Inventory claimant;
        private InventorySlot[] pending = Array.Empty<InventorySlot>();
        private bool processing;
        private bool networkLootRolled;
        public bool IsOpen { get; private set; }
        public long Revision { get; private set; }
        public bool HasAuthority => GetComponent<ILootChestAuthority>()?.CanMutateChest ?? hasAuthority;
        public LootTable Table => lootTable;
        public float InteractionRange => interactionRange;
        // Factories configure inactive, unopened instances; never replaces rolled rewards.
        public bool TryConfigureSpawn(LootTable table, bool authority)
        {
            if (gameObject.activeInHierarchy || IsOpen || Revision != 0 || processing || table == null) return false;
            lootTable = table;
            hasAuthority = authority;
            return true;
        }
        public IReadOnlyList<InventorySlot> PendingRewards => Array.AsReadOnly(pending);
        public Vector3 InteractionPoint => GetComponent<BoxCollider>().bounds.center;
        public event Action Changed;
        public void SetAuthority(bool authority) => hasAuthority = authority;

        internal void ApplyReplicatedOpen(bool value)
        {
            if (GetComponent<ILootChestAuthority>() == null || HasAuthority || IsOpen == value) return;
            IsOpen = value;
            Changed?.Invoke();
        }

        public bool TryInteract(Inventory player, out string message)
        {
            message = null;
            if (!isActiveAndEnabled || processing) message = "Chest is unavailable.";
            else if (!HasAuthority || player == null || !player.HasAuthority
                || (GetComponent<ILootChestAuthority>() is ILootChestAuthority policy && !policy.AllowsClaimant(player)))
                message = "Only authority can grant chest loot.";
            else if (!player.isActiveAndEnabled || (player.TryGetComponent<CharacterStats>(out var stats) && stats.IsDead)) message = "Player cannot loot now.";
            else if (!InteractionReach.CanReach(player.transform, transform, InteractionPoint, interactionRange)) message = "Chest is out of reach or obstructed.";
            else if (IsOpen && pending.Length == 0) message = "Chest has already been looted.";
            else if (IsOpen && claimant != player) message = "Remaining rewards belong to the player who opened this chest.";
            if (message != null) return false;
            processing = true;
            try
            {
                // Network chests transfer the complete roll once. Capacity failures keep
                // the same server-only roll available to either team, without claiming it.
                if (GetComponent<ILootChestAuthority>() != null)
                {
                    if (IsOpen) { message = "Chest has already been looted."; return false; }
                    if (lootTable == null) { message = "Chest has no loot table."; return false; }
                    if (!lootTable.TryValidate(player.Catalog, out message)) return false;
                    if (!networkLootRolled)
                    {
                        if (!lootTable.TryRoll(new System.Random(Guid.NewGuid().GetHashCode()), out pending, out message)) return false;
                        networkLootRolled = true;
                    }
                    if (pending.Length == 0)
                    {
                        claimant = player;
                        IsOpen = true;
                        Revision++;
                        Changed?.Invoke();
                        message = "Chest opened. No loot.";
                        return true;
                    }
                    bool granted = player.TryDeposit(pending, remainder =>
                    {
                        pending = remainder;
                        claimant = player;
                        IsOpen = true;
                        Revision++;
                        // Commit replicated state before inventory observers can reenter.
                        Changed?.Invoke();
                    }, requireAll: true);
                    message = granted ? "Chest looted." : "Make room for all chest rewards and try again.";
                    return granted;
                }
                bool openedNow = false;
                if (!IsOpen)
                {
                    if (lootTable == null) { message = "Chest has no loot table."; return false; }
                    if (!lootTable.TryValidate(player.Catalog, out message)) return false;
                    // Server-side randomness boundary: never accept a seed/reward list from a client.
                    if (!lootTable.TryRoll(new System.Random(Guid.NewGuid().GetHashCode()), out var rolled, out message)) return false;
                    pending = rolled;
                    claimant = player;
                    IsOpen = true;
                    openedNow = true;
                }
                bool collected = player.TryDeposit(pending, remainder => { pending = remainder; Revision++; });
                if (openedNow && !collected) Revision++;
                if (openedNow || collected) Changed?.Invoke();
                message = pending.Length == 0 ? "Chest looted." : "Inventory full. Free space and interact again to collect remaining rewards.";
                return openedNow || collected;
            }
            finally { processing = false; }
        }
    }
}
