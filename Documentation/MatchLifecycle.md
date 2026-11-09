# Network match lifecycle

## Completion verification (2026-11-06)

Status: complete. Resumed the saved implementation and verified it against a fresh Windows Development build. No additional runtime fixes were required.

- Deterministic rules: 130 checks passed.
- Unity MCP: 68 lifecycle, 76 team spawn, 40 respawn and 43 fortress prefab/reference checks passed (357 checks including deterministic rules).
- Fresh build: `Builds/MatchLifecycle/DragonlandLifecycle.exe`, `Succeeded errors=0 warnings=1`. The remaining warning is the optional Unity Pipeline package's missing RuntimePipelineConfig; the build succeeded.
- Two-round host/client acceptance passed on the fresh build: `Builds/LifecycleAcceptance-55646b51/`. Verified one-player lobby waiting, replicated phase changes and winners, new avatars/session IDs, clean progression/inventory/equipment/world state, preserved teams, and frozen damage/rewards after finish.
- Respawn regression passed on the same build: `Builds/RespawnAcceptance-3b56d0b0/`. Both teams completed two revival cycles with preserved state, protection, replicated events and match-end cancellation.
- Six-peer team spawn acceptance passed on the same build: `Builds/TeamSpawnAcceptance-59d4a795/`. Verified occupied-team waiting, distinct team positions, reservations, client authority rejection, NPC policy and actual walk/dodge entry blocking. This resolves the earlier Photon connectivity acceptance limitation.
- Final Unity MCP Console error inspection returned zero errors. Open scenes and assets were saved; Editor remained in Edit Mode, with no compilation in progress and no unsaved scene changes.
- Runtime acceptance used standalone batch players. Normal controls and visual layout still benefit from the game director's manual review; no visual approval is claimed.

Completion changes: updated this report, `TeamSpawnAreas.md` and `SessionRespawn.md`; saved the open `SampleScene.unity` through Unity MCP. No new runtime systems or deleted assets; no commit created. The existing uncommitted milestone implementation is preserved and inventoried below.

The fortress match uses a server-owned `NetworkMatch` with six replicated phases:

| Phase | Behavior |
| --- | --- |
| Boot | Bind the coordinator, verify lifecycle configuration, then enter Lobby. |
| Lobby | Keep connections and team assignments. Show the replicated roster, spawn waiting status and minimum player count. Start automatically when enough avatars are ready and both teams have players. |
| Starting | Create a new progression session ID, recreate players and world objects, and run the configured countdown. Combat, movement, rewards and objectives remain gated. A disconnect below the required team/player count cancels preparation through ReturningToLobby. Occupied spawn points delay readiness, with no opposing-team fallback. |
| Playing | Open the gameplay gate: combat, movement, regeneration, AI, camps, chests and Kings run on the server. |
| Finished | Commit the winning team once, freeze gameplay and objective progression immediately, display the winner and wait for the configured end delay. |
| ReturningToLobby | Remove round objects and recreate clean lobby avatars without disconnecting players. After the return delay, clear the result and enter Lobby. |

`MatchPhase` is separate from the existing offline `MatchStateMachine`, so the old offline test scenes retain their existing behavior. Empty online matches return without declaring a winner. Losing one team during Playing does not invent a forfeit rule: remaining players can finish the King objective or wait for joins.

## Configuration and authoring

`MatchLifecycleConfig` is a ScriptableObject referenced by the network match prefab. Prototype defaults are two players (with both teams represented), three seconds for Starting, eight seconds for Finished, and one second for ReturningToLobby. All values are configurable; no final balance is implied.

Run `MatchLifecycleSceneBuilder.Install()` through Unity MCP to create `Assets/Game/Data/MultiplayerFortress/MatchLifecycle.asset`, assign it to `NetworkMatch.prefab`, install `NetworkRoundReset` and the opt-in acceptance probe, rebake the Fusion prefab table and save `MultiplayerFortressTest.unity`. The normal fortress builder also calls this installer. Existing lifecycle configuration is preserved on repeat installation.

The existing fortress UI now displays the phase, team allocation, readiness, countdown and winner. A server-only Retry preparation action is available for corrected authoring failures; clients cannot force transitions. Other views can subscribe to `NetworkMatch.StateChanged` and `Ended`, and should read the current snapshot immediately when binding. Network snapshots may skip very short intermediate phases, especially with zero-duration settings.

## Reset ownership

`NetworkRoundReset` coordinates the round boundary. The server despawns session-owned player avatars, AI (including camp enemies and guards), camps, reward chests and Kings. Factories recreate their authored defaults. The bootstrap and its team roster survive; connected players retain their team and player connection identity. New avatar network IDs invalidate requests addressed to old avatars.

This resets level to 1, XP to 0, earned/unallocated attribute points to 0, allocated attributes to 0, inventory and equipment to empty, resources to their initial full values, AI state/targets/cooldowns to initial values, camps to Pending (activated in Playing), chests to unclaimed future rewards, and Kings to full health. Base attributes and configuration come from the authored prefab rather than being erased. Reward deduplication also resets under the new progression session ID.

Normal death/respawn within a round still reuses the same avatar and preserves inventory, equipment and progression. The full recreation boundary applies only to round reset/return. The project uses Fusion's default provider, which creates fresh instances rather than pooling gameplay state. A future custom pooling provider must reset component state before reuse.

Old colliders are disabled before despawn because Unity's deferred destruction must not obstruct placement of the replacement players. New spawn selections still use the shared authoritative team spawn service.

## Validation

- `Tests/Standalone/RunMatchLifecycleRules.ps1`: 130 transition/readiness/roster checks, independent of Unity and Photon connectivity.
- `Tests/EditorCommands/MatchLifecycleValidation.cs`: transition and configuration checks, saved scene references and match prefab bake, executed through Unity MCP.
- `Tests/Standalone/RunMatchLifecycle.ps1`: development host/client acceptance over two rounds. Verifies the one-player lobby wait; progression, equipment and inventory reset; player/King health; AI and camp reset; opened chest removal; retained teams; replaced avatars/session IDs; frozen rewards/damage after finish; and replicated winners/events for both teams.
- Build the fortress scene as a Development Windows player at `Builds/MatchLifecycle/DragonlandLifecycle.exe` before running online acceptance.

## Change inventory

New runtime: `MatchPhase.cs`, `MatchLifecycleConfig.cs`, `NetworkRoundReset.cs`. New authoring: `MatchLifecycleSceneBuilder.cs`. New validation: `MultiplayerLifecycleProbe.cs`, `MatchLifecycleValidation.cs`, `MatchLifecycleRulesValidation.cs`, `RunMatchLifecycle.ps1`, `RunMatchLifecycleRules.ps1`.

Updated runtime: `NetworkMatch.cs` (phase coordinator and replicated lobby), `MultiplayerBootstrap.cs` and `NetworkTeamRoster.cs` (preserve roster across avatar recreation), `TeamSpawnService.cs` (round reservations), `NetworkEnemySpawner.cs` and `NetworkCampSpawner.cs` (round factories), `NetworkCharacterStats.cs` and `NetworkEnemy.cs` (safe initialization outside Playing), `NetworkTestPlayer.cs` (movement gate), `NetworkLootChest.cs` (subscribe before gameplay activation), `NetworkFortressPresentation.cs` (lobby/countdown/result UI). Updated authoring: `MultiplayerFortressSceneBuilder.cs`. Existing fortress and respawn probes now wait for Playing before exercising combat.

No assets deleted and no automatic commits. New scripts keep their Unity-generated metadata.
