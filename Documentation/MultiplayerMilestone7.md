# Multiplayer Milestone 7 — networked loot chest interaction

Open `Assets/Scenes/MultiplayerCampTest.unity`, start a Host/Client session, clear the neutral camp, approach its chest and press **F** or the gamepad north button (**Y** on Xbox). The local prompt identifies reachable unopened chests. A server response reports success or rejection. Either team may open the chest, regardless of who cleared the camp.

## Authority and one-time transfer

`NetworkLootChest` remains a server-owned Fusion object with no player input authority. The opening RPC contains only intent; its connection identity comes from `RpcInfo.Source`. The server resolves that connection's registered avatar, applies the existing per-avatar request budget, and validates an active authoritative inventory, living player, opening distance and line of sight. There is no team filter and no client-provided recipient, rewards, seed, table or position.

The existing `LootChest` processing guard serializes requests and prevents reentrant transfers. Network chests roll once on the server and stage the complete deposit before committing. An unsuccessful capacity check changes neither inventory nor open state, reserves no claimant, and retains the same private roll for a later attempt by either team. This avoids losing overflow or opening the chest again to collect it. An authored empty roll opens once without granting items.

The first successful deposit commits inventory contents and chest state before notifications. Subsequent requests from either player fail. `IsOpen` and `OpenedBy` replicate; late joiners apply the open state at spawn and through Render. Existing `LootChestVisual` opens the lid. Inventory contents continue using the M4 owner-only replication; request responses target only the caller. No rewards are replayed when a client receives a snapshot.

`InventoryState.TryDeposit` now accepts an optional all-or-nothing flag. Offline chests keep their original partial deposits and claimant-only overflow recovery. Gameplay remains in the chest/inventory components; local input and prompt/response display live in `NetworkChestInteractor` on the camp player prefab. Camp completion/spawning from M6 is reused.

## Validation tools

- `Tests/EditorCommands/NetworkLootPrefabValidation.cs`: saved interactor, inventory composition, loot catalog/range, lid pivot and opt-in probe reference.
- Existing `CampNetworkPrefabValidation.cs` and `LootValidation.cs`: camp integration and offline regression.
- `Tests/Standalone/RunMultiplayerLoot.ps1`: starts a hidden host, remote client and late joiner using `Builds/MultiplayerLoot/DragonlandLoot.exe`.
- `Tests/Standalone/VerifyMultiplayerLoot.py <log-directory>`: rejects incomplete/failed logs and checks authority, both-team ownership, single transfer and peer state.

`MultiplayerLootProbe` runs only with `-hodrLoot` in Editor/development builds. It spawns temporary server-owned chest fixtures using the saved camp chest prefab. It tests distance/LOS/dead/unregistered rejection, full-inventory atomic failure, retained rolls, both teams winning, reentrant contention, repeat rejection, remote RPCs and late-join state. The direct contention checks execute in one server frame; the transport test submits duplicate remote requests. This is not a measured packet-latency fairness test or a new automated camp combat run.

## Acceptance results — 2026-09-13

- Final development build: `Builds/MultiplayerLoot/DragonlandLoot.exe`, **Succeeded, errors=0, warnings=17**. Warnings are development directives, an existing Fusion API deprecation and optional Pipeline configuration. The MCP wrapper reports warning-bearing builds as partial errors; the BuildReport confirms success.
- Saved camp checks: **23 passed**. New loot prefab/reference checks: **6 passed**.
- Existing offline `LootValidation` passed in Unity Play Mode, including **10,000 weighted rolls**, authority/range/LOS, partial deposits and exact overflow recovery.
- `powershell -NoProfile -File Tests/Standalone/RunMultiplayerLoot.ps1 -ValidationRoot Builds/MultiplayerLootAcceptanceRetry4`: **passed**, host + remote client + late client.
- `python Tests/Standalone/VerifyMultiplayerLoot.py Builds/MultiplayerLootAcceptanceRetry4`: **29 checks passed**.
- Final shared chest `[Id:1034]`, winner `[Player:2]` (Team B), remote winner inventory total **4 items**, host and late joiner **0**. Remote success and late already-looted rejection were received through targeted RPC responses. Both Team A and Team B also won separate server fixtures.
- Unity Console: **zero errors/exceptions**. Editor restored to clean `SampleScene.unity`, outside Play Mode, not compiling. No ProjectSettings changes.

Earlier fixture failures are preserved in `Builds/MultiplayerLootAcceptance` and `Retry1`–`Retry3`: player-registration timing, deferred collider/physics synchronization, and temporary fixture transforms being restored by Fusion. They were corrected in the test probe; failed logs are not counted as acceptance.

The network acceptance is headless. Manual keyboard/gamepad interaction and rendered prompt/lid appearance remain for a visual pass; injected packet loss, dedicated servers and host migration were not tested. Two independent packets arriving on exactly the same server tick were not measured; contention safety was tested through same-frame sequential and reentrant calls plus duplicate remote intent.

## Changed files

Modified: `Assets/Game/World/NetworkLootChest.cs`, `Assets/Game/World/LootChest.cs`, `Assets/Game/Inventory/Inventory.cs`, `Assets/Game/Inventory/InventoryState.cs`, `Assets/Game/Data/MultiplayerCamps/NetworkCampPlayer.prefab`, `Assets/Scenes/MultiplayerCampTest.unity`.

New: `Assets/Game/Interaction/NetworkChestInteractor.cs`, `Assets/Game/Core/MultiplayerLootProbe.cs`, their Unity-generated `.meta` files, `Tests/EditorCommands/NetworkLootPrefabValidation.cs`, `Tests/Standalone/RunMultiplayerLoot.ps1`, `Tests/Standalone/VerifyMultiplayerLoot.py`, this document.

No assets deleted. Prefab and scene changes were authored and saved through Unity MCP. No automatic commit.
