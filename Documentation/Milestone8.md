# Milestone 8: equipment

There are no character classes or class restrictions. Every player follows the
same slot and attribute checks. Equipment is a per-player session component with
MainHand, OffHand, Head, Chest, Hands and Legs. ItemDefinition.EquipmentSlot declares
the destination; None means not equippable. Only Weapon/Armor categories may declare
an equipment slot. One copy occupies one slot. Two-handed rules are not introduced.

## Usage and transactions

Equipment.TryEquip(inventorySlotIndex, equipmentSlot, out reason) consumes exactly
one item from the specified inventory slot. Replacing equipment returns the old
item to inventory in the same transaction. TryUnequip(slot, out reason) returns
one item. Unknown/empty/invalid sources, incorrect slots, unmet requirements,
disabled equipment, replicas and insufficient return space produce a reason and
leave state unchanged. A full inventory can swap using the incoming item's vacated
slot; a stacked source that does not free enough space cannot lose the old gear.

Inventory stages transfers on private state. Equipment and inventory commit before
the inventory Changed event, followed by Equipment.Changed(slot, oldId, newId).
Each successful transfer advances both revisions once. Reentrant equipment writes
during event delivery are refused. Failed transfers emit no events. Subscribers
should not throw exceptions; they should read committed state and schedule further
commands after notifications. Events use stable item IDs, not names or instance IDs.

Use GetItemId/GetItem to query gear. Authority is shared with the owner's Inventory;
the future network adapter must replicate inventory and equipment together, with
player/session routing. This milestone does not implement an equipment replication
transport. Equipment survives component disable/enable for the session. Destroy
and recreate the player at match end; clearing inventory alone does not clear gear.

## Requirements and modifiers

IEquipmentRequirementSource.TryGetBaseValue is the replaceable requirement boundary.
CharacterAttributes supplies minimal Inspector-configured test values: Strength,
Dexterity and Intelligence 10, Level 1. Any ItemStat may be added to its entries.
An explicit provider may be assigned to Equipment; otherwise one is discovered on
the player. Missing/duplicate/nonfinite attributes fail closed. All minimum
requirements must pass. Base values prevent self-qualification and equipment bonus
cycles. Requirements are checked when equipping; later attribute changes do not
automatically remove already equipped items. There is no progression implementation.

Equipment.GetModifiedStat(stat, baseValue) computes
max(0, (baseValue + sum(flat)) * (1 + sum(additivePercent))). It recalculates from
current items so unequipping cannot leave accumulated bonuses. CharacterAttributes
TryGetValue exposes modified attributes. MeleeAttack uses the AttackDamage result.
Armor and other modifiers are queryable; armor mitigation and changes to health,
stamina or mana pool limits are not added by this milestone.

IronSword: MainHand, Strength 8, +10 attack damage.
ScoutArmor: Chest, Dexterity 8, +5 armor.
Dragonblade: MainHand, Strength 20, +20% attack damage (fails with default attributes).
Other test items remain unequippable. IDs and .meta files are preserved.

## Debug testing

Enter Play Mode and select Player. Use InventoryDebug to add a chosen item. In
EquipmentDebug set its inventory slot index and destination, then use the component
context menu Equipment/Equip Inventory Slot or Equipment/Unequip Slot. The Console
reports success or the rejection reason. InventoryDebug/Log Snapshot shows returned
items. Change CharacterAttributes test values in the Inspector to exercise thresholds.
No UI rendering, drag-and-drop, character classes, or equipment visuals are added.

Tests/EditorCommands/EquipmentValidation.cs runs with Unity MCP in Play Mode. It
uses temporary runtime fixtures and checks all six slots, invalid item/slot reasons,
missing/insufficient/satisfied attributes, full return failure, stacked-source
rollback, full swap success, event atomicity, quantity conservation, authority,
flat/percentage bonuses, bonus removal, and actual melee damage. ItemValidation
and InventoryValidation regression checks also passed. Console: zero errors/warnings.

Changed: ItemDefinition, Inventory/InventoryState, MeleeAttack, three sample item
assets, SampleScene. Added: EquipmentSlot, Equipment, CharacterAttributes,
IEquipmentRequirementSource, EquipmentDebug, validation script, this document and
Unity-generated metadata. No assets deleted.
