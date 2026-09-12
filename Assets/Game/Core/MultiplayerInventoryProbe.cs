using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Enabled only by a development command-line flag; no item grant RPC exists.
    public sealed class MultiplayerInventoryProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private ItemDefinition sword;
        [SerializeField] private ItemDefinition potion;
        [SerializeField] private ItemDefinition dragon;
        [SerializeField] private ItemDefinition armor;
        private NetworkInventory network;
        private NetworkEquipment networkEquipment;
        private NetworkTestPlayer player;
        private Inventory inventory;
        private Equipment equipment;
        private EquipmentVisualHooks visuals;
        private bool active, seeded, tamperChecked, subscribed, pending, complete;
        private int step, visualEvents, responses;
        private float nextAction, nextReport;
        private long oldInventory, oldEquipment;
        private void Awake()
        {
            active = Environment.GetCommandLineArgs().Contains("-hodrInventory");
            network = GetComponent<NetworkInventory>();
            networkEquipment = GetComponent<NetworkEquipment>();
            player = GetComponent<NetworkTestPlayer>();
            inventory = GetComponent<Inventory>();
            equipment = GetComponent<Equipment>();
            visuals = GetComponent<EquipmentVisualHooks>();
        }
        private void Check(bool ok, string label)
        {
            if (!ok) { Debug.LogError("[InventoryCheck] FAIL participant=" + player.ParticipantNumber + " " + label); enabled = false; }
            else Debug.Log("[InventoryCheck] PASS participant=" + player.ParticipantNumber + " " + label);
        }
        private int Find(ItemDefinition item)
        {
            for (int i = 0; i < inventory.SlotCount; i++) if (inventory.GetSlot(i).ItemId == item.Id) return i;
            return -1;
        }
        private void Resolved(bool accepted, string reason)
        {
            responses++;
            if (!pending) { Check(false, "unexpected duplicate response"); return; }
            bool expected = step == 4 || step == 5 || step == 6 || step == 7 || step == 8;
            Check(accepted == expected, "request=" + step + " accepted=" + accepted + " reason=" + reason);
            if (step == 1) Check(reason.Contains("Strength"), "server stat requirement reason");
            if (step == 2) Check(reason.Contains("cannot be equipped"), "server slot requirement reason");
            if (step == 13) Check(reason.Contains("stale"), "stale revision reason");
            pending = false;
            step++;
            nextAction = Time.realtimeSinceStartup + 0.5f;
        }
        private void Update()
        {
            if (!active || network.Object == null || !network.Object.IsValid) return;
            if (!subscribed)
            {
                subscribed = true;
                visuals.VisualChanged += OnVisual;
                networkEquipment.RequestResolved += Resolved;
                network.RemoveResolved += Resolved;
            }
            if (network.IsAdmitted && !seeded)
            {
                seeded = true;
                Check(!network.AdmitRequest(PlayerRef.None), "unowned sender rejected");
                long revision = inventory.Revision;
                Check(!network.TryAddItem("unknown", 1) && !network.TryAddItem(potion.Id, 0)
                    && !network.TryAddItem(potion.Id, -1) && !network.TryRemoveItem(potion.Id, 1)
                    && inventory.Revision == revision, "invalid server add/remove atomic");
                if (player.ParticipantNumber <= 2)
                {
                    Check(network.TryAddItem(potion.Id, inventory.SlotCount * potion.MaximumStackSize)
                        && !network.TryAddItem(sword.Id) && inventory.Clear(), "capacity enforced");
                    Check(network.TryAddItem(potion.Id, 18) && network.TryRemoveItem(potion.Id, 1)
                        && network.TryAddItem(sword.Id) && network.TryAddItem(dragon.Id)
                        && network.TryAddItem(armor.Id), "server seed and remove");
                }
            }
            if (!network.HasStateAuthority && !tamperChecked && networkEquipment.SnapshotRevision > 0)
            {
                tamperChecked = true;
                long revision = inventory.Revision;
                inventory.SetAuthority(true);
                var forged = new InventorySlot[inventory.SlotCount]; forged[0] = new InventorySlot(sword.Id, 1);
                Check(!inventory.HasAuthority && !inventory.TryAdd(sword.Id) && !inventory.TryRemove(potion.Id)
                    && !inventory.TryRemoveAt(0, 1) && !inventory.Clear()
                    && !inventory.TryApplySnapshot(new InventorySnapshot(999999, forged))
                    && !network.TryAddItem(sword.Id) && !network.TryRemoveItem(potion.Id)
                    && !equipment.TryEquip(0, EquipmentSlot.MainHand, out _) && inventory.Revision == revision,
                    "client mutation paths blocked");
                if (!network.HasInputAuthority)
                    Check(!network.RequestRemove(0, 1) && !networkEquipment.RequestEquip(0, EquipmentSlot.MainHand),
                        "remote object requests blocked");
            }
            if (network.HasInputAuthority && player.ParticipantNumber <= 2 && !complete
                && !pending && Time.realtimeSinceStartup >= nextAction && network.SnapshotRevision > 1
                && inventory.GetAmount(dragon.Id) == 1)
            {
                if (step == 0) { oldInventory = network.SnapshotRevision; oldEquipment = networkEquipment.SnapshotRevision; }
                pending = true;
                switch (step)
                {
                    case 0: networkEquipment.RequestEquip(999, EquipmentSlot.MainHand); break;
                    case 1: networkEquipment.RequestEquip(Find(dragon), EquipmentSlot.MainHand); break;
                    case 2: networkEquipment.RequestEquip(Find(sword), EquipmentSlot.Head); break;
                    case 3: networkEquipment.RequestEquip(Find(potion), EquipmentSlot.MainHand); break;
                    case 4:
                        for (int i = 0; i < 10; i++) networkEquipment.RequestEquip(Find(sword), EquipmentSlot.MainHand);
                        break;
                    case 5: networkEquipment.RequestEquip(Find(armor), EquipmentSlot.Chest); break;
                    case 6: networkEquipment.RequestUnequip(EquipmentSlot.MainHand); break;
                    case 7: networkEquipment.RequestEquip(Find(sword), EquipmentSlot.MainHand); break;
                    case 8: network.RequestRemove(Find(potion), 3); break;
                    case 9: network.RequestRemove(Find(potion), -1); break;
                    case 10: network.RequestRemove(Find(potion), int.MaxValue); break;
                    case 11: networkEquipment.RequestEquip(Find(sword), EquipmentSlot.MainHand); break;
                    case 12: networkEquipment.RequestUnequip((EquipmentSlot)999); break;
                    case 13: networkEquipment.RequestStaleForValidation(Find(dragon), EquipmentSlot.MainHand, oldInventory, oldEquipment); break;
                    default:
                        pending = false; complete = true;
                        Check(responses == 14 && inventory.GetAmount(potion.Id) == 14 && inventory.GetAmount(sword.Id) == 0
                            && inventory.GetAmount(armor.Id) == 0 && inventory.GetAmount(dragon.Id) == 1
                            && equipment.GetItemId(EquipmentSlot.MainHand) == sword.Id
                            && equipment.GetItemId(EquipmentSlot.Chest) == armor.Id
                            && equipment.GetModifiedStat(ItemStat.AttackDamage, 25) == 35 && visualEvents >= 4,
                            "conservation modifiers visuals and spam");
                        Debug.Log("[InventoryProbe] COMPLETE participant=" + player.ParticipantNumber);
                        break;
                }
            }
            if (Time.realtimeSinceStartup < nextReport) return;
            nextReport = Time.realtimeSinceStartup + 0.5f;
            bool privateAllowed = network.HasStateAuthority || network.HasInputAuthority;
            bool hidden = network.SnapshotRevision == 0 && network.SlotCount == 0;
            if (!privateAllowed)
                for (int i = 0; i < NetworkInventory.Capacity; i++)
                    hidden &= network.Slots[i].Amount == 0 && string.IsNullOrEmpty(network.Slots[i].ItemId.ToString());
            Debug.Log($"[InventoryState] participant={player.ParticipantNumber} owner={network.HasInputAuthority} private={privateAllowed} hidden={hidden} potion={inventory.GetAmount(potion.Id)} sword={inventory.GetAmount(sword.Id)} dragon={inventory.GetAmount(dragon.Id)} main={equipment.GetItemId(EquipmentSlot.MainHand) == sword.Id} chest={equipment.GetItemId(EquipmentSlot.Chest) == armor.Id} visuals={visualEvents} complete={complete}");
        }
        private void OnVisual(EquipmentSlot slot, ItemDefinition item) => visualEvents++;
        private void OnDestroy()
        {
            if (!subscribed) return;
            if (visuals != null) visuals.VisualChanged -= OnVisual;
            if (networkEquipment != null) networkEquipment.RequestResolved -= Resolved;
            if (network != null) network.RemoveResolved -= Resolved;
        }
#endif
    }
}
