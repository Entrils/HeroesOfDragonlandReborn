# Milestone 9: loot tables and chest

SampleScene contains Test Loot Chest near spawn at (2.5, 0.05, 2). Approach within
3 metres and press F or gamepad Y/Triangle. The closest reachable interactable is
selected. Interaction requires captured game input and is suppressed during
attacks/dodges. The chest validates range and line of sight independently. A label
marks the chest; the lid opens through a separate visual component.

## Loot data

Create > Heroes of Dragonland > Loot Table creates a ScriptableObject. Configure
minimum/maximum drop count (inclusive, 0–32), weighted item entries and inclusive
quantity ranges (1–100000). Each draw is independent, with replacement. Effective
weight = entry weight * rarity multiplier from the existing item's rarity. The
five multiplier positions are Common, Uncommon, Rare, Epic and Legendary. Zero
weights disable entries/rarities; invalid weights/ranges or no positive weight
with nonzero drops are rejected. Rarity never mutates the ItemDefinition.

TestChestLoot.asset rolls 2–4 drops from all five sample items. Weapons and armor
drop one at a time; potions 1–5 and scales 1–3. Loot output contains stable item IDs
and quantities, using InventorySlot as a value record (a reward can exceed a stack).
TryRoll accepts System.Random for reproducible tests and does not use Unity's
global random state. Item/build validation also validates all loot tables.

## Chest and overflow

LootChest starts closed with no rewards. Only authority can open or grant rewards,
and the recipient inventory must also be authoritative. A valid interaction rolls
once, records the opener, and permanently opens the chest for this scene/session.
The inventory receives as much as fits, including filling partial stacks. Every
uncollected item remains in PendingRewards; free space and interact again to collect
the exact remainder. Retries never reroll and an empty opened chest never grants
again. Overflow is reserved to the original Inventory owner for the session.

Transfers stage inventory writes, then commit the remaining loot and revision
before inventory notifications. Reentrant chest interactions are blocked. Changed
fires once per opening or successful collection; rejected/no-space retries emit no
changes. Disabled/re-enabled chests retain their state. There is no camp logic.

## Multiplayer boundary

LootChest.HasAuthority/SetAuthority and Inventory authority gate all mutations.
Future server code should receive interaction intent, authenticate the player,
validate distance/state, and run generation and transfer on the server. Clients
must never supply rewards or random seeds. Replicate IsOpen, Revision, pending
item-ID/quantity records and inventory changes together, with a network chest ID,
player ID and match/session ID. The current claimant reference must be mapped to
that stable player identity. Disconnect/reconnect ownership policy, transport,
replica application, and persistence are future work; these local flags are not
network authentication. Starting a new scene/session creates a closed chest.

## Validation

Tests/EditorCommands/LootValidation.cs runs through Unity MCP in Play Mode with
temporary fixtures. Passed: 10,000 weighted rarity draws; quantity/count bounds;
seed reproducibility; invalid/zero weight rejection; chest/player authority;
range/obstruction checks; PlayerInteractor selection; full-inventory opening;
repeat rejection without reroll; opener reservation; partial recovery (15 potions
across a 10-item slot); event/transfer consistency; and exhausted/disabled chest
behavior. The actual scene chest was exercised via the player interactor.

Changed: Inventory, InventoryState, PlayerController, item/build validation, and
SampleScene. Added: LootTable, IInteractable, InteractionReach, PlayerInteractor,
LootChest, LootChestVisual, TestChestLoot.asset, TestChestWood.mat under Assets/Art,
this document and the validation script, with Unity-generated metadata.
No pre-existing assets were deleted. Physical keyboard/gamepad feel remains a
manual playtest; logic and scene references were validated through Unity MCP.
