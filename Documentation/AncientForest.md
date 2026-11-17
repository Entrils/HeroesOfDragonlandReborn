# Milestone 56 — Ancient Forest

<!-- HOME-INTEGRATION-2026-11-17 -->
**Home integration update (2026-11-17):** Asset validator and conservative battlefield pass completed through MCP. Both forest approaches were rendered and reviewed, original collider/camp invariants passed, and scene saved. Final solid-prop art placement and combined combat readability remain open.

See [the live validation record](HomeIntegrationValidation.md) for actual outcomes, failures, evidence and remaining checks. Earlier offline-only status below is historical.

## Delivery and acceptance status

Ancient Forest applies the approved [Weathered Dragonland direction](ArtDirection.md): aged pale bark, muted olive leaves, gray-green stone, readable warm dirt paths, restrained moss and sparse low vegetation. It is the requested forest biome treatment within the existing map; the earlier name Ashen Borderwood describes the same art theme, not another required region or a layout change.

**Delivered here:** original editable forest assets, tileable PBR maps, tiered mesh LODs, collision policies, a Blender look-development patch, explicit Unity asset-building and battlefield-integration commands, density/performance rules and exported-file validation. These are **production candidates**, not a claim of an approved production-quality biome in the game. Human visual approval, Unity import/compilation, actual battlefield replacement, six-player performance measurements and gameplay acceptance are pending because this computer has no Unity Editor/MCP. The saved gameplay map is unchanged.

AGENTS.md and ArtDirection.md were read. Existing scene authoring code, the saved scene's object names, camera, target lock, camp spawning and balance documentation were inspected read-only. DESIGN.md is still absent. No third-party assets were downloaded, no new gameplay system is added and M52/M53 remain deferred.

## Inventory and file layout

| Category | Delivered source/export modules | Intended use |
| --- | --- | --- |
| Living trees | TreeAncient01, TreeAncient02 | Bent tapered trunks, forks, layered opaque folded-leaf crowns |
| Dead trees | DeadTree01, DeadTree02 | Bare branching silhouettes; same ground/pivot convention |
| Rocks | Rock01, Rock02, Rock03 | Three irregular closed rock shapes; no walkable high ground implied |
| Bushes | Bush01, Bush02 | Broad low leaf clusters, approximately 0.7m tall |
| Grass | GrassClump01, GrassClump02 | Opaque geometry blades, 0.08–0.22m tall |
| Fallen wood | FallenLog01, Stump01 | Bark and visible lighter cut ends |
| Forest ruins | RuinWall01, RuinPillar01 | Original irregular stone courses and integrated moss-colored upper faces |
| Camp landmark | CampCairn01 | Neutral stacked-stone landmark; not a new camp or reward mechanic |
| Ground | ForestFloorTile, DirtPathTile | Flat 4×4m floor and 3.2×4m path visual modules |
| Background | DistantTreeline | Deliberately simple distant geometry, never close combat cover |
| Camp props reused | M55 Crate and Barrel | Keep existing kit sources/exports and prefab identity |
| Ambient | Soft 64px mote texture and Unity particle-prefab builder | Sparse non-emissive dust/pollen; no gameplay/network component |

There are **19 new AF model assets**, each with an individual `.blend` and FBX. Vegetation, rocks and fallen wood have three mesh LODs; the already small cairn and two ruin modules have two LODs, avoiding an identical redundant third level. The two flat ground tiles have one mesh each. M55 camp props remain their own assets, rather than being duplicated into the forest library.

```text
ArtSource/Environment/AncientForest/
  SM_AF_<Module>.blend
  AncientForest_LookDev.blend
  AncientForest.manifest.json
  AncientForest.validation.json
Assets/Art/Environment/Models/AncientForest/SM_AF_<Module>.fbx
Assets/Art/Textures/Environment/AncientForest/T_AF_<Surface>_<Map>.png
Tools/ArtPipeline/create_ancient_forest.py
Tools/ArtPipeline/validate_ancient_forest.py
Assets/Game/Art/Editor/AncientForestAssetBuilder.cs
Assets/Game/Art/Editor/AncientForestBattlefieldPass.cs
Documentation/AncientForest/Overview.png
Documentation/AncientForest/PathView.png
```

`AF` means Ancient Forest. Per-asset `EXPORT` roots contain `SM_AF_<Module>_LOD0/1/2`, optional `COL_<Module>` and Front/Right/Up unit markers. LOD1/2 and collision are hidden in the saved Blender viewport/render, but included in FBX deliberately. All module transforms are applied and root/pivot is zero. Blender +Y front/+Z up follows the established export contract to Unity +Z front/+Y up; no compensating prefab scale is allowed. Ground pivots are surface-center at height zero. Logs, rocks and vegetation use ground-center origins; visual bounds are measured in the manifest.

## Biome material palette

| Shared material | sRGB swatch | Roughness | Runtime maps |
| --- | --- | --- | --- |
| M_AF_Bark | #A19C87 | 0.87 | BaseColor, Normal, Mask |
| M_AF_Leaves | #657354 | 0.90 | Constant color, opaque geometry |
| M_AF_LeavesLight | #89936D | 0.90 | Sparse leaf variation; no emission |
| M_AF_Rock | #8C9187 | 0.90 | BaseColor, Normal, Mask |
| M_AF_ForestFloor | #6C7058 | 0.93 | BaseColor, Normal, Mask |
| M_AF_DirtPath | #8E8165 | 0.93 | BaseColor, Normal, Mask |
| M_AF_Heartwood | #9A8464 | 0.85 | Cut ends / small woody details |
| M_AF_Moss | #67774E | 0.95 | Upper ruin faces and grass blades |

Four shared 512×512 texture sets supply twelve PNGs, plus one 64×64 RGBA ambient-mote texture. Base color contains no baked lighting. The procedural ground/rock fields sample noise on a torus for periodic U/V wrapping; bark has periodic vertical grooves with noise variation. Normal maps use +Y tangent convention. Masks follow ArtPipeline.md: linear R metallic=0, G AO=1, B reserved=0, A smoothness=1−roughness. This intentionally avoids artificial baked AO in ground textures. Normal strength starts at 0.6.

URP Lit materials are external shared assets built in Unity. Textured materials use white tint; the albedo map owns their color. Leaf/grass/moss materials disable backface culling and remain opaque: no alpha-card carpet, transparent bush layers or emissive foliage. This improves predictable coverage but is a stylized construction choice requiring visual review, not a photorealistic forest claim.

The biome does not use azure, vermilion or bright warning orange in decorative foliage. Path/ground contrast is deliberate; verify actual values under URP daylight, shadows and grayscale alongside both team avatars before approval.

## Geometry, LOD and collision

Living trees currently use approximately 2,824 / 824 / 246 triangles across LOD0/1/2. Bark tubes keep cylindrical UVs; cap/leaf/stone UVs are unwrapped before explicit triangulation. Low-cost opaque leaf blades provide the close silhouette; LOD2 uses broad canopy masses. DistantTreeline is an additional background placeholder, not an LOD for a nearby living tree.

Initial Unity screen-relative LOD transitions: LOD0→1 at 0.20, LOD1→2 at 0.07, cull at 0.012. Two-level small stone assemblies transition at 0.20 and cull at 0.012. Ground tiles have no LODGroup and remain visible. Thresholds are candidates for camera/performance review; visible silhouette popping is not accepted merely because triangle counts decrease. LOD2, ground and decorative vegetation have shadow casting disabled by the builder; living-tree close/middle LODs retain it.

| Asset type | Standalone prefab collision | Battlefield treatment |
| --- | --- | --- |
| Living/dead tree | Capsule, radius 0.425m, height 3.6m, center Y=1.8m | Remove the new visual instance's collider; retain original map tree collider exactly |
| Rock/log/stump/ruin/cairn | Separate low-detail non-convex static MeshCollider | Do not scatter as new blockers automatically; place only after cover/navigation review |
| Bush/grass | None | Decorative only; never blocks movement, camera or lock-on rays |
| Ground/path tile | None | Render overlay; existing traversal ground stays authoritative |
| DistantTreeline | None | Background only, outside playable/camera space |
| Ambient | None | Cosmetic particle system only |

No prefab has a Rigidbody or network component. MeshCollider props are static; dynamic use requires a reviewed convex/primitive variant. Collider validity is independent of visual LOD/culling. The standalone Unity look-development scene gets one separate flat test-ground BoxCollider; that collider is not part of either ground tile and is never added to the battlefield.

Living tree LOD0 crowns stay within the existing placeholder canopy's 1.7m horizontal radius. Main forks/foliage start above the critical near-ground sightline, around 3.6m. This does not replace a third-person orbit/lock-on test: visual occlusion can change even with identical collider bounds. Dead trees are not substituted for established live-tree cover automatically.

## Existing map integration contract

The original map has two rotationally paired forest floor rectangles: northwest center (-23,25), southeast center (23,-25), each 38×34m. Six tree pockets per half contain three existing trunk/canopy pairs, for 36 trees total. Existing trails are 3.2m wide. The balance pass moved camp anchors after the first blockout; never restore positions from the obsolete initial Battlefield.md table. Read the current serialized NetworkCampSpawner entries and actual scene through MCP instead.

`AncientForestBattlefieldPass.Apply()` is an explicit Editor/MCP command, **not an automatic importer or runtime generator**. It requires a saved active DragonlandBattlefield scene and checks the expected 36 trunks/canopies and two floor renderers. It:

1. Creates one named visual root, hides the old tree/canopy renderers with Undo support, and instantiates forest tree visuals at the original ground anchors and rotations. Old objects, colliders, NavMesh modifiers and identity are retained.
2. Assigns forest-floor and dirt-path materials to the existing renderers, preserving mesh dimensions, route transforms, widths and height. Floor material tiling matches the 38×34m patch. Path tiling uses a small shared set of length buckets, avoiding a material per segment; this approximates texel density and must be reviewed on junctions.
3. Adds limited paired grass/bush decoration only outside actual trail footprints plus a conservative 1.8m center buffer, outside 10m center-distance from current serialized camp anchors, and away from existing tall collider bounds. Both mirrored candidate positions must pass exclusions. No colliders are added by this scatter.
4. Adds two small ambient emitters above the ground sightline. Re-running is refused if the named visual root exists.
5. Verifies serialized camp configuration is byte-for-byte unchanged and pre-existing collider count, bounds and enabled state are unchanged. Failures revert scene Undo changes. The pass marks the scene dirty for MCP review/save; it does not silently save the map or rebake navigation.

The pass deliberately does not scatter solid rocks/logs/ruins/camp props or add distant treelines inside the battlefield. Their approved placements require live scene/camera review, especially around camps, cover and boundary views. Those placements and the final saved map replacement remain **required unfinished M56 work**. Do not call the material/tree pass alone a completed production biome.

No camp anchor, spawn footprint, objective, timer, aggro/range, reward, damage, team identity, target-lock rule or navigation surface is redesigned. No map YAML has been edited on this computer.

## Vegetation density guidelines

These are starting authoring limits, not measured performance results.

| Zone | Guidance |
| --- | --- |
| Existing trail surface | Preserve the existing full 3.2m width; no new rocks, logs, grass or bush overlap |
| Trail shoulder | Keep at least 1m of low-noise clear visual ground beyond the path; use center buffers accounting for module radius |
| Camp/objective clearing | No automatic vegetation center within 10m of the current anchor; independently inspect actual combat/leash/loot footprints, which can require more space |
| Critical PvP/lock-on corridor | No new solid geometry; no bushes in the view from 0.8–2.5m height; keep feet and attack boundaries visible |
| Ordinary forest pocket | Preserve current tree anchors; do not increase trunk density without paired gameplay review |
| Low grass | Maximum 0.22m height; start around 10–20% ground coverage at edges, 0% on combat/path surfaces |
| Bushes | Approximately 0.7m tall; start below 5% visible ground coverage, away from camp/path sightlines |
| Background | Simple geometry at approximately 50–60m or beyond, behind existing boundaries/cover; never a foreground replacement or new invisible obstacle |

The conservative battlefield pass caps scatter at **48 grass + 10 bushes per half**; actual accepted counts can be lower after exclusions. It pairs 180° placements rather than independently randomizing the two sides. Existing forest collision cover is preserved, not multiplied.

The Blender patch is a separate visual fixture with a central clear 3.2m path and sparse vegetation away from it. Its camp sample does not define a new gameplay camp location. Its background strip extends the demonstration ground to the distant placeholder; it is not a proposal to enlarge the actual map.

## Multiplayer/performance budget

- All biome appearance is static local presentation. No NetworkObject, RPC, tick callback, server-driven wind or synchronized ambient particles is added. Existing map colliders remain identical for all peers.
- Shared materials, repeated mesh data, instancing-enabled URP materials, opaque foliage, three LODs, minimal shadow casting and capped scatter are the initial cost controls. These do not prove batching or target frame time; verify with the actual render pipeline and build.
- Each ambient emitter uses at most 16 particles, 2 particles/second, six-second lifetime, 0.025m size and low alpha. There are two emitters in the battlefield pass: maximum 32 live particles, no particle collision, lights, trails or shadows. Emission is not gameplay timing and may differ between peers.
- Ambient motes are non-emissive, subdued and above the main sightline. If warnings, target markers or silhouettes become less clear, disable motes before weakening gameplay presentation. The particle material and behavior are prepared by the Editor builder, not simulated in the Blender renders.
- Start with 512px shared PBR sets, mipmaps, compressed imports and Read/Write off. Do not raise texture size or add alpha-card layers until a measured close-camera need justifies it.
- Profile a six-player host/client session at 1080p on the intended minimum hardware. Compare the same route, camera and active camp/telegraph overlap before/after: median and p95 CPU/GPU frame times, draw calls/SetPass, memory, shadow cost and particle overdraw. Provisional art target: no more than 2ms p95 GPU regression versus the blockout and no unexplained main-thread regression. Hardware and baseline must be recorded before this becomes an acceptance claim.
- No benchmark hardware or multiplayer frame-time measurement is available here. No claim of a shipped/optimized production biome is made from Blender renders or polygon counts alone.

## Build and offline validation

Run from the project root with the installed executable:

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/create_ancient_forest.py -- .
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/validate_ancient_forest.py -- .
```

Generation requires the M55 Crate and Barrel sources for the look-development patch. It writes only its named AF sources/FBXs/textures, manifest and preview images. It does not regenerate M55 sources. Do not run the generator over future hand-edited AF files; export individual edited sources with the existing blender_tool.py `--export/--output` command instead and deliberately update the manifest.

The exporter is the existing shared `export_collection`, unchanged. Offline generation and independent actual-FBX reimport are tested in Steam Blender 5.3.0. The validator checks all 19 exported assets: mesh/LOD counts, nondegenerate geometry and UV triangles, material slot counts, meter-scale bounds, basis sockets and closed collision shells. It checks all 13 PNGs for size/finiteness, mask packing and tangent normal validity. Results are in AncientForest.validation.json. The overview/path renders are manually inspected; this is not equivalent to target-camera or color-vision gameplay acceptance.

Blender emits API deprecation notices and cannot write its user extension cache under the sandbox; workspace outputs still save successfully. The Blender version differs from the project's pinned 4.5 LTS export toolchain. Re-run the existing scale/axis calibration in Unity before approving this version for production. The Unity Editor scripts have not been compiled/run on this machine.

## Unity MCP completion checklist

1. Pull/hydrate Git LFS content, open the expected Unity version, let new `.meta` files be generated and inspect Console. Resolve compilation/import errors first. Re-run the existing Blender→Unity calibration because of the Blender version difference.
2. Complete the M55 asset builder if its Crate/Barrel prefabs do not exist yet. Run `HeroesOfDragonland.Art.Editor.AncientForestAssetBuilder.Build()` through MCP. It prepares 19 model prefabs, eight shared surface materials, the ambient particle prefab/material and a separate saved AncientForestLookDev scene. Existing outputs are not overwritten. First-import defaults remain those of ArtAssetPostprocessor.
3. Run `AncientForestAssetBuilder.Validate()`; inspect material/texture links, configured LOD levels, two-sided opaque leaves, collider policy, imported basis/scale and neutral palette. Review a humanoid/team pair and actual warning geometry against the biome at 5m/60° framing, in grayscale and in shadow. Test near/far LOD transitions and collisions in Play Mode.
4. In the saved battlefield, inspect Hierarchy, current camps, actual cover and all approach routes. Invoke `AncientForestBattlefieldPass.Apply()` through MCP; review the resulting tree/material/scatter pass before saving. It does not complete solid prop placement or final quality acceptance.
5. Through MCP place selected AF dead trees, rocks, log, stump, ruins and cairn, plus existing camp props, only where reviewed footprints preserve routes, lock-on views and camp/loot clearings. Prefer existing non-traversable decorative/cover footprints. Keep the two halves visually comparable. Retain existing interactable chest visuals/prompts; crates and barrels are decorative and must not impersonate rewards. A three-notch cairn sample is not a universal tier indicator: assign the correct existing tier glyph/label and lifecycle state before using it as a camp landmark.
6. If a solid collider must change, explicitly revalidate access/path lengths, camp navigation and symmetry and rebake the existing NavMesh asset through the established Editor tooling, preserving its GUID. Do not rebake merely for grass/material changes. Do not make vertical terrain that the current planar player motor cannot traverse.
7. Run human movement/camera orbit, lock acquisition/switching at the current 20m limit, targeting behind intended cover, chest interactions, camp engagements, both team silhouettes and all three Stone Warden telegraphs. Confirm low grass never erases attack boundaries. Test the actual host and remote client views; visual geometry and physics visibility can disagree.
8. Profile six-player sessions using the budget above. Check Console, save the reviewed biome scene/assets through MCP and return to the intended working scene. Record scene path, performance numbers, screenshots and pass/fail outcomes here. Until then, production quality, performance and map replacement remain pending.

## Change summary

All M56 files are new: 19 `.blend` model sources, one look-development `.blend`, 19 FBXs, 13 texture PNGs, manifest/validation report, two Blender scripts, two Editor-only commands, this document and two preview PNGs. Existing models, metadata, gameplay source and scenes are retained; no assets were deleted. No runtime architecture changes or automatic commits/pushes. `.blend`, `.fbx` and PNGs follow the existing Git LFS rules; `.blend1` backups remain ignored. Generated Unity metadata, prefabs, materials and saved scenes will belong to the later Unity integration changes.
