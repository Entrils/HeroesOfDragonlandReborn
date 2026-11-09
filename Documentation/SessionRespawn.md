# Session respawn

## Integrated regression (2026-11-06)

All 40 Editor checks passed again through Unity MCP. Host/client acceptance passed against the fresh match lifecycle build (`Builds/MatchLifecycle/DragonlandLifecycle.exe`): two revival cycles for both teams, preserved inventory/equipment/progression, restored resources, spawn protection, replicated events and cancellation when the match finishes. Logs: `Builds/RespawnAcceptance-3b56d0b0/`. This confirms respawn remains functional with automatic round reset enabled. See [match lifecycle completion](MatchLifecycle.md).

Fortress players revive on the same server-owned avatar. Death does not despawn the player or change their team/roster slot. Inventory, equipped items, level, allocated attributes and session reward history remain on the existing components.

## Configuration

`Assets/Game/Data/MultiplayerFortress/SessionRespawn.asset` provides a 5-second delay and 3 seconds of spawn protection by default. Mana and Stamina each independently support `Full`, `Preserve` (the amount at death, including its remaining regeneration delay), and `Empty`. Both default to Full. Health always restores to its current maximum, including equipment and attribute bonuses. Normal resource regeneration resumes after revival.

`NetworkFortressSpawner` stores the respawn configuration and delegates placement to `TeamSpawnService`, also used for initial joins. The fortress scene has six authored points per team, facing into the arena. The server skips occupied points and reserves selections for the current simulation tick. If no point is available, the dead avatar waits and retries without moving to an opposing fortress or overlapping another player. See [Team spawn areas](TeamSpawnAreas.md) for authoring and zone settings.

`NetworkPlayerRespawn` schedules and executes revival only on the authoritative server for a registered player. No client respawn RPC exists. Fusion tick timers replicate waiting/protection state. Teleport moves the existing NetworkTransform without interpolating a trip across the map. Pending melee/dodge actions cancel on death. Protection blocks incoming damage through the shared invulnerability interface; it lasts the configured duration and does not prevent movement or attacking. Initial joins do not receive respawn protection.

Match end cancels pending respawns and clears their countdown. Disconnect uses the existing player despawn and roster cleanup. Earlier multiplayer scenes retain their existing player prefab; the fortress scene uses the new `NetworkFortressPlayer.prefab`.

## UI integration (no UI created)

Subscribe to `NetworkPlayerRespawn.Died`, `RespawnCountdownChanged(float seconds)` and `Respawned`. They are local notifications driven by server state, available on the host and client replicas. Filter by `HasInputAuthority` for the local player's UI. Countdown changes follow the network clock and reach zero when complete or cancelled; inspect `WaitingForRespawn` to distinguish a pending timer from cancellation. Read `SecondsUntilRespawn`, `WaitingForRespawn` and `IsSpawnProtected` immediately when attaching a view. Late joiners read current state without replaying historical death/respawn events. Sequence counters allow consumers to detect snapshot changes. Observers must not perform gameplay mutations.

## Validation

- `Tests/EditorCommands/SessionRespawnValidation.cs`: saved scene/prefab/Fusion references, both teams' three positions, invalid team rejection and all nine Mana/Stamina policy combinations.
- `Tests/Standalone/RunMultiplayerRespawn.ps1`: host/client acceptance using the opt-in `-hodrRespawn` development probe. Seeds equipment, inventory, XP and an attribute; runs two deaths/revivals for both teams; verifies full resources, same registered avatar, preserved state, protection and expiry, replicated UI events, client authority rejection and cancellation of a third respawn at match end.
- Build the fortress scene as a Development Windows player to `Builds/MultiplayerRespawn/DragonlandRespawn.exe` before running the standalone test.

Verified 2026-09-14: 40 respawn checks and 43 existing fortress prefab checks passed. The final development build succeeded with zero errors (21 existing package/development-build warnings). Final host/client acceptance passed; logs: `Builds/RespawnAcceptance-79f57d43/`. Unity Console error check was clean, and the Editor remained in Edit Mode with no unsaved scene changes. Manual visual testing of normal player controls after respawn is still recommended; acceptance runs are headless.

## Files

New runtime: `NetworkPlayerRespawn.cs`, `SessionRespawnConfig.cs`. New validation: `MultiplayerRespawnProbe.cs`, `SessionRespawnValidation.cs`, `RunMultiplayerRespawn.ps1`, and this document.

Modified runtime: `CharacterStats.cs` (resource restoration), `NetworkPlayerCombat.cs` and `NetworkPlayerDodge.cs` (cancel pending actions), `NetworkFortressSpawner.cs` (configuration and team poses), `NetworkActionPresentation.cs` (hide cancelled actions, including zero-delay revivals). Modified authoring: `MultiplayerFortressSceneBuilder.cs` installs the configuration, isolated player prefab and opt-in test probe.

New assets: `SessionRespawn.asset` and `NetworkFortressPlayer.prefab` in `Assets/Game/Data/MultiplayerFortress/`, plus Unity metadata. Updated `Assets/Scenes/MultiplayerFortressTest.unity` references the new player and settings. No assets deleted; no final UI added.
