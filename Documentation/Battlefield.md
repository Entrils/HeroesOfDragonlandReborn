# First playable battlefield

**Current balance:** see [3v3 balance pass](BattlefieldBalance.md) for the revised camp positions, recurring timers, rewards, terrain barriers, camera rules, respawn ramp and validation. The layout and verification notes below describe the original blockout baseline, before that pass.

Open `Assets/Scenes/DragonlandBattlefield.unity`. Host and join the same room using the existing multiplayer panel. The battlefield starts with six players, assigned 3v3. Existing combat, dodge, loot interaction, inventory (**I**), skills (**K**), progression, respawn and king victory rules remain connected. **M** toggles the local follow camera and map overview.

All environment geometry is Unity primitives with flat placeholder materials. No final art, terrain sculpting, stealth system or fog of war is introduced.

## Layout

The ground is an 88 by 88 metre square, bounded by physical walls. Team A's fortress is on the west and Team B's on the east. Their kings stand at (-36, 0) and (36, 0). Each courtyard has a six-metre main gate, two flank exits, a guard, and a separate protected spawn footprint with six clear spawn poses. Enemy players cannot enter the small spawn footprint; the king remains outside it and attackable.

Mechanical geometry, access and rewards are paired under a 180-degree rotation. Forest pockets occupy northwest/southeast, rocky highlands northeast/southwest, the ruined battlefield spans the center, and corrupted reaches occupy the north/south cross routes. Rocky height comes from non-traversable outcrops: the existing planar player motor remains on flat walkable ground.

Trails mark an interconnected open field, not confined lanes. The fast central approach intersects the valuable PvE camp. Inner north/south detours bypass its immediate aggro, outer routes use the fortress flank exits, and cross links allow rotations between camps and approaches. Trees, rocky outcrops, broken walls and corrupted stones interrupt direct sightlines and provide several ambush/retreat angles. Camp rings mark locations without forming safe enclosures.

| Tier | Count | Positions (x, z) | Encounter | Reward |
| --- | --- | --- | --- | --- |
| Low | 4 | (±25, ±18) | Two wolves, 40 HP each | 40 XP; 1–2 drops, common/uncommon bias |
| Medium | 4 | (±12, ±25) | Bear and wolf, 80 HP each | 90 XP; 3–4 drops, improved rarity |
| High | 1 | (0, 0) | Two knights and bear, 120 HP each | 240 XP; 5–6 drops, strongest rarity weighting |

XP is shared with living players within 12 metres when a camp clears, including nearby opponents. The chest uses the existing authoritative first-claim rules. New battlefield definitions opt into proximity XP, while a zero radius preserves existing test-scene reward behavior. Camps and loot reset each round; there is no timed camp respawn yet.

At five metres per second, the direct fortress-to-center trip is roughly seven seconds before fighting or detours. Outer routes trade more travel for bypassing the central encounter. Final balance, camera visibility and camp tuning require human 3v3 playtesting.

## Implementation and checks

- `BattlefieldSceneBuilder` authors the new scene, copied player/match prefabs, three camp tiers, loot tables, enemy profiles, spawn rules, primitive materials and baked NavMesh through Unity MCP. It refuses to overwrite an existing authored battlefield.
- `BattlefieldCamera` provides a local follow view and an overview toggle without affecting network gameplay.
- `NetworkCampDefinition` adds an optional XP radius; `NetworkCamp` checks distance and life state on the server.
- Editor validation checks clear spawn capsules, navigable camp anchors, complete center/inner/outer routes, paired access distances and a bound on center travel.
- `BattlefieldAcceptanceProbe` and `Tests/Standalone/RunBattlefield.ps1` provide an opt-in six-process smoke test (`-hodrBattlefield`) for actual 3v3 startup, all nine camps, reward tiers, ownership and movement through the authored geometry. Test movement is disabled in normal games.

Source fortress/camp scenes and their existing data are preserved. All new map-specific assets live under `Assets/Game/Data/Battlefield/`, `Assets/Art/Materials/Battlefield/` and the new battlefield scene.

Verified on 2026-11-07:

- Unity MCP created/saved the scene, prefab copies, materials, camp definitions and NavMesh.
- Editor validation passed with the battlefield loaded alone: all spawn capsules clear, camp anchors navigable, complete center/inner/outer connections, paired access within two metres. Measured base-to-center distance: 36 metres / 7.2 seconds at speed 5.
- The top-down overview was rendered and visually reviewed: `Builds/battlefield-overview.png`.
- Windows development build: `Builds/Battlefield/DragonlandBattlefield.exe`; succeeded with zero errors and 550 warnings (project/package, shader and development-probe warnings).
- Six-process acceptance passed, including a real 3v3 match, all nine active camps and all six authoritative players walking out through their own fortresses. Logs: `Builds/BattlefieldAcceptance-2101eb8c/`.
- MCP Console query returned zero errors. The battlefield is left open in Edit Mode for review.

Manual verification remaining: human 3v3 playtesting for combat pacing, camera visibility around cover, camp risk/reward and perceived team balance. The smoke test proves startup and movement, not competitive balance.
