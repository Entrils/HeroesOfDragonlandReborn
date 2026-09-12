# Multiplayer Milestone 6 — networked neutral camps

## Server ownership and completion

`NetworkCampSpawner` spawns camp prefabs when the host/server session starts. Camps, their enemies and reward chests have no player input authority. `NetworkCamp` requires a valid server-owned object for every mutation; clients cannot initialize or complete camps through its public methods. There is no completion, spawn-enemy or XP-grant client RPC.

`NetworkCampDefinition` is the authored network composition: enemy prefabs/offsets, XP and chest prefab/offset. It reuses the existing network Wolf, Bear and Knight prefabs and their EnemyDefinitions. It is separate from the offline `CampDefinition`, whose factory uses ordinary Instantiate and requires enabled offline agents. The existing offline Camp remains intact.

The server validates every NavMesh position before spawning enemies with `Runner.Spawn`. It owns the membership list and subscribes to authoritative CharacterStats death events. Disabled/despawned live members remain unresolved, never counting as kills. Initialization failures leave a Faulted camp and remove only its partially spawned runtime members.

After all members die, the next server tick commits Active → Cleared before any reward or observer executes. The transition and completion counter cannot run twice, including reentrant callbacks. Public state replicates the status, dead/total counts, completion count, configured XP, rewarded-player count and chest NetworkId. Clients render this state; late joiners receive the final state without replaying completion events.

## XP recipients and progression

For this milestone, **every registered player connected at completion receives the full camp XP**, including dead players and either team. There is no proximity, damage-credit, split-XP or capture-team rule. Late joiners do not receive rewards from already cleared camps. The example camp awards 100 XP: level 1 → 2, 0 remaining XP and 1 progression point with the existing curve.

`NetworkSessionProgression` wraps the existing `SessionProgression` model and its session/reward-ID ledger. The server bootstrap generates a fresh match ID before spawning players. Each reward uses `camp:<network-object-id>` within that match, so duplicate grants are rejected independently of the camp's completion guard. The existing level curve, overflow-safe grants, cap and events are reused.

`IProgressionAuthority` overrides the offline authority flag. Client calls to XP grants, session resets or attribute allocation fail even after SetAuthority(true). Public accepted XP, level, points and allocated attributes are replicated; the existing local progression component mirrors them without emitting XP/level reward events. This milestone adds no client attribute-allocation RPC, persistence or reconnect restoration. At maximum level the existing progression model rejects further XP.

## Network reward chest

The server spawns one `NetworkLootChest` with the existing chest mesh, LootChest logic and loot table. Its NetworkId is stored on the camp; its reverse CampId identifies the source. Clients and late joiners receive the same chest. Spawn failure retries once per second without rerunning the camp completion or XP grants. A successfully spawned chest is not recreated if later removed.

`ILootChestAuthority` prevents clients from bypassing the offline authority flag. The server accepts only registered same-runner inventory claimants. `NetworkLootChest.RequestOpen()` is an intent API for a future network interaction UI; the server resolves the caller from RpcInfo and reuses inventory request throttling, living-state, range/LOS, random loot, claimant reservation and partial-deposit checks. No client chooses the loot table, reward list, seed or recipient. Open presentation replicates; private loot contents remain in the server chest and the existing owner-only inventory replication. The acceptance scope below is chest spawning/visibility, not a new looting UI.

## Assets and integration

Open `Assets/Scenes/MultiplayerCampTest.unity` to run this slice with the existing Host/Client panel. It uses the saved PvE arena/NavMesh and one camp containing Wolf, Bear and Knight. A dedicated NetworkCampPlayer prefab adds session progression to the existing network player composition. SampleScene, the foundation player prefab and the earlier PvE test scene are not converted or overwritten by Milestone 6.

`MultiplayerCampSceneBuilder` authors and saves assets through Unity MCP. `RegisterPrefabs()` refreshes Fusion's prefab tables and checks all new IDs; this project has two imported config assets, so both tables are refreshed. Adding another camp needs a configured NetworkCampDefinition, camp prefab and spawner entry. Runtime work is proportional to the small camp membership; no large-world optimization or continuous spawn RPCs are introduced.

## Validation

Automated acceptance **passed on 2026-09-12**.

- Saved camp/player/chest references, disabled duplicate spawner, NavMesh, Fusion bake and prefab registration: passed (23 checks plus registration checks).
- Existing ProgressionValidation (25 checks), LootValidation (including 10,000 weighted rolls) and CampProgressionValidation (14 checks): passed in Unity Play Mode. Includes duplicate/session rewards, levels, chest partial recovery and the original offline camp chain.
- Final development build: `Builds/MultiplayerCamps/DragonlandCamps.exe`, BuildReport `Succeeded errors=0 warnings=502`. Warnings concern package shaders/APIs, existing development-build directives and optional Pipeline configuration. The MCP wrapper classifies warning-bearing builds as partial errors; the BuildReport confirms success.
- Standalone runner: `powershell -NoProfile -File Tests/Standalone/RunMultiplayerCamps.ps1 -Scenario combat -ValidationRoot Builds/MultiplayerCampAcceptance` starts Host + five Clients. Three same-team owners killed the three enemy types through production movement/melee input. All peers saw the cleared camp/chest/XP; a disconnected peer was replaced by a late client.
- `-Scenario removed` despawns one living camp member on the server and confirms it cannot clear the camp or grant rewards.
- `python Tests/Standalone/VerifyMultiplayerCamps.py Builds/MultiplayerCampAcceptance`: **195 checks passed** across combat (174) and removed (21). It also checks duplicate completion/XP rejection, absence of client decision events, rejection of direct forged progression/loot mutations, consistent IDs and complete peer logs.
- Concrete final state: camp `[Id:1024]`, Cleared, 3/3 dead, completion count 1, reward 100, six rewarded players, one shared chest `[Id:1040]`. Each original player reached level 2 / XP 0 / points 1. Late participant 7 received level 1 / XP 0 / points 0 and the existing camp/chest state. Each enemy lost exactly its configured 60/180/85 health to production player attacks.
- Unity Console after changes/build/regressions: zero errors/exceptions. Editor restored to `SampleScene.unity`, clean, outside Play Mode and not compiling. ProjectSettings remain unchanged.

Successful logs are in `Builds/MultiplayerCampAcceptance/`. Earlier unsuccessful host startup logs are preserved in `Builds/MultiplayerCampValidation/` (Photon timeout) and `Builds/MultiplayerCampValidationRetry1/` (missing prefab-table registration, fixed). The successful combat folder also preserves one failed Photon startup for the late client before its successful retry; no joined-peer gameplay failure was hidden or retried.

The multiplayer acceptance was headless: it verifies spawned client chest objects, linked IDs and presentation state, not final rendered animation quality. Deliberate packet-loss/latency, dedicated-server deployment and a manual visual pass remain outside this automated run.

## Changed files

Modified for M6: `MultiplayerBootstrap.cs` (shared session ID), `MultiplayerValidationProbe.cs` (camp input ownership), `SessionProgression.cs` (authority adapter/snapshot mirror), `LootChest.cs` (authority adapter/open mirror).

New runtime: `NetworkCamp.cs`, `NetworkCampDefinition.cs`, `NetworkCampSpawner.cs`, `NetworkCampPresentation.cs`, `IProgressionAuthority.cs`, `NetworkSessionProgression.cs`, `ILootChestAuthority.cs`, `NetworkLootChest.cs`. New tooling: `MultiplayerCampProbe.cs`, `MultiplayerCampSceneBuilder.cs`, `CampNetworkPrefabValidation.cs`, `RunMultiplayerCamps.ps1`, `VerifyMultiplayerCamps.py`.

New assets: `Assets/Game/Data/MultiplayerCamps/` (camp definition, camp/player/chest prefabs), `Assets/Scenes/MultiplayerCampTest.unity`, and Unity-generated metadata. No assets deleted. No automatic commit. Earlier M4/M5 working-tree changes are preserved.
