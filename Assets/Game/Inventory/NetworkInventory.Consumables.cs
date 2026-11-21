using Fusion;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Multiplayer
{
    public sealed partial class NetworkInventory
    {
        private readonly TickTimer[] potionCooldowns = new TickTimer[5];
        private TickTimer globalPotionCooldown;
        private TickTimer defensePotion, damagePotion;
        private float defenseAmount, damageAmount;
        private bool usingConsumable;
        private LocalGameplayInputGate potionInputGate;
        [SerializeField, Range(1, QuickItemSlots.MaximumSlots)] private int quickSlotCount = 4;
        private QuickItemSlots quickSlots;
        public QuickItemSlots QuickSlots => CanRequest
            ? quickSlots ??= new QuickItemSlots(inventory, Mathf.Clamp(quickSlotCount, 1, QuickItemSlots.MaximumSlots))
            : null;

        public bool RequestUseSelectedQuickItem()
            => QuickSlots is { } slots && slots.TryFindSelectedInventoryIndex(out int index)
                && RequestUseConsumable(index);

        internal float IncomingPotionMultiplier => PotionActive(defensePotion) ? 1f - defenseAmount : 1f;
        internal float OutgoingPotionMultiplier => PotionActive(damagePotion) ? 1f + damageAmount : 1f;

        private bool PotionActive(TickTimer timer)
            => Object != null && Object.IsValid && Runner.IsServer && HasStateAuthority
                && !stats.IsDead && NetworkMatch.AllowsGameplay(Runner) && timer.IsRunning && !timer.Expired(Runner);

        private void ClearPotionBuffs()
        {
            defensePotion = TickTimer.None;
            damagePotion = TickTimer.None;
        }

        // Accepts a slot from the owner's current inventory snapshot. A successful use
        // changes the revision, making repeats of the same request stale.
        public bool RequestUseConsumable(int index)
        {
            if (!CanRequest || index < 0 || index >= inventory.SlotCount) return false;
            RPC_UseConsumable(index, HasStateAuthority ? SnapshotRevision : inventory.Revision);
            return true;
        }

        public bool RequestQuickUse(ConsumableEffect effect)
        {
            if (!CanRequest) return false;
            for (int i = 0; i < inventory.SlotCount; i++)
                if (inventory.TryResolveItem(inventory.GetSlot(i).ItemId, out var item)
                    && item.Consumable != null && item.Consumable.Effect == effect)
                    return RequestUseConsumable(i);
            return false;
        }

        private void Update()
        {
            if (!CanRequest || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked
                || (potionInputGate != null && potionInputGate.IsBlocked)
                || !NetworkMatch.AllowsGameplay(Runner) || Keyboard.current is not { } keyboard) return;

            if (keyboard.leftBracketKey.wasPressedThisFrame) QuickSlots.SelectPrevious();
            else if (keyboard.rightBracketKey.wasPressedThisFrame) QuickSlots.SelectNext();

            if (keyboard.gKey.wasPressedThisFrame) RequestUseSelectedQuickItem();
            else if (keyboard.f1Key.wasPressedThisFrame) RequestQuickUse(ConsumableEffect.Health);
            else if (keyboard.f2Key.wasPressedThisFrame) RequestQuickUse(ConsumableEffect.Stamina);
            else if (keyboard.f3Key.wasPressedThisFrame) RequestQuickUse(ConsumableEffect.Mana);
            else if (keyboard.f4Key.wasPressedThisFrame) RequestQuickUse(ConsumableEffect.Defense);
            else if (keyboard.f5Key.wasPressedThisFrame) RequestQuickUse(ConsumableEffect.Damage);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_UseConsumable(int index, long expectedRevision, RpcInfo info = default)
        {
            if (!AdmitRequest(info.Source) || !CanMutateInventory || !stats.CanMutate || usingConsumable
                || expectedRevision != SnapshotRevision || index < 0 || index >= inventory.SlotCount) return;

            if (!inventory.TryResolveItem(inventory.GetSlot(index).ItemId, out var item)
                || item.Category != ItemCategory.Consumable || item.Consumable is not { IsValid: true } potion
                || !globalPotionCooldown.ExpiredOrNotRunning(Runner)
                || !potionCooldowns[(int)potion.Effect].ExpiredOrNotRunning(Runner)) return;

            bool canApply = potion.Effect switch
            {
                ConsumableEffect.Health => stats.CurrentHealth < stats.MaxHealth,
                ConsumableEffect.Stamina => stats.CurrentStamina < stats.MaxStamina,
                ConsumableEffect.Mana => stats.CurrentMana < stats.MaxMana,
                ConsumableEffect.Defense => defensePotion.ExpiredOrNotRunning(Runner),
                ConsumableEffect.Damage => damagePotion.ExpiredOrNotRunning(Runner),
                _ => false
            };
            if (!canApply) return;

            usingConsumable = true;
            try
            {
                inventory.TryConsumeAt(index, () => ApplyPotion(potion));
            }
            finally { usingConsumable = false; }
        }

        private void ApplyPotion(ConsumableDefinition potion)
        {
            potionCooldowns[(int)potion.Effect] = PotionTimer(potion.Cooldown);
            globalPotionCooldown = PotionTimer(inventory.Catalog.GlobalPotionCooldown);
            switch (potion.Effect)
            {
                case ConsumableEffect.Health: stats.Heal(potion.Amount); break;
                case ConsumableEffect.Stamina: stats.RestoreStamina(potion.Amount); break;
                case ConsumableEffect.Mana: stats.RestoreMana(potion.Amount); break;
                case ConsumableEffect.Defense:
                    defenseAmount = potion.Amount;
                    defensePotion = PotionTimer(potion.Duration);
                    break;
                case ConsumableEffect.Damage:
                    damageAmount = potion.Amount;
                    damagePotion = PotionTimer(potion.Duration);
                    break;
            }
        }

        private TickTimer PotionTimer(float seconds)
            => seconds > 0 ? TickTimer.CreateFromTicks(Runner, Mathf.Max(1, Mathf.CeilToInt(seconds / Runner.DeltaTime))) : TickTimer.None;
    }
}
