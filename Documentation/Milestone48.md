# Milestone 48 - Fortress assault

Both fortresses in `Assets/Scenes/DragonlandBattlefield.unity` now have layered assault gameplay. Existing battlefield cross-connections and both sally ports remain; no lane spawning, forced gate-first objective lock, or single mandatory approach was added.

## Layout and approaches

The fortresses remain rotationally mirrored at x=-33 / +33. Local +X points toward the battlefield. Each fortress has:

- An outer defensive zone in front of the main entrance, with two low cover blocks and an outer guard.
- A team-owned 600 HP main gate occupying the original 6m opening.
- Two existing side/sally approaches with player-sized clearance. Attackers can use these while the main gate remains intact.
- An open inner courtyard that connects the main and side approaches.
- A King chamber recessed into the rear half of the fortress, with solid front/side cover, a courtyard doorway and a separate north-side doorway.

The King moves from local (-3,0,0) to (-5,0,7): Team A (-38,0,7), Team B (38,0,-7). Chamber walls block straight ranged sightlines from the main and both side approaches. Reaching the King requires entering and turning through the layout; the main-gate breach is useful but not a prerequisite for a flank assault. King HP, team policies and match-ending authority remain unchanged. There is no temporary King invulnerability timer or guard-kill requirement. King protection comes from terrain, route length, health and stationed defenders.

The protected respawn footprints and all twelve authored spawn poses remain clear. The chamber occupies the opposite side of the courtyard from protected spawning. The open battlefield route graph, neutral camps and boss encounter are preserved.

## Gates

`FortressGate` handles team filtering and local collision/visual changes. `NetworkFortressGate` supplies the match-owned authoritative damage policy and replicated open/destruction state. Gates reuse `CharacterStats`, `NetworkCharacterStats`, `DamageReceiver`, `TeamIdentity` and `Targetable`; both melee and existing magic use their normal hit paths.

Friendly-team damage is always rejected, including a source that opts into friendly fire. Only living, admitted enemy-team player attacks from the same runner can deal network damage, and only during the active match while the real server is resolving an accepted melee or spell impact. Clients submit ordinary combat intent, not health, gate ownership or open-state claims. Direct replica damage/resource mutation is denied by the existing stats authority.

Gates start closed with 600 HP. Server death commits a one-way open state and one destruction count for that gate instance. The gate body disappears, its solid collider is disabled, and its carving NavMeshObstacle is disabled, reopening the main approach to players and NPC navigation. Clients apply the same removal from replicated state, including newly spawned replicas receiving an already-open snapshot. A gate remains a network object for state/late-join consistency until round cleanup. Repeated hits cannot repeat destruction.

Gates block both teams while closed. Friendly exits use the two side ports; a manual raise/lower interaction is outside this milestone. Breaching is permanent for that round. Target lock and gate overhead text show the existing replicated health, then BREACHED.

Static navigation is baked with the gate passage open; the spawned gate carves its closed footprint at runtime. Chamber walls and cover are baked through Unity's NavMesh APIs. The existing `BattlefieldNavMesh.asset` GUID is preserved while its data is updated. No raw Unity YAML was edited.

## Defensive guards

Each fortress has four guards, all using the existing shared EnemyBrain, Knight archetype blocking/weapon behavior, EnemyNavigation, EnemyPerception, NetworkEnemy and NetworkFortressGuard team policies.

| Post | HP | Detection | Home leash | Damage |
| --- | ---: | ---: | ---: | ---: |
| Outer entrance | 140 | 8m | 6.5m | 16 |
| North sally approach | 140 | 6m | 5m | 16 |
| South sally approach | 140 | 6m | 5m | 16 |
| Royal / chamber | 200 | 6m | 5.5m | 22 |

All have 0.3m patrol radii. Targets must remain within the post's leash; pulling a guard farther drops aggro, cancels its attack and returns it home. Guards are not map-wide pursuers. Their positions cover the gate, alternate entrances and final objective rather than forming a lane wave. The new definitions/prefabs are dedicated assault assets, so the older fortress test scene and generic enemy profiles are not retuned.

The legacy match-owned guard slots now spawn the two outer guards. FortressAssaultSpawner creates two gates and the six additional guard posts through the round lifecycle. It preflights posts and protected spawn rules before spawning. `NetworkRoundReset` invokes this spawner and includes gates in cleanup. Ending a round removes the old defenses; the next round creates new full-health, closed gates and fresh stationed guards. No independent second match manager or duplicate AI controller was added.

## Authoring

`Heroes of Dragonland > Battlefield > Install Fortress Assault` is an additive, repeatable Unity Editor setup for the active battlefield. It saves the mirrored geometry, gate/guard prefabs and definitions, battlefield match references and navigation. It preserves existing assets and metadata and explicitly rebuilds/reimports Fusion prefab registration.

`FortressAssaultDefinition` stores the gate and additional guard poses/prefabs. Baseline gate stats and the three guard profiles are ScriptableObjects under `Assets/Game/Data/FortressAssault/`. Core generation values are in the setup source; later balance can be edited directly in these assets without rerunning baseline setup.

Top-down previews of both layouts were captured and visually inspected: `Builds/Milestone48/TeamA-fortress.png` and `TeamB-fortress.png`. Existing primitive/environment art is reused. Dedicated castle art, gate destruction animation and audio are not new assets in this milestone.

## Validation

`Heroes of Dragonland > Battlefield > Validate Fortress Assault In Play Mode` passed **69 assertions**: mirrored route connectivity, main/side approach sightline shielding, side-port capsule clearance, all guard post navigation, short leashes, clear protected spawn poses, friendly-fire denial, enemy damage, lethal breach, blocker removal, repeat damage denial, unspawned/replica fail-closed behavior, local guard engagement and leash return. The existing **63 neutral-boss assertions** and **35 archetype assertions** also pass with the new battlefield layout. Temporary validation objects are cleaned up.

Windows development build: succeeded with zero errors. Summary is in `Builds/Milestone48/build-result.txt`; existing package/API/compute-shader and optional Pipeline configuration warnings remain.

The opt-in `-hodrAssaultCheck` acceptance probe runs a real host/client session with four roster bots. It verifies eight defensive posts and uses normal owning-player Fire Projectile requests to prove friendly damage rejection and enemy damage against each team's gate. The server fast-forwards only the remaining gate HP after observing a genuine enemy hit, so ordinary owner projectile hits still cause each actual breach. The fixture pauses AI to isolate gate/network checks; it does not claim to validate combat difficulty.

Both peers verify replicated gate health, one destruction per gate, collision/obstacle removal and denied replica mutation. The fixture then triggers the normal King-death/match-finish pipeline and verifies the next round removes old gate IDs and restores both gates to closed, full-health state. Host/client log paths and process IDs are recorded in `Builds/m48-acceptance-processes.json`. Acceptance processes are stopped after verification. Human playtesting remains for coordinated breach versus flank balance and guard strength.

Resume verification (2026-11-13): all 69 Play Mode assertions passed again; the rebuilt Windows development player succeeded with zero errors (589 existing warnings), and the Unity Console error/exception query was empty. The original acceptance run had passed on the host but timed out on the client because the round reset could remove the breached gate before a background client rendered it. The opt-in fixture now holds both breached gates for 20 seconds before finishing the round and allows 150 seconds for the encounter. A fresh host/client run passed on both peers, including the full round reset; logs are `Builds/AssaultAcceptance-c49572af/host-check.log` and `client-check.log`. This timing change affects only the opt-in test fixture, not ordinary match timing.

## Files changed

New runtime: `World/FortressGate.cs`, `NetworkFortressGate.cs`, `FortressAssaultDefinition.cs`, `FortressAssaultSpawner.cs`, `FortressAssaultNetworkProbe.cs`, and `UI/FortressGatePresentation.cs`.

New authoring/validation: `World/Editor/FortressAssaultSetup.cs`, `FortressAssaultValidation.cs`.

Modified runtime: `Core/NetworkRoundReset.cs` (assault spawning and gate cleanup).

New content under `Data/FortressAssault/`: assault definition, gate stats, three guard definitions and stats, two gate prefabs and six team/role guard prefabs, plus Unity metadata. Modified content: `Data/Battlefield/BattlefieldMatch.prefab`, `BattlefieldNavMesh.asset`, and `Scenes/DragonlandBattlefield.unity`.

No assets deleted; no commits created. Existing Milestone 45-47 changes remain in the working tree. Scenes and assets are saved through Unity MCP/Editor APIs.
