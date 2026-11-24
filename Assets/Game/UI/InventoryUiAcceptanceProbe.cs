using UnityEngine;
#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
#endif

namespace HeroesOfDragonland.UI
{
    [DefaultExecutionOrder(200)]
    public sealed class InventoryUiAcceptanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        private bool active, preview, completed;
        private InventoryEquipmentPresenter presenter;
        private readonly Dictionary<NetworkObject, int> fixtures = new();
        private int step;
        private float nextAction;
        private void Awake() { active = Environment.GetCommandLineArgs().Contains("-hodrInventoryUi"); presenter = GetComponent<InventoryEquipmentPresenter>(); }
        public void BeginPreview() { active = true; preview = true; }
        private static ItemDefinition Item(Inventory inventory, string name) => inventory.Catalog.Items.First(item => item.name == name);
        private static int Find(Inventory inventory, ItemDefinition item)
        { for (int i = 0; i < inventory.SlotCount; i++) if (inventory.GetSlot(i).ItemId == item.Id) return i; return -1; }
        private void Check(bool ok, string label)
        { if (!ok) { active = false; throw new InvalidOperationException("[InventoryUiCheck] FAIL " + label); } Debug.Log("[InventoryUiCheck] PASS " + label); }
        private void Seed(NetworkRunner runner)
        {
            foreach (var owner in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(owner, out var actor) || actor == null) continue;
                var inventory = actor.GetComponent<Inventory>(); var equipment = actor.GetComponent<Equipment>();
                var network = actor.GetComponent<NetworkInventory>();
                var sword = Item(inventory, "IronSword"); var potion = Item(inventory, "ManaPotion");
                if (!fixtures.TryGetValue(actor, out int state))
                {
                    Check(network.TryAddItem(sword.Id) && network.TryAddItem(potion.Id, 3)
                        && network.TryAddItem(Item(inventory, "Dragonblade").Id) && network.TryAddItem(Item(inventory, "ScoutArmor").Id), "server-fixture-only");
                    var stats = actor.GetComponent<CharacterStats>();
                    stats.TrySpendMana(stats.CurrentMana);
                    fixtures[actor] = 0; continue;
                }
                if (preview) continue;
                string main = equipment.GetItemId(EquipmentSlot.MainHand);
                if (state == 0 && main == sword.Id) fixtures[actor] = 1;
                else if (state == 1 && main == null) fixtures[actor] = 2;
                else if (state == 2 && main == sword.Id)
                {
                    int safety = inventory.SlotCount * potion.MaximumStackSize;
                    while (safety-- > 0 && inventory.CanAdd(potion.Id)) Check(network.TryAddItem(potion.Id), "server-fill-slot");
                    fixtures[actor] = 3;
                }
                else if (state == 3 && main == null)
                {
                    Check(actor.GetComponent<NetworkSessionProgression>().TryGrantExperience(2000, "inventory-ui-requirements"), "grant-requirement-progression");
                    var progression = actor.GetComponent<SessionProgression>();
                    for (int i = 0; i < 10; i++) Check(progression.TryIncreaseAttribute(ItemStat.Strength, progression.SessionId, out _), "allocate-requirement-points");
                    fixtures[actor] = 4;
                }
            }
        }
        private void LateUpdate()
        {
            if (!active) return;
            var bootstrap = GetComponentInParent<MultiplayerBootstrap>(); var runner = bootstrap.Runner;
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (runner == null || !runner.IsRunning || match == null || !match.Object.IsValid || match.State != MatchPhase.Playing) return;
            if (runner.IsServer) Seed(runner);
            if (completed || presenter.LocalActor == null) return;
            var actor = presenter.LocalActor; var inventory = actor.GetComponent<Inventory>(); var equipment = actor.GetComponent<Equipment>();
            var network = actor.GetComponent<NetworkInventory>();
            var quickSlots = network.QuickSlots;
            var sword = Item(inventory, "IronSword"); var potion = Item(inventory, "ManaPotion"); var dragon = Item(inventory, "Dragonblade"); var armor = Item(inventory, "ScoutArmor");
            if (preview) { if (!presenter.View.IsOpen) { presenter.Open(); presenter.SelectInventory(Find(inventory, dragon)); } return; }
            if (Time.unscaledTime < nextAction || presenter.RequestPending || inventory.GetAmount(dragon.Id) == 0 && step < 7) return;
            nextAction = Time.unscaledTime + .4f;
            switch (step)
            {
                case 0:
                    Check(actor.HasInputAuthority && actor.InputAuthority == runner.LocalPlayer, "local-owner-only");
                    presenter.Open(); Check(bootstrap.GetComponent<LocalGameplayInputGate>().IsBlocked, "inventory-blocks-combat-input");
                    Check(presenter.View.DisplayedSlotCount == inventory.SlotCount, "all-inventory-slots-rendered");
                    presenter.SelectInventory(Find(inventory, dragon));
                    Check(!presenter.View.EquipEnabled && presenter.View.DetailsText.Contains("MISSING  Strength") && presenter.View.DetailsText.Contains("need +10"), "requirement-reason-and-disabled-equip");
                    long before = inventory.Revision; presenter.RequestEquip(); Check(inventory.Revision == before && !presenter.RequestPending, "disabled-button-does-not-request");
                    presenter.SelectInventory(Find(inventory, potion));
                    Check(presenter.View.QuickAssignmentAvailable && presenter.View.DetailsText.Contains("Quantity: 3")
                        && presenter.AssignSelectedToQuickSlot() && inventory.GetAmount(potion.Id) == 3,
                        "quick-assignment-keeps-quantity");
                    quickSlots.SelectPrevious();
                    Check(quickSlots.Selected.Index == quickSlots.Count - 1 && quickSlots.Selected.Item == null
                        && !network.RequestUseSelectedQuickItem(), "quick-wrap-and-empty-use");
                    quickSlots.SelectNext();
                    Check(quickSlots.Selected.Item == potion && quickSlots.Selected.Quantity == 3, "quick-selection-and-quantity");
                    presenter.Close();
                    Check(network.RequestUseSelectedQuickItem(), "quick-use-request-sent");
                    step = 9; break;
                case 9:
                    if (inventory.GetAmount(potion.Id) != 2) return;
                    Check(quickSlots.Selected.Quantity == 2 && quickSlots.Selected.Item == potion,
                        "server-use-decrements-quick-quantity");
                    Check(network.RequestRemove(Find(inventory, potion), 2), "remove-last-quick-stack-request");
                    step = 10; break;
                case 10:
                    if (inventory.GetAmount(potion.Id) != 0) return;
                    Check(quickSlots.Selected.Item == null && quickSlots.Selected.Quantity == 0
                        && !network.RequestUseSelectedQuickItem(), "last-item-clears-quick-slot");
                    presenter.Open();
                    presenter.SelectInventory(Find(inventory, sword)); Check(presenter.View.EquipEnabled, "valid-item-equip-enabled"); presenter.RequestEquip(); step = 1; break;
                case 1:
                    if (equipment.GetItemId(EquipmentSlot.MainHand) != sword.Id) return;
                    Check(inventory.GetAmount(sword.Id) == 0, "equip-replicated-conservation");
                    presenter.SelectInventory(Find(inventory, armor)); presenter.RequestEquip(); step++; break;
                case 2:
                    if (equipment.GetItemId(EquipmentSlot.Chest) != armor.Id) return;
                    presenter.SelectEquipment(EquipmentSlot.MainHand); Check(presenter.View.UnequipEnabled, "unequip-enabled-with-space"); presenter.RequestUnequip(); step++; break;
                case 3:
                    if (equipment.GetItemId(EquipmentSlot.MainHand) != null) return;
                    Check(inventory.GetAmount(sword.Id) == 1, "unequip-returned-to-bag");
                    presenter.SelectInventory(Find(inventory, sword)); presenter.RequestEquip(); step++; break;
                case 4:
                    if (equipment.GetItemId(EquipmentSlot.MainHand) != sword.Id || inventory.HasEmptySlot) return;
                    presenter.SelectEquipment(EquipmentSlot.MainHand);
                    Check(!presenter.View.UnequipEnabled && presenter.View.StatusText.Contains("Bag full"), "full-bag-disabled-unequip");
                    // Test fixture frees space through the pre-existing validated remove API, not a UI action.
                    int index = Find(inventory, potion);
                    presenter.SelectInventory(index);
                    presenter.RequestDiscard();
                    Check(presenter.View.StatusText.Contains("Confirm discard") && inventory.GetAmount(potion.Id) > 0, "discard-requires-confirmation");
                    presenter.RequestDiscard();
                    step++; break;
                case 5:
                    if (!inventory.HasEmptySlot) return;
                    presenter.SelectEquipment(EquipmentSlot.MainHand); Check(presenter.View.UnequipEnabled, "space-update-enables-unequip"); presenter.RequestUnequip(); step++; break;
                case 6:
                    if (equipment.GetItemId(EquipmentSlot.MainHand) != null || !equipment.TryGetRequirementValue(ItemStat.Strength, out float strength) || strength < 20) return;
                    presenter.SelectInventory(Find(inventory, dragon)); Check(presenter.View.EquipEnabled && !presenter.View.DetailsText.Contains("MISSING"), "attribute-event-enables-equip"); presenter.RequestEquip(); step++; break;
                case 7:
                    if (equipment.GetItemId(EquipmentSlot.MainHand) != dragon.Id) return;
                    Check(inventory.GetAmount(dragon.Id) == 0 && inventory.GetAmount(sword.Id) == 1, "final-server-equipment-state");
                    presenter.Close(); step++; break;
                case 8:
                    Check(!presenter.View.IsOpen && !bootstrap.GetComponent<LocalGameplayInputGate>().IsBlocked, "close-releases-input");
                    completed = true; Debug.Log("[InventoryUiComplete] server=" + runner.IsServer); break;
            }
        }
#endif
    }
}
