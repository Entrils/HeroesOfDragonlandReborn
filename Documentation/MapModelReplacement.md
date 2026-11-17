# Battlefield model replacement — 2026-11-17

The director explicitly requested replacing the remaining map blockout after the initial integration had placed mainly trees. This authorizes actual fortress replacement beyond the earlier offline handoff's test-kit-only scope. No new milestone, character pipeline or Blender regeneration is included.

## Actual map changes

- Both fortresses use the existing Fortress/Ruins wall, battlement, tower and gate-frame models. Eight cylinder towers and the flat gate lintels are no longer rendered. Royal-chamber walls and doorway lintels also use imported modular stonework.
- Both authoritative gate prefabs retain their GUIDs, original Gate body transform, network components, collider and NavMeshObstacle. Imported wooden barricade art is parented below Gate body, so the existing opening/destruction lifecycle hides it. The old cube renderer is disabled.
- Eight broken ruin walls, eight pillars, twelve rocky outcrops, six corrupted blocks, central cover, six rotation barriers and four map-boundary stretches use the existing imported FR/AF/BS models. Long stone ridges include continuous imported masonry behind the rocks, preserving the original opaque cover appearance rather than exposing visual sightline gaps.
- Existing low camp perimeter cubes are visually replaced by fitted rubble models; public camp cairns reuse AF art. Camp anchors, spawn composition, timing and gameplay state remain unchanged.
- Both fortresses have matching blue/red cloth standards and distinct chevron/bar gate crests. Materials are shared. Ground/courtyard surfaces retain their footprint with shared paving/path materials.
- 330 original blockout renderers were disabled, not deleted; 681 linked imported model instances now provide the scene art. Their original collision objects remain in place. Model instance scales fit the already-reviewed map dimensions; importer calibration, FBX sources and asset GUIDs are unchanged.

## Material correction

Live inspection found a real FBX submesh-order difference in ShatteredStandard: LOD0 is cloth/pole/base, while LOD1 is pole/base/cloth. The offline builder had assigned one assumed order, making LOD0 cloth black and the base team-colored. BiomeSetBuilder now resolves these three roles from imported submesh bounds. The existing standard prefab and loaded map instances were repaired in place; cloth, iron and stone are now assigned correctly at both LODs.

## Checks actually run

- Before changes: Git branch/status, saved battlefield Hierarchy and MCP Console inspected; Console had zero errors. All prior local changes retained.
- During scene application and refinement: complete original collider serialization, transforms, world matrices/bounds and collider count compared before/after; camp, fortress, assault and team-spawn authoring serialized data compared before/after. Checks passed. No new art collider remains and no NavMesh asset rebake was needed.
- Six native Unity renders reviewed: overview, both fortresses, ruins, highlands and corrupted region. Early ridge gaps and wrong banner material assignment were corrected after review. Evidence is local under `Builds/MapModelReplacement/`.
- Play Mode, active local match: both imported closed gates and original blockers verified; authoritative destruction hid imported art and disabled the original collider/obstacle; both public minimap gate states became Cleared. Validator result: PASS.
- Existing camera regression: PASS third-person view, orbit, camera-relative movement, wall collision, avatar rebinding and bot isolation.
- Asset/reference audit: all existing M55–M62 validators and 114 prefab missing-script checks passed. Scene baked/saved through MCP, one loaded battlefield, dirty=false; no temporary fixtures left in Edit Mode.
- Windows development build succeeded: 0 errors, 488 warnings, 245300538 bytes, 23.21 seconds; output `Builds/M54M62/Dragonland.exe`. Warnings include existing package compute-shader variants. Final MCP Console query returned zero errors and zero warnings. Scene remains saved in Edit Mode, focused on the new fortress. This follow-up does not claim a repeated complete multiplayer/audio/music acceptance matrix or a measured performance regression comparison.

## Files and architecture

Modified: DragonlandBattlefield.unity, TeamAGate.prefab, TeamBGate.prefab, the existing ShatteredStandard prefab, BiomeSetBuilder.cs and the linked integration documentation.

New: BattlefieldModelReplacement.cs (Editor authoring only), BattlefieldModelPlayValidation.cs (Editor test only), shared fortress team/crest materials and heraldry mesh plus Unity-generated metadata. No new runtime gameplay system, RPC or NetworkBehaviour. Deleted assets/files: none. Existing serialized collider references and metadata retained. No commit/push.

Remaining review: final artistic approval, distant LOD transitions and a dedicated performance comparison for the added static art. No claim that these delivered low-poly prototype meshes are final production art.
