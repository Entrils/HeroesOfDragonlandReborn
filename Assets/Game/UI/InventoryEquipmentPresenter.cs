using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.UI
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(InventoryEquipmentView))]
    public sealed class InventoryEquipmentPresenter : MonoBehaviour
    {
        [SerializeField] private MultiplayerBootstrap bootstrap;
        private InventoryEquipmentView view;
        private LocalGameplayInputGate inputGate;
        private NetworkObject actor;
        private Inventory inventory;
        private Equipment equipment;
        private NetworkInventory networkInventory;
        private NetworkEquipment networkEquipment;
        private CharacterAttributes attributes;
        private CharacterStats stats;
        private int selectedIndex = -1;
        private EquipmentSlot selectedEquipment;
        private string selectedId, feedback = "", lastBlock;
        private bool dirty, pending;
        private float requestAt, nextRequestAt;
        public InventoryEquipmentView View => view;
        public NetworkObject LocalActor => actor;
        public bool RequestPending => pending;
        public int SelectedInventoryIndex => selectedIndex;
        private void Awake() { view = GetComponent<InventoryEquipmentView>(); inputGate = bootstrap != null ? bootstrap.GetComponent<LocalGameplayInputGate>() : null; }
        private void OnEnable()
        {
            view.ToggleRequested += Toggle;
            view.CloseRequested += Close;
            view.InventorySelected += SelectInventory;
            view.EquipmentSelected += SelectEquipment;
            view.EquipRequested += RequestEquip;
            view.UnequipRequested += RequestUnequip;
        }
        private void OnDisable()
        {
            view.ToggleRequested -= Toggle; view.CloseRequested -= Close;
            view.InventorySelected -= SelectInventory; view.EquipmentSelected -= SelectEquipment;
            view.EquipRequested -= RequestEquip; view.UnequipRequested -= RequestUnequip;
            Bind(null);
        }
        private void Update()
        {
            if (!Application.isFocused || Keyboard.current == null) return;
            if (Keyboard.current.iKey.wasPressedThisFrame) Toggle();
            else if (view.IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }
        private void LateUpdate()
        {
            NetworkObject local = null;
            var runner = bootstrap != null ? bootstrap.Runner : null;
            if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var candidate) && candidate != null
                && candidate.IsValid && candidate.HasInputAuthority) local = candidate;
            if (!ReferenceEquals(local, actor)) Bind(local);
            if (pending && Time.unscaledTime - requestAt > 4)
            { pending = false; feedback = "No response yet. Check the current slots before retrying."; dirty = true; }
            string block = BlockReason();
            if (lastBlock != block) { lastBlock = block; dirty = true; }
            if (dirty) Refresh();
        }
        private void Bind(NetworkObject value)
        {
            Close();
            if (inventory != null) inventory.Changed -= OnInventory;
            if (equipment != null) equipment.Changed -= OnEquipment;
            if (attributes != null) attributes.Changed -= Invalidate;
            if (networkEquipment != null) networkEquipment.RequestResolved -= OnResolved;
            actor = value; inventory = null; equipment = null; attributes = null; networkInventory = null; networkEquipment = null; stats = null;
            ClearSelection(); pending = false; feedback = ""; nextRequestAt = 0;
            if (actor != null)
            {
                inventory = actor.GetComponent<Inventory>(); equipment = actor.GetComponent<Equipment>();
                attributes = actor.GetComponent<CharacterAttributes>(); stats = actor.GetComponent<CharacterStats>();
                networkInventory = actor.GetComponent<NetworkInventory>(); networkEquipment = actor.GetComponent<NetworkEquipment>();
                if (inventory != null) inventory.Changed += OnInventory;
                if (equipment != null) equipment.Changed += OnEquipment;
                if (attributes != null) attributes.Changed += Invalidate;
                if (networkEquipment != null) networkEquipment.RequestResolved += OnResolved;
            }
            view.SetAvailable(inventory != null && equipment != null && networkEquipment != null);
            dirty = true;
        }
        public void Toggle() { if (view.IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (actor == null || !actor.IsValid || !actor.HasInputAuthority || inventory == null || equipment == null || inputGate == null) return;
            view.SetOpen(true); inputGate.SetBlocked(this, true); dirty = true;
            Refresh();
        }
        public void Close() { view.SetOpen(false); if (inputGate != null) inputGate.SetBlocked(this, false); }
        private void ClearSelection() { selectedIndex = -1; selectedEquipment = EquipmentSlot.None; selectedId = null; }
        public void SelectInventory(int index)
        {
            if (!view.IsOpen || inventory == null || index < 0 || index >= inventory.SlotCount) return;
            selectedIndex = index; selectedEquipment = EquipmentSlot.None; selectedId = inventory.GetSlot(index).ItemId;
            feedback = ""; Refresh();
        }
        public void SelectEquipment(EquipmentSlot slot)
        {
            if (!view.IsOpen || equipment == null || slot < EquipmentSlot.MainHand || slot > EquipmentSlot.Legs) return;
            selectedEquipment = slot; selectedIndex = -1; selectedId = equipment.GetItemId(slot);
            feedback = ""; Refresh();
        }
        private void Invalidate() { dirty = true; view.HideTooltip(); }
        private void OnInventory(InventorySnapshot _) => Invalidate();
        private void OnEquipment(EquipmentSlot slot, string before, string after) => Invalidate();
        private ItemDefinition SelectedItem(out int amount)
        {
            amount = 0;
            if (inventory == null || equipment == null) return null;
            string id = null;
            if (selectedIndex >= 0 && selectedIndex < inventory.SlotCount)
            { var slot = inventory.GetSlot(selectedIndex); id = slot.ItemId; amount = slot.Amount; }
            else if (selectedEquipment != EquipmentSlot.None) { id = equipment.GetItemId(selectedEquipment); amount = id != null ? 1 : 0; }
            // An inventory snapshot can replace the selected index. Never act on a different item silently.
            if (id != selectedId) { ClearSelection(); return null; }
            return inventory.TryResolveItem(id, out var item) ? item : null;
        }
        private string BlockReason()
        {
            if (actor == null || !actor.IsValid || !actor.HasInputAuthority || networkInventory == null || networkEquipment == null) return "No local player.";
            if (!networkInventory.CanRequest || !networkEquipment.isActiveAndEnabled) return "Equipment requests are unavailable.";
            if (networkInventory.SnapshotRevision == 0 || networkEquipment.SnapshotRevision == 0) return "Synchronizing inventory...";
            if (!NetworkMatch.AllowsGameplay(actor.Runner)) return "Equipment can change only while the match is playing.";
            if (stats == null || stats.IsDead) return "Cannot change equipment while dead.";
            var combat = actor.GetComponent<NetworkPlayerCombat>(); var dodge = actor.GetComponent<NetworkPlayerDodge>();
            if ((combat != null && combat.IsBusy) || (dodge != null && dodge.Busy)) return "Finish your current action before changing equipment.";
            if (pending) return "Waiting for server...";
            if (Time.unscaledTime < nextRequestAt) return "Please wait...";
            return "";
        }
        private void Refresh()
        {
            dirty = false;
            var selected = SelectedItem(out int quantity);
            if (inventory == null)
            { view.SetSlotCount(0); view.SetDetails("Select an item.", false, false, false); view.SetStatus(""); return; }
            view.SetSlotCount(inventory.SlotCount);
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i); inventory.TryResolveItem(slot.ItemId, out var item);
                view.SetInventorySlot(i, item, slot.Amount, selectedIndex == i, InventoryItemText.Describe(item, slot.Amount, equipment, out _));
            }
            for (int i = 1; i <= 6; i++)
            {
                var slot = (EquipmentSlot)i; var item = equipment.GetItem(slot);
                view.SetEquipmentSlot(slot, item, selectedEquipment == slot, InventoryItemText.Describe(item, 1, equipment, out _));
            }
            bool met;
            string description = InventoryItemText.Describe(selected, quantity, equipment, out met);
            string block = BlockReason();
            bool valid = selected != null && selected.TryValidate(out _);
            bool room = selected != null && inventory.CanAdd(selected.Id);
            bool canEquip = selectedIndex >= 0 && valid && selected.EquipmentSlot != EquipmentSlot.None && met && block.Length == 0;
            bool canUnequip = selectedEquipment != EquipmentSlot.None && valid && room && block.Length == 0;
            view.SetDetails(description, canEquip, canUnequip, selected != null && selected.Category == ItemCategory.Consumable);
            string message = block.Length > 0 ? block : feedback;
            if (message.Length == 0 && selected != null && !met) message = "Requirements not met. See all MISSING attributes above.";
            if (message.Length == 0 && selectedEquipment != EquipmentSlot.None && selected != null && !room) message = "Inventory has no room for this item.";
            view.SetStatus(message);
        }
        public void RequestEquip()
        {
            Refresh();
            var item = SelectedItem(out _);
            if (!view.IsOpen || !view.EquipEnabled || item == null) return;
            BeginRequest();
            if (!networkEquipment.RequestEquip(selectedIndex, item.EquipmentSlot)) OnResolved(false, "Equipment request could not be sent.");
            dirty = true;
        }
        public void RequestUnequip()
        {
            Refresh();
            if (!view.IsOpen || !view.UnequipEnabled) return;
            BeginRequest();
            if (!networkEquipment.RequestUnequip(selectedEquipment)) OnResolved(false, "Unequip request could not be sent.");
            dirty = true;
        }
        private void BeginRequest() { pending = true; requestAt = Time.unscaledTime; nextRequestAt = requestAt + .2f; feedback = ""; }
        private void OnResolved(bool accepted, string reason)
        {
            pending = false; feedback = accepted ? "Equipment updated." : "Not changed: " + reason;
            nextRequestAt = Mathf.Max(nextRequestAt, Time.unscaledTime + .15f); dirty = true;
        }
    }
}
