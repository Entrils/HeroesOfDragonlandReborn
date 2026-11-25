#if UNITY_EDITOR || DEBUG
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;

namespace HeroesOfDragonland.Multiplayer
{
    // Server bot loadout improvements through existing requirements and atomic transfers.
    internal static class MatchSimulationBotLoadout
    {
        private static readonly ItemStat[] Attributes =
            { ItemStat.Strength, ItemStat.Vitality, ItemStat.Endurance, ItemStat.Mind, ItemStat.Dexterity };

        internal static void Improve(NetworkTestPlayer player)
        {
            var inventory = player.GetComponent<Inventory>();
            var equipment = player.GetComponent<Equipment>();
            if (equipment != null && player.GetComponent<NetworkEquipment>().CanChangeEquipment)
                for (int i = 0; i < inventory.SlotCount; i++)
                {
                    var slot = inventory.GetSlot(i);
                    if (slot.IsEmpty || !inventory.TryResolveItem(slot.ItemId, out var item) || item.EquipmentSlot == EquipmentSlot.None) continue;
                    var current = equipment.GetItem(item.EquipmentSlot);
                    if (current == null || item.Value > current.Value) equipment.TryEquip(i, item.EquipmentSlot, out _);
                }
            var progression = player.GetComponent<SessionProgression>();
            if (progression != null && progression.ProgressionPoints > 0)
            {
                progression.TryIncreaseAttribute(Attributes[(int)(progression.Revision % Attributes.Length)], progression.SessionId, out _);
            }
        }
    }
}
#endif
