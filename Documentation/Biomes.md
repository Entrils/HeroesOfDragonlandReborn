# Milestone 57 — Production Biome Set

## Direction and delivery status

The four regions share the approved [ArtDirection](ArtDirection.md), the [modular fortress/ruin kit](ModularEnvironmentKit.md) and [Ancient Forest](AncientForest.md). They differ through broad terrain patterns, shape language, prop selection and restrained local light accents. Gameplay readability takes priority over density. A recognizable place must not require a different renderer, shader family, export profile or team visibility rule.

**Offline work delivered:** four visual identity specifications, a shared Blender comparison scene, five new modular assets, three new terrain PBR sets, five preblended terrain-transition PBR sets, six review renders, a manifest/validation report and one Unity Editor tool for shared asset setup and optional ground-material application. Existing AF/FR rocks, trees, ruins, architecture and props remain the primary library.

**Production acceptance is pending.** This computer has no Unity Editor/MCP. The new Editor tool has not been compiled/run, the gameplay scene has not been changed, and live camp visibility, transitions, lighting balance and six-player performance have not been verified. These are candidate production assets/directions, not a claim that four finished biomes have replaced the blockout.

AGENTS.md, ArtDirection.md and the current map/camp/camera documentation were read. Scene-builder geometry and saved scene names were inspected read-only. DESIGN.md remains absent. M52/M53 and the earlier Unity integration tasks remain deferred. No new enemy type, camp rule or map layout is introduced.

## Existing map footprint

Coordinates below are Unity X/Z meters, derived from the existing authored region surfaces. These are placement constraints, not a proposal for four new islands or separate maps.

| Region | Existing footprint | Preserved relationship |
| --- | --- | --- |
| Ancient Forest | Centers (-23,25) and (23,-25), each 38×34m | Northwest/southeast forest pockets, existing tree cover and routes |
| Rocky Highlands | Centers (23,25) and (-23,-25), each 38×34m | Northeast/southwest non-traversable outcrops and open detours |
| Ruined Battlefield | Center (0,0), 47×20m region surface | Central approach, contested objective and intersecting rotations |
| Corrupted Region | Centers (0,30) and (0,-30), each 12×24m | Narrow north/south cross-route treatment between the large regions |

Region surfaces overlap at some edges and do not cover every underlying ground area. Keep the established 3.2m paths, fortress exits, barriers, boundaries and planar traversal. Do not move or resize camp anchors to fit decoration. Inspect the **current serialized NetworkCampSpawner entries** in Unity; the original blockout positions in older documentation predate later balance passes.

The look-development scene uses four separated 12×12m comparison panels for review. Their arrangement and gaps are not an alternative gameplay layout. The transition gallery is also a material comparison, not a wall or route.

## Shared language and regional differences

| Biome | Terrain/material identity | Dominant silhouette | Prop signature | Light accent |
| --- | --- | --- | --- | --- |
| Ancient Forest | Muted olive litter/soil, warm readable dirt paths, pale bark | Tall branching crowns above open lower sightlines | Living trees, fallen wood, sparse grass/bushes, mossy shared ruins | Soft pale green/ivory bounce, restrained leaf shade |
| Rocky Highlands | Cool gray gravel/stone with broad pale slabs | Low horizontal strata and blunt outcrop shoulders | Stacked rock formation, common loose rocks, isolated dead tree and cairn | Cool open-sky accent on exposed stone |
| Ruined Battlefield | Worn gray-brown paving and churned earth | Broken vertical posts/columns against low broken walls | Neutral torn standard, common ruins, rubble, old camp supplies | Small warm ivory/brass pools near existing masonry |
| Corrupted Region | Medium-value ash-gray/violet soil with shallow dry cracks | Narrow inclined spires and distorted bare roots | Shared ruins in muted mineral treatment, spire and bent deadwood | Subdued dusty violet near mineral faces; no luminous ground pool |

Hue reinforces identity, but grayscale silhouette and surface frequency must carry it. No region owns the team azure/vermilion palette or warning-orange language. Important paths, players, chests, camp landmarks and committed attack shapes remain higher contrast than decorative texture details.

## 1. Ancient Forest

**Visual identity:** aged pale trunks, asymmetrical high crowns, calm moss/soil planes, restrained leaf variation. No dense green wall at character height. The M56 palette and geometry are reused directly; M57 does not rebuild the forest library or alter its identity.

**Props:** AF living/dead trees, rocks, bushes, low grass, fallen log, stump, ruin wall/pillar and neutral cairn. FR crate/barrel and fortress stone remain common-world elements. Place solid decorations in reviewed existing footprints, not by adding colliders across established routes.

**Gameplay purpose:** reinforce the existing pockets of cover, detours and approaches. Preserve deliberate tree-trunk cover while keeping the lower trunk zone and camp clearings readable. This art treatment does not add stealth, concealment bonuses or a new navigation rule.

**Enemy/camp theme:** existing wolves and bears fit the woodland silhouette; existing neutral knights can occupy established ruin-associated encounters. Keep authoritative prefab choices, numbers and camp tiers unchanged. Camp life state and reward availability remain driven by current systems. A decorative cairn or supply crate cannot impersonate an interactable loot chest.

**Lighting:** common overcast key/fill with a pale green/ivory local accent (`#C6D0B7`). Do not darken the forest by changing exposure or adding heavy fog. Optional point-light preset: intensity 0.15, range 4m, no shadows. These are starting Unity settings, not measured physical-light calibration.

**Performance/readability:** retain the M56 maximum of 48 grass and 10 bush candidates per forest half, with actual counts reduced by exclusions. No new understory in the clear trail/shoulder, camp or critical camera corridors. Use existing mesh LODs, opaque foliage and shared materials; no biome-specific wind/network system.

## 2. Rocky Highlands

**Visual identity:** cool pale slabs over medium gray gravel. Horizontal layering distinguishes it from narrow corrupted spires and upright ruined columns. Broad surface planes replace busy pebble noise. Outcrops feel ancient and eroded rather than clean stacked plates.

**Props:** new `SM_BS_StrataOutcrop`, built from the shared AF Rock02 geometry; reuse AF Rock01/02/03, a few dead trees, neutral cairns and occasional common ruin fragments. No separate highland rock/architecture collection is required. Loose rocks remain near outcrop bases instead of scattering into paths.

**Gameplay purpose:** visually explain existing rocky cover and non-traversable high ground while keeping detours and cross-links clear. Do not turn decorative layered rocks into new climbable terrain: the existing player motor is planar. Preserve existing obstacle footprints, line-of-sight breaks and approach distances.

**Enemy/camp theme:** existing bears and wolves read as rocky wilderness predators; existing knights can use the same world ruin/camp kit. Pale fur/armor variants are an optional future visual treatment, not new statistics or new spawns. Tier identity still uses the common notches/glyphs and current labels.

**Lighting:** cool open-sky accent (`#C2CED4`), optional intensity 0.15 / range 4m / shadowless point preset for a limited focal rock. Avoid icy blue saturation that resembles Team A, glittering highlights or deep black crevices that swallow actors. No snow/weather mechanic is added.

**Performance/readability:** StrataOutcrop has 960/240/60 triangles across three LODs. Repeated formations share meshes/materials. Prefer replacing a reviewed existing outcrop visual to adding new solid obstacles. Keep small stones decorative and out of the combat ground; do not create a collider for every pebble. Background geology can stay coarse behind the existing boundary.

## 3. Ruined Battlefield

**Visual identity:** broad worn stone courses, dust/earth between fragments, broken pale-gray masonry and dark iron. Repeated rectangular construction anchors the scene to the fortress kit. Small decorative war relics supply history without covering combat surfaces in weapons and rubble.

**Props:** shared AF ruin wall/pillar, FR broken wall/ruined arch/collapsed pillar/rubble where structurally sensible, existing crates/barrels/rack, and new `SM_BS_ShatteredStandard`. Its torn ivory cloth is **neutral decoration**, not a team flag, capture point, interaction prompt or camp-tier marker. All fortresses continue to use the shared FR architecture and established team crest rules.

**Gameplay purpose:** make the central contested space recognizable while keeping it the clearest fighting surface. Retain the fast central approach, flank cover and rotations. Do not rebuild the boss arena, fence camps, add an arena lock or add visual lines that resemble safe zones and attack circles.

**Enemy/camp theme:** existing neutral knights and the **Stone Warden** fit the ruined construction language. The contested boss remains the same neutral encounter; other current camp compositions remain unchanged. Decoration never implies Team A/B ownership of a neutral camp. Loot chests retain their domed/lock/brass recognition language and current interaction rules.

**Lighting:** restrained warm ivory (`#D4BB91`), intensity 0.25 / range 4m / no shadows, near a reviewed existing structure or brazier. Keep the combat floor lit by the same common overcast baseline. Warmth cannot resemble a persistent hostile telegraph or bury the real orange-to-ivory danger perimeter.

**Performance/readability:** keep the center comparatively empty. No automatic vegetation/prop scatter. Reuse modular ruins and batching-friendly materials. ShatteredStandard uses 92/28 triangles and two primitive colliders on pole/base only; its cloth has no ray-blocking sheet collider. No animated cloth simulation, dynamic debris or networked decoration is introduced.

## 4. Corrupted Region

**Visual identity:** dry ash-gray/violet terrain, inclined mineral forms, warped deadwood and pale mineral facets. Corruption is physical shape and restrained material variation, not a purple fog filter. Ground remains medium value, with shallow cracks and no glowing damage-pool pattern.

**Props:** new `SM_BS_CorruptedSpire` derives from AF Rock03; `SM_BS_TwistedRoot` derives from AF DeadTree02. Reuse common loose rocks, dead trees and ruin wall/pillar with a muted corrupted-stone material assignment. Shared geometry is retained wherever useful. Do not imply destructibility, poison, damage-over-time or magic barriers unless existing gameplay supplies them.

**Gameplay purpose:** make the north/south cross-route pockets distinct and tense without creating new hazards or navigation constraints. Preserve the corridor width and connections. Inclined silhouettes belong near existing reviewed non-traversable footprints; no sharp protrusion may visually block a path that physics allows through.

**Enemy/camp theme:** existing neutral armored enemies and established nearby high-tier camps can receive the common bone/iron/gray visual treatment. This milestone does not create a corrupted monster faction, swap current enemy definitions or introduce a separate boss. Any future visual variant must retain recognizable attack pose/weapon, neutral identity and existing damage rules.

**Lighting:** dusty violet (`#B2A1BD`), optional intensity 0.15 / range 4m / no shadows, localized to a reviewed mineral face. No new emission or ambient particle system is required. Avoid bright purple crystals across the whole region, blackened silhouettes, thick haze and flashes competing with combat.

**Performance/readability:** spire LODs use 320/80/20 triangles; twisted-root LODs use 744/296/166. Both share source construction with AF assets. Use opaque URP Lit surfaces and sparse placement. No transparent fog volume, animated distortion pass or per-frame biome controller. Existing attack warnings must remain distinguishable from both cracked ash and pale mineral faces.

## Transitions and neighboring regions

Five **preblended 512px PBR transition sets** share the same URP Lit shader and 4×4m terrain-tile mesh:

| Transition | Application context |
| --- | --- |
| Forest → Ruins | Woodland litter gives way to worn masonry at existing inner approaches |
| Highlands → Ruins | Gravel/stone gives way to paving without an abrupt terrain wall |
| Forest → Corrupted | Litter thins into dry ash; occasional deadwood replaces live decoration |
| Highlands → Corrupted | Mineral strata sparsely acquire inclined forms and ash tones |
| Ruins → Corrupted | Broken construction and mineral staining bridge the cross-route treatment |

Forest and Highlands are separated by the established cross-route/ground treatments in the relevant northern/southern areas; do not invent a new boundary ridge to force a direct transition. Inspect actual neighboring floor edges in the saved scene. Uncovered base-ground areas can retain the shared dirt/earth material instead of enlarging a region rectangle.

Transition maps blend base color in linear light, renormalize tangent normals and interpolate mask values. U=0 is the named first region; U=1 is the second. The tile UVs follow local +X for blend direction and local +Y for repeat direction (Unity +X / +Z after import). The Unity builder sets Clamp U / Repeat V on these owned transition textures, preventing opposite-end wrap bleeding. Tileable pure-region maps remain Repeat.

Use a narrow approximately 2–4m visual collar along the existing boundary, oriented to the actual edge. Sample scale, UV phase and adjacent-map alignment must be reviewed; endpoint color matching in a texture does not automatically guarantee a seam-free join on differently tiled scene meshes. Preserve actual path width and trail dominance through the collar. Keep vegetation/solid decorations out of the corridor and do not hide chests or camp labels behind transition props.

These are **opaque shared-shader surfaces**, not alpha-blended layered carpets. In Unity, fit/trim collars within existing floor coverage and inspect overlapping region ordering and depth. Do not stack coincident planes or solve z-fighting with large visible floating offsets. Use a controlled mesh split when necessary through MCP, preserving traversal collision below it. No transition geometry has been added to the gameplay map on this computer.

## One pipeline and shared library

All new assets use `Tools/ArtPipeline/export_fbx.py`, the existing ArtAssetPostprocessor, meters/applied transforms, Front/Right/Up calibration sockets, UV0, explicit triangulation, external shared URP Lit materials and normal/mask conventions from ArtPipeline.md. There are no four renderers, biome-specific shader graphs or parallel import profiles.

| Shared content | Regional reuse |
| --- | --- |
| AF Rock01/02/03 | Loose rock in every region; source for strata and spire geometry |
| AF living/dead trees | Forest, sparse exposed highland deadwood, source for distorted root |
| AF/FR ruins and fortress kit | Woodland relics, highland remnants, battlefield structures and corrupted stone variants |
| AF cairn and existing camp labels | Consistent neutral/tier recognition in all regions |
| FR crate/barrel/rack | Shared camp supplies; never a substitute for an interactable chest |
| AF ForestFloor and DirtPath maps | Reused directly, not duplicated into a new forest material family |
| URP Lit | All environment terrain/prop/transition materials; no custom shader added |

The new comparison scene instantiates these sources and assigns regional materials; material variation does not require exporting duplicate rocks or ruins. Existing team fortresses retain the same architecture and equal team heraldry regardless of adjacent biome.

## Lighting and competitive rules

Use the same global key, fill and fixed exposure across all four regions. Local accents are optional low-cost focal details. They cannot be the only way to identify a biome; the four silhouettes and terrain patterns must also read in grayscale. Blender comparison-point powers are reference values, not a unit conversion proving Unity light parity.

Start with at most one accent light visible per local region focal point and at most two within a typical combat camera. The comparison board has four isolated samples; this is not permission to blanket the map with point lights. Disable accent lights first if they push performance or hide a team color/hostile boundary. Avoid team-dependent lighting, region-wide color grading, lens effects, thick fog and exposure adaptation.

Common requirements remain: both teams readable at 5/15/25m; hostile boundaries readable quickly against every floor; neutral identity distinct from team ownership; chests recognizable at approximately 10m; camp landmarks identifiable at approximately 25–35m on a clear approach. No decision may depend only on subtle hue differences. Existing cover can intentionally block sight; decoration cannot add excessive accidental concealment.

## Performance and neutral-camp constraints

- No runtime biome manager, network object, RPC, per-tick scatter or synchronized weather is added. All changes are local static presentation; camp authority remains in existing systems.
- New source geometry is limited to five reusable modules. Three terrain PBR sets plus five transition sets add 24 PNG maps, all 512×512, with mipmaps/compressed imports. No 4K texture set per region.
- Repeated objects share meshes/materials; instancing is enabled in the prepared URP materials. Verify actual batching/SetPass behavior in a build instead of assuming instancing eliminates cost.
- Existing AF/FR LODs are reused. New large shapes use three LODs, the standard two, and the flat terrain tile one mesh. Initial thresholds are 0.20/0.07/0.012 screen fraction (two-level standard: 0.20 then cull 0.012). Review silhouette changes at the real camera.
- New strata/spire/root standalone prefabs use the lowest visual mesh as a static non-convex MeshCollider. Do not add Rigidbody components or automatically scatter these colliders onto the map. Material/ground changes do not require a navigation rebake; deliberate blocker changes do.
- No new regional particle emitter is added on top of M56's budget. Use the existing sparse ambient prefab only where it remains readable. Corruption does not need a second transparent fog/particle system.
- Preserve current camp anchors, prefab composition, timing, aggro/leash, reward and chest lifecycle. Theme is art direction, not permission to move/spawn enemies. Clear combat/loot footprints before adding supplies or landmarks.
- Profile host and remote clients with all six players, overlapping PvPvE warnings and a worst-case boundary view. Compare median/p95 CPU and GPU times, draw calls, shadows, texture memory and overdraw to the same saved baseline/camera. The provisional M56 +2ms p95 GPU regression ceiling must be assessed for the **combined** biome pass, not granted independently to each region. Hardware and timings remain unmeasured here.

## Files and authoring commands

```text
ArtSource/Environment/Biomes/
  SM_BS_StrataOutcrop.blend
  SM_BS_ShatteredStandard.blend
  SM_BS_CorruptedSpire.blend
  SM_BS_TwistedRoot.blend
  SM_BS_TerrainTile.blend
  Biomes_LookDev.blend
  Biomes.manifest.json
  Biomes.validation.json
Assets/Art/Environment/Models/Biomes/SM_BS_*.fbx
Assets/Art/Textures/Environment/Biomes/T_BS_*.png
Assets/Game/Art/Editor/BiomeSetBuilder.cs
Tools/ArtPipeline/create_biome_set.py
Tools/ArtPipeline/validate_biome_set.py
Documentation/Biomes/{Overview,Forest,Highlands,Ruins,Corrupted,Transitions}.png
```

`BS` means the shared Biome Set, not a separate pipeline. Source root/pivot is ground-center zero; TerrainTile uses surface-center zero, 4×4m. All transforms are applied. Each source exports its own `EXPORT` collection only; do not export the comparison board as a production model. Existing source paths and `.meta` files are preserved.

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/create_biome_set.py -- .
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/validate_biome_set.py -- .
```

The generator requires the existing M55/M56 source library and AF forest texture set. It does not regenerate or overwrite AF/FR sources, existing map files or prior milestone outputs. It rebuilds its own named BS files; preserve future manual edits before reusing the generator. Individual source edits should use the shared existing `blender_tool.py --export ... --output ...` path and deliberately updated manifest measurements instead.

Offline validation checks five actual FBX reimports, mesh/LOD counts, nondegenerate UV/geometry triangles, material-slot counts, meter-scale bounds, basis markers and closed low-LOD collision shells where used. The 24 maps are checked for mask packing and valid normals; five transition pairs are checked against both pure-material endpoints. Review renders are inspected separately. This is tested in Steam Blender 5.3.0, not a claim of Unity importer/lighting parity with the pinned 4.5 LTS toolchain. Re-run the existing Unity calibration before production approval. Sandbox-denied Blender extension-cache writes and API deprecation notices do not prevent workspace output saves.

Verification record, 2026-11-16: the independent validator completed with exit code 0: **five FBXs, 24 maps and five PBR transition pairs passed**. The initial map writer changed image color space after filling pixels, which cleared generated normal/mask data; moving color-space setup before pixel assignment corrected the output and the full check then passed. Overview and transition renders were inspected. Unity compilation, Console, prefabs, live map and performance checks remain unrun.

## Unity MCP completion

1. Pull/hydrate LFS assets. Open the expected Unity version, let new `.meta` files be generated and resolve Console errors. Finish prior FR/AF prefab setup needed by the shared library. Preserve all existing GUIDs and run scale/axis calibration for the newer Blender version.
2. Invoke `HeroesOfDragonland.Art.Editor.BiomeSetBuilder.Build()` through MCP. It uses existing import defaults, creates 13 external materials (eight palette + five transition), five linked-model prefabs, four optional shadowless accent-light prefabs and a separate `Assets/Art/Environment/Scenes/BiomesLookDev.unity`. It refuses to overwrite existing model-prefab/scene outputs, builds temporary wrappers in a preview scene and restores the previous active scene. Run `BiomeSetBuilder.Validate()` and inspect Console. These commands have not been compiled/run here.
3. Review all four panels with both teams, neutral actor/chest samples and actual attack warnings, in color and grayscale at the existing 60° / 5m camera. Check local accent color and brightness against the common baseline. The board itself has no gameplay authority or playable ground-collision promise.
4. In the saved battlefield, after the earlier forest integration, optionally run `BiomeSetBuilder.ApplyLoadedGrounds()` through MCP. It assigns materials to the seven existing named biome-floor renderers, preserves their transforms/meshes/colliders, uses size-derived shared tiling and marks the scene dirty for review. It does not save the map, add props/collars/lights, move camps or rebake navigation. Existing source/runtime gameplay files remain unchanged.
5. Through MCP place representative regional props in existing reviewed cover/decorative footprints, use shared AF/FR ruins and architecture, and fit transition collars to actual neighboring edges/UV phase. Preserve paths, camera orbit space, lock-on rays, enemy navigation and chest/camp recognition. Apply paired visual treatment to mirrored regions. Limit lighting accents; no automatic dense scatter.
6. Test player movement, camera collision/orbit, lock acquisition/switching, camp engagements, chest prompts and boss cleave/sweep/stomp on each floor and at transitions. Compare both teams in shadow and from both directions. If any collider changes, revalidate/rebake the existing navigation asset through established tooling, preserving its GUID.
7. Profile the combined biome set in a six-player host/client build. Check Console again, save all reviewed scenes/materials/prefabs through MCP and record screenshots/performance/pass-fail results here. Until then, Unity integration, map transition placement and production quality/performance acceptance remain pending.

## Change summary

New M57 content only: five model `.blend` sources and FBXs, one comparison `.blend`, 24 PBR PNGs, manifest/validation report, six review images, two Blender scripts, one Editor-only tool and this document. No existing source, scene, metadata or gameplay asset was deleted or modified; no runtime architectural system or automatic Git commit/push is added. Existing LFS rules cover `.blend`, `.fbx` and PNGs. Newly generated Unity metadata/materials/prefabs/scenes belong to the later integration changes.
