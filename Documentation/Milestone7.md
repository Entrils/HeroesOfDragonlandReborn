# Milestone 7: session inventory

The SampleScene Player owns an Inventory component with 16 slots and the existing
TestItemCatalog. Configure catalog and slot count (1–256) before Awake. Slot count
is fixed for that session; editing the Inspector during play does not resize it.
Each component owns a separate InventoryState. No singleton, UI, save file or
DontDestroyOnLoad state is introduced. A new player starts empty. Disable/enable
preserves items within the session; call Clear on the authority when reusing a
player for a new match, or destroy/recreate the player at the match boundary.

## API and behavior

- TryAdd(ItemDefinition/string ID, amount) and TryRemove(...) return success.
  Requests are all-or-nothing. Unknown items, nonpositive amounts, insufficient
  space, or insufficient items are rejected without events or partial changes.
- Existing stacks fill first, then empty slots in index order. Maximum stack size
  comes from the catalog's ItemDefinition, not from a caller-supplied object.
  Removal walks slot order and clears depleted slots without shifting others.
- GetAmount returns a long total. CanAdd checks capacity for a specific item.
  HasEmptySlot reports slot occupancy. IsFull means no empty slots and no room in
  any existing stack; an inventory with only partial stacks may still reject a
  different item. InventoryState.GetCapacity returns remaining item capacity.
- Changed publishes one detached InventorySnapshot after each successful mutation.
  Failed operations and clearing an already empty inventory emit nothing. Consumers
  read CaptureSnapshot initially, then subscribe/unsubscribe for later changes.
  Slot values are structs; callers cannot mutate the live slot array.

## Future network adapter

InventoryState contains the operations; Inventory supplies Unity ownership and
authority gating. Local sessions default to HasAuthority=true. A future networking
adapter sets replicas false, forwards client intents to the server, and applies
trusted snapshots only to replicas. SetAuthority is an integration hook, not an
authentication/security implementation. Process calls on Unity's main thread.

Snapshots contain schema version 1, monotonic revision and ordered slots of string
item IDs plus integer amounts. They carry no Unity object references. JsonUtility
roundtrips them. Apply rejects unknown IDs, invalid amounts, oversized stacks,
wrong slot counts/schema, and stale/duplicate revisions before changing anything.
Initial empty state is revision 0 on both peers; its duplicate needs no application.
The networking adapter must route by player AND match/session identity, authenticate
the sender and load matching catalogs. Snapshot revisions are local to that session;
they are not cross-match identity. No transport or client prediction is implemented.

## Debugging and checks

In Play Mode, select Player and open InventoryDebug's component context menu:
Inventory/Add Test Item, Remove Test Item, Log Snapshot, or Clear Session Items.
Default test item is Mana Potion, amount 7. Change these Inspector fields to test
equipment or larger quantities. Commands only run in the editor/development builds.
For example, adding 7 twice produces stacks of 10 and 4; logs show accepted or
rejected requests, and Log Snapshot prints IDs, quantities and revision.

Tests/EditorCommands/InventoryValidation.cs runs through Unity MCP RunCommand.
Passed: split/fill stacking, capacity/full detection, atomic failure, events,
cross-stack removal, clearing, owner/snapshot isolation, unstackable weapons,
JSON replication, invalid/stale payload rejection, and 300 randomized operations
with quantity/stack invariants. Play Mode also passed real player wiring, empty
start, authority/replica gates, snapshot application and debug add/remove.
Unity compiled successfully; Console had zero errors and warnings.
