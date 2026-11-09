# Team spawn areas

## Completion verification (2026-11-06)

The earlier Photon connectivity limitation is resolved. All 76 Editor checks passed again through Unity MCP. Six-peer acceptance passed against the fresh lifecycle build (`Builds/MatchLifecycle/DragonlandLifecycle.exe`), including queued joins, distinct own-team spawn points, reservations, server authority and actual protected-zone walk/dodge rejection. Logs: `Builds/TeamSpawnAcceptance-59d4a795/`.

Respawn regression on that same build also passed: `Builds/RespawnAcceptance-3b56d0b0/`. The final Editor Console error inspection was clean. These results supersede the failed September online attempts documented below. See [match lifecycle completion](MatchLifecycle.md) for the integrated milestone report.

Implemented in `Assets/Scenes/MultiplayerFortressTest.unity`. Team A and Team B each have a `TeamSpawnArea` with six child spawn transforms inside their own fortress. Both initial joining and respawning use the server-owned `TeamSpawnService`. Earlier test scenes without fortresses retain their existing placement.

## Placement

- The server cycles through the team's points, checks capsule obstruction and reserves successful selections for the current Fusion tick. Clients cannot choose a point.
- The player capsule must fit inside the team's authored footprint. Disabled, missing, out-of-area or non-child points are rejected. Missing or duplicate areas for a team reject placement.
- If every point is occupied, joining players keep their assigned team and wait for a free point. Respawning players remain dead until a point is available. Neither path falls back to the other fortress or an occupied location.
- Disconnect removes pending joins and releases the roster slot. Existing respawn state preservation, timers and match-end cancellation remain in place.

## Inspector configuration

Select `TeamA Fortress Spawn Area` or `TeamB Fortress Spawn Area` in the fortress scene. Set its Team, Size, Points and Rules. Move the child points to change the exact spawn poses; their rotation controls facing. Keep at least one full capsule of clearance around each point and preserve space between them. Select the bootstrap's `TeamSpawnService` to inspect its Areas and Obstruction Mask.

`Assets/Game/Data/MultiplayerFortress/SpawnZoneRules.asset` is shared by both saved areas. Assign separate rule assets to configure teams independently.

- **Protected Zone** defaults to off. When enabled, opposing and unassigned NPCs cannot spawn within the footprint, including their radius. Friendly NPCs are allowed. Checks cover normal network enemies, camp enemies, fortress guards and kings.
- **Enemy Player Entry** supports `Allow` and `BlockEntry`. It applies only when protection is enabled. `BlockEntry` checks the movement segment on the server, including dodge movement, preventing crossing through the zone in one step. Existing occupants can leave; there is no forced ejection.
- The footprint is planar and extends vertically. Protection does not prevent existing enemies from attacking or walking in, and does not grant invulnerability. The existing timed respawn invulnerability is a separate setting in `SessionRespawn.asset`.

These are configurable prototype rules. No damage, duration, punishment, forced ejection or final balance values were added for the zone.

`TeamSpawnAreaSceneBuilder.Install()` installs and saves the areas through Unity Editor APIs; the fortress builder invokes it. Existing areas and rule assets are preserved when installing again.

## Validation

- `Tests/EditorCommands/TeamSpawnAreaValidation.cs`: 76 checks passed through Unity MCP on 2026-09-14 (saved references, six points per team, containment and spacing, invalid team, enemy and neutral spawn denial, friendly allowance, movement crossing, exit and optional rules).
- `Tests/EditorCommands/SessionRespawnValidation.cs`: 40 checks passed against the integrated scene and prefab.
- Existing development player: `Builds/TeamSpawns/DragonlandTeamSpawns.exe`; build result reports success with zero errors and 510 warnings.
- `Tests/Standalone/RunTeamSpawns.ps1` exercises six peers, blocked-team join waiting, distinct team points, tick reservations, client rejection, NPC policy and actual walk/dodge entry blocking.
- `Tests/Standalone/RunMultiplayerRespawn.ps1 -Executable Builds/TeamSpawns/DragonlandTeamSpawns.exe` exercises both teams' repeated respawns through the shared selector.

Current acceptance limitation (2026-09-14): online runs were attempted with network access, but Photon Cloud returned `PhotonCloudTimeout` / `OperationTimeout` before all peers connected. The six-peer and respawn acceptance runs have therefore **not passed on this build**. Logs: `Builds/TeamSpawnAcceptance-9aa523b6/`, `Builds/TeamSpawnAcceptance-eb73ae2e/`, `Builds/RespawnAcceptance-1061d879/`. Rerun the two scripts when connectivity is available, then visually check ordinary spawning and respawning in the fortress scene. Final Editor Console check reported zero errors; existing compilation and MCP warnings remain.

## Files in this feature

New runtime: `Assets/Game/World/TeamSpawnArea.cs`, `TeamSpawnService.cs`, `SpawnZoneRules.cs`. New authoring: `Assets/Game/World/Editor/TeamSpawnAreaSceneBuilder.cs`. New asset: `Assets/Game/Data/MultiplayerFortress/SpawnZoneRules.asset`, plus metadata.

Integration changes: `MultiplayerBootstrap.cs`, `NetworkTestPlayer.cs`, `NetworkFortressSpawner.cs`, `NetworkEnemySpawner.cs`, `NetworkMatch.cs`, `NetworkCamp.cs`, `MultiplayerFortressSceneBuilder.cs`, and `MultiplayerFortressTest.unity`.

Validation: `MultiplayerTeamSpawnProbe.cs`, `TeamSpawnAreaValidation.cs`, `RunTeamSpawns.ps1`, with the existing respawn probe and validation updated to use authored points. Documentation: this file and `SessionRespawn.md`. No assets deleted; no commits created.
