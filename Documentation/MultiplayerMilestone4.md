# Multiplayer Milestone 4 — authoritative inventory and equipment

## Authority and privacy

`NetworkInventory` adapts the existing `Inventory`/`InventoryState`; it does not introduce a second server inventory. The adapter implements `IInventoryAuthority`, overriding the offline `hasAuthority` flag. `SetAuthority(true)`, direct add/remove/clear/exchange, and public snapshot injection cannot authorize a network client. Mutations require a living, enabled, server-authoritative avatar registered to its connection. Host/Server and Client are supported; Shared mode and authority transfer are not supported.

Inventory contents, quantities, slot count and revision belong to a separate Fusion behaviour filtered with `ReplicateToAll(false)` and `ReplicateTo(InputAuthority, true)`. Other clients receive none of that behaviour's state, including on late join. The authoritative host can inspect every inventory. The local static item catalog is shared content, not evidence that any player owns a particular item.

The network capacity is 32 slots; the test prefab uses 16. `ItemDefinition` and `ItemCatalog` remain ScriptableObjects. Stable 32-character asset IDs, not array positions, identify items. Definitions provide stack limits, slots, requirements and modifiers. Catalog mismatch/unknown IDs fail closed; production connection-time content version negotiation is future work.

## Commands and transactions

- Trusted **server** rewards/loot systems call `NetworkInventory.TryAddItem(id, amount)` / `TryRemoveItem(id, amount)`. Existing server `Inventory.TryAdd`/`TryRemove` calls are also authority-gated. Positive quantity, catalog identity, stack capacity and available quantity are validated by the existing state model. There is **no client AddItem RPC** and no client-selected reward payload.
- The owning client can call `NetworkInventory.RequestRemove(index, amount)` to discard a quantity from one inventory stack. This removes items; it does not create a pickup, transfer to another player, or implement consumable effects. Index, positive amount, available quantity and expected inventory revision are checked on the server. Failed operations leave the state unchanged.
- The owner calls `NetworkEquipment.RequestEquip(index, EquipmentSlot)` / `RequestUnequip(slot)`. Only intent and the revisions of the viewed inventory/equipment are sent. The server checks RPC source, connection/avatar registration, living state, enabled components, idle combat/dodge state, both revisions, inventory ownership, slot compatibility and base-plus-allocated attributes. Gear cannot satisfy its own requirements. Mid-attack changes cannot alter damage after an attack has started.
- Transfers reuse `Equipment.TryEquip`/`TryUnequip` and `InventoryState.TryExchange`. Inventory and equipment commit atomically before change events. Unequip or swap that cannot return the previous item fails without loss or duplication. Unequipped items occupy inventory; equipped items do not.
- Inventory and equipment RPCs share a per-avatar server budget of one processed request per 0.1 seconds, rounded up to a simulation tick. Excess requests are dropped without reply. Version checks reject delayed/replayed requests after a successful mutation; requests select against the client's applied snapshot, not a newer unapplied network buffer.

`RemoveResolved` and `RequestResolved` report processed request results only to the owner. They are transient feedback events, not the source of truth; consumers must observe replicated state and allow for a throttled request receiving no callback. A future UI should serialize outstanding requests or add correlation IDs/timeouts if it supports concurrent operations. The same state survives late join without replaying command RPCs.

Fusion RPC source/target and host source semantics follow the [official RPC documentation](https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs). Fixed-capacity state uses [Fusion network collections](https://doc.photonengine.com/fusion/v2/manual/fusion-types/network-collections).

## Equipment and visual integration

`NetworkEquipment` publicly replicates six equipped item IDs and an equipment-only revision. It does not include inventory IDs, inventory revision, quantities, resources or requirement-source attributes. Remote `Equipment` mirrors apply all six slots atomically before firing the existing `Changed(slot, oldId, newId)` events. Server combat and attribute calculations continue to use this existing component.

`EquipmentVisualHooks.VisualChanged(slot, ItemDefinition)` provides a presentation-only hook. `GetItem(slot)` and `Refresh()` initialize late subscribers from current equipped state. A renderer can resolve its visual assets from the definition or a separate art mapping. No final inventory UI or final equipment meshes are added; the existing practice swing visual remains a melee placeholder independent of gear.

## Validation

Milestone 4 automated acceptance **passed on 2026-09-10**:

- Fresh development build: `Builds/MultiplayerInventory/DragonlandInventory.exe`, BuildReport `Succeeded errors=0 warnings=498`. Warnings are from package shaders, deprecated API/development-build directives and the optional Pipeline runtime configuration. The MCP build wrapper labels warnings as a partial command error; the completed BuildReport and executable establish success.
- Inventory regression, including the new indexed remove checks and 300 randomized operations: **passed**.
- Existing equipment Play Mode regression, including all six slots and equipped melee damage: **passed**.
- Saved catalog/references/capacity and Fusion prefab bake: **passed**.
- Real Host, owner Client and late observer: **75 verifier checks passed**. This includes server grants/removals, capacity, ownership gates, stat and slot requirements, private wire-state filtering, equip/unequip conservation, quantity replication, spam, stale requests, modifier values and visual-hook events.
- Milestone 3 regressions on the new executable: **82 network-action checks** across `host_hits`, `client_hits`, `dodge`, and **48 combat/resource checks** across `host_hits`, `client_hits`, all passed. The verifiers now accept an optional `--scenarios` selection and explicitly print which scenarios were checked; their default full suites are unchanged.
- Unity Console queried specifically for errors/exceptions after changes: **0 errors/exceptions**. Shader/build warnings remain present.

Additional `friendly` and `disconnect` regression attempts timed out connecting to Photon before combat, so they are not counted as passed on this build. A first dodge startup also timed out; the isolated retry above passed. Failed startup logs are preserved separately under `Builds/MultiplayerInventoryCombatRegression/*-startup-timeout/`. These are distinct from the successful three-peer Milestone 4 acceptance run in `Builds/MultiplayerInventoryValidation/`.

The network tests are headless: visual hook state and item definitions were validated, not final rendered equipment meshes. Final UI/art is intentionally outside this milestone.

Editor commands run through Unity MCP:

- `SetupNetworkInventory.cs` updates and saves the existing network prefab, with catalog and probe references.
- `NetworkInventoryPrefabValidation.cs` checks required components, catalog, capacity, offline flag and baked Fusion behaviour references.
- `InventoryValidation.cs` covers stacking, invalid inputs, snapshot validation, indexed removal and 300 randomized quantity-conservation operations.
- `EquipmentValidation.cs` exercises all six slots, requirements, full-inventory rollback/swap, atomic events, flat/percentage modifiers and actual melee damage in Play Mode.

`MultiplayerInventoryProbe` runs only in development automation with `-hodrInventory`. Server-only setup grants test items; no grant RPC exists. Owners send the actual production equip/remove RPCs, including invalid ownership/slot/requirements, spam, stale revisions and invalid quantities. Every peer reports privacy, quantity and visual-hook state. Late observers join after both original owners equip their items.

Run `powershell -NoProfile -File Tests/Standalone/RunMultiplayerInventory.ps1`, then `python Tests/Standalone/VerifyMultiplayerInventory.py`. The runner defaults to `Builds/MultiplayerInventory/DragonlandInventory.exe`; logs are under `Builds/MultiplayerInventoryValidation/`. Run Milestone 3 combat scenarios against this executable as a regression; without `-hodrInventory`, no test loadout is granted.

## Scope and files

Modified gameplay: `Inventory.cs`, `InventoryState.cs`, `Equipment.cs`, and the saved `NetworkTestPlayer.prefab`.

New gameplay: `IInventoryAuthority.cs`, `NetworkInventory.cs`, `IEquipmentChangePolicy.cs`, `NetworkEquipment.cs`, `EquipmentVisualHooks.cs`. New development instrumentation: `MultiplayerInventoryProbe.cs`. Unity generates and preserves their `.meta` files.

Tests: updated `InventoryValidation.cs`, `VerifyMultiplayerCombat.py` and `VerifyNetworkActions.py`; added `SetupNetworkInventory.cs`, `NetworkInventoryPrefabValidation.cs`, `RunMultiplayerInventory.ps1`, `VerifyMultiplayerInventory.py`. Added this report. No scenes or ItemDefinitions were modified; no assets deleted and no automatic commit. The Editor removed a preloaded asset during Play Mode; the original reference was restored through Unity MCP, leaving ProjectSettings unchanged.

This milestone does not add persistence, reconnect inventory restoration, network world loot, trading, a consumable system, final UI, item instances/durability, host migration or lag compensation. Fresh avatars start empty unless a trusted server game system grants items. Gameplay rewards and persistence must restore through the authoritative server APIs, never a client inventory snapshot.
