# 3v3 balance pass

This is a playtest baseline for a 20–30 minute match, not a validated match-duration guarantee. The scene remains an 88 × 88 metre primitive blockout.

## Objectives and income

| Camp | Count | First appearance | Respawn after clear | XP per nearby living player | Chest drops |
|---|---:|---:|---:|---:|---:|
| Home / low | 4 | Match start | 75 seconds | 25 | 1 |
| Flank / medium | 4 | Match start | 105 seconds | 50 | 1–2 |
| Center / high | 1 | 150 seconds after Playing starts | 165 seconds | 90 | 2–3 |

XP requires presence within nine metres and still includes opponents. It is not divided between recipients. Center rarity weighting is reduced to 0.3 / 1 / 1.5 / 0.5 / 0.05 for common through legendary. Repeat rewards use distinct server-owned cycle IDs, while duplicate completion of a cycle remains rejected.

Camp locations are (±33, ±18) for low camps and (±10, ±34) for medium camps. Center remains (0, 0). Home camps have access toward fortress flank exits; the ridge between home and medium camps makes invasion a travel commitment. Medium income from a pair of flank camps exceeds one center clear, and returns sooner, giving teams a reason to concede center temporarily.

The respawn deadline starts when enemies are cleared, independent of chest collection. At that deadline the previous chest, including uncollected contents, and the previous enemies are despawned. Camp presentation shows the countdown. Rewards already deposited in inventories persist. Faulted initialization remains visible as a fault rather than silently granting rewards or looping spawns. Old camp definitions default to one-time immediate encounters.

## Routes and visibility

Paired solid ridges at x=±15 separate center from fortress gates. Four further ridges at x=±21, z=±26 separate home and flank farming areas. These use colliders and Not Walkable NavMesh modifiers; they affect actual movement. The original gates, flank exits, spawn areas and terrain remain intact.

Measured after baking at movement speed 5:

| Route | Navigation estimate |
|---|---:|
| Spawn to center | 9.0–10.2 seconds |
| Spawn to home camps | 2.8–4.7 seconds |
| Home to neighboring flank camp | 7.2 seconds |
| Flank camp to center | 7.6 seconds |
| Center to enemy main gate | 7.6 seconds |
| Opposing flank camps on the same edge | 4.0 seconds |

Geometry is rotationally paired. NavMesh polygon A* selects different detours on the two spawn-to-center queries (about six metres difference); paired home/flank/gate routes differ by less than a metre. Player movement uses physics sweeps rather than NavMesh autopilot. Human route timing, particularly spawn-to-center parity, remains a playtest check.

Live matches use the local follow camera. M can inspect the overview outside Playing, but cannot reveal the entire battlefield during play, including while dead. There is no new fog of war, bush concealment or server-side visibility filter. Ambushes currently rely on approach angles and obstruction; vegetation must not imply a stealth mechanic. NPCs and players remain replicated normally.

## Siege and recovery

The battlefield has its own respawn configuration. Death delay is five seconds until minute 10, interpolates to ten seconds at minute 15 and fifteen seconds at minute 20, and caps there. The server snapshots that delay at death. The three-second spawn protection and inventory/progression retention remain in place. Other test scenes retain their existing respawn settings.

Kings retain 1,000 HP. Base melee deals 25 damage with a 0.9-second attack/recovery cycle: three attackers have an ideal no-resource-limit ceiling of about 83 damage/second, or roughly 12 seconds to remove 1,000 HP. Real siege is slower with stamina, guard pressure, positioning and obstruction, and faster with equipment/skills. Therefore the ramp is deliberately capped at fifteen seconds instead of introducing a large late wipe penalty and changing king HP simultaneously.

Human 3v3 tests must measure solo and coordinated siege duration with representative late equipment, whether guards/respawns cause stalemates, and whether a single wipe wins too reliably. Adjust king durability and the respawn cap from those results together. The current cap is provisional.

## Validation

`BattlefieldBalancePass.Validate()` checks complete camp/base/gate routes, mirrored route connectivity, all enemy spawn offsets and the respawn ramp. `BattlefieldBalanceProbe` is opt-in with `-hodrBalance`; it accelerates server timer deadlines without changing saved configuration and checks delayed center activation, repeated XP, idempotency, enemy reactivation and expired-chest cleanup.

Run `Tests/Standalone/RunBattlefieldBalance.ps1 -Executable Builds/BattlefieldBalance/DragonlandBattlefield.exe` against a fresh development build. It starts a host and five clients, exercises the existing 3v3 startup/movement acceptance and the recurring-objective integration checks. Normal matches never execute the probe.

Before final environment art, measure first contact, player travel fraction, farm distribution, center control versus win rate, comeback after a lost center fight, 2+1 versus three-player grouping, siege duration and total match length across human matches.

Verified on 2026-11-07: editor route/anchor/ramp checks passed; the development build succeeded with zero errors and 527 warnings (project/package/shader and development-probe warnings). Host plus five clients passed the movement and balance integration checks. Logs: `Builds/BattlefieldAcceptance-5a872da2/`. The final Unity Console error query returned zero entries. The scene is saved in Edit Mode. Timers were accelerated for cycle integration; no full-duration human match or siege playtest has been claimed.

## Files changed in this pass

Modified existing files (some were already untracked or modified before this task):

- `Assets/Game/World/Camps/NetworkCamp.cs`, `NetworkCampDefinition.cs`: opt-in recurring schedule, cycle reward identity, lifecycle cleanup.
- `Assets/Game/Core/NetworkMatch.cs`: authoritative playing time, reset per round.
- `Assets/Game/Player/NetworkPlayerRespawn.cs`, `SessionRespawnConfig.cs`: configurable late respawn ramp.
- `Assets/Game/UI/NetworkCampPresentation.cs`: initial/respawn countdown.
- `Assets/Game/World/BattlefieldCamera.cs`: local view during live play.
- `Assets/Game/World/BattlefieldAcceptanceProbe.cs`: revised initial camp and reward assertions.
- `Assets/Game/World/Editor/BattlefieldSceneBuilder.cs`: existing validation recognizes the balanced map.
- `Assets/Scenes/DragonlandBattlefield.unity`: camp anchors/markers, barriers, respawn reference, opt-in probe.
- `Assets/Game/Data/Battlefield/{Low,Medium,High}Camp.asset`, `{Low,Medium,High}Loot.asset`, `BattlefieldNavMesh.asset`: timing, income, navigation.
- `Tests/Standalone/RunBattlefield.ps1`: updated success description.
- `Documentation/Battlefield.md`: link to the current balance specification.

Created files:

- `Assets/Game/World/Editor/BattlefieldBalancePass.cs` and `.meta`: guarded additive authoring pass and navigation validation.
- `Assets/Game/World/BattlefieldBalanceProbe.cs` and `.meta`: opt-in runtime integration checks.
- `Assets/Game/Data/Battlefield/BattlefieldRespawn.asset` and `.meta`: map-specific respawn settings.
- `Tests/Standalone/RunBattlefieldBalance.ps1`: six-process integration runner.
- `Documentation/BattlefieldBalance.md`: decisions, measurements and test record.

No assets or files deleted; no commits created. The architectural change is an opt-in schedule on existing camp definitions/instances and an optional ramp on existing respawn configuration; there is no new gameplay manager or visibility system. Build/MCP request files under `Builds/` are local verification artifacts.
