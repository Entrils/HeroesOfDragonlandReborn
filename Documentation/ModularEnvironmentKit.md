# Milestone 55 — Fortress + Ruins modular kit

## Delivery and status

This is a small reusable **prototype environment kit**, following the approved [ArtDirection.md](ArtDirection.md): broad limestone forms, restrained bevels, dark matte timber and limited iron. It is not final map art. No gameplay scene, existing art, collision footprint or gameplay behavior has been replaced.

Delivered offline: 21 individual editable Blender sources, 21 FBXs exported using the existing `export_fbx.export_collection` profile, two real mesh LODs per asset, dedicated static collision meshes, UV0, a manifest, a Blender test fortress/gallery and a preview render. An independent FBX round-trip validator is included.

**Pending on the Unity computer:** compile the new Editor builder, import through the existing ArtAssetPostprocessor, create the 21 linked prefabs and three shared URP materials, save the separate Unity test scene through MCP, inspect Console, and run visual/physics checks. No `.prefab`, `.mat`, `.unity` or `.meta` is hand-authored here. The Editor tool prepares these operations; it has not been run or compiled in Unity. M55's Unity requirements are not yet complete.

AGENTS.md and ArtDirection.md were read; DESIGN.md remains absent. M52/M53 remain deferred. The existing ArtPipeline.md conventions and export profile are reused without changes.

## Files and naming

```text
ArtSource/Environment/FortressRuins/
  SM_FR_<Module>.blend                 # one module, metric source
  FortressRuins_TestSection.blend      # assembly + separate ruins/prop gallery
  FortressRuins.manifest.json          # measured dimensions, triangles, materials, placements
  FortressRuins.validation.json        # independent FBX reimport results
Assets/Art/Environment/Models/FortressRuins/
  SM_FR_<Module>.fbx
Tools/ArtPipeline/
  create_fortress_kit.py
  validate_fortress_kit.py
Assets/Game/Art/Editor/FortressRuinsKitBuilder.cs
Documentation/FortressRuins/TestSection.png
```

`FR` identifies this Fortress/Ruins kit. Names are ASCII, stable and unique. Within each module's `EXPORT` collection:

```text
SM_FR_<Module>                         # identity root, pivot 0/0/0
  SM_FR_<Module>_LOD0                  # beveled visual, limited trim
  SM_FR_<Module>_LOD1                  # unbeveled core, reduced decorative detail
  COL_<Module>                        # separate collision-only mesh
  Socket_Front / Socket_Right / Socket_Up
```

LOD1 and COL are hidden from Blender rendering but are deliberately included in FBX. The source is a single-module authoring file; do not export the demo scene. Shared material identifiers are `M_FR_Stone`, `M_FR_Wood`, `M_FR_Iron`. Material slot order is explicitly recorded per LOD in the manifest for Unity remapping.

Planned Unity outputs from the builder:

```text
Assets/Art/Environment/Prefabs/FortressRuins/SM_FR_<Module>.prefab
Assets/Art/Materials/Environment/FortressRuins/M_FR_<Surface>.mat
Assets/Art/Environment/Scenes/FortressRuinsKitTest.unity
```

## Units, grid and pivots

One source unit = one meter, Metric / Unit Scale 1.0. Authoring right +X, front +Y, up +Z maps through the established pipeline to Unity right +X, front +Z, up +Y. No corrective 0.01/100 scales or rotated prefab wrappers. Three unit-distance basis markers support explicit import checks.

Use 1m placement grid, 4m wall/floor cadence and 90° turns. Subdivisions such as 0.5m are allowed for deliberate half-thickness offsets; stair steps are the documented exception. Place at scale 1. Non-grid decorative bevels and trims do not define snap spacing. The manifest records actual visual bounds, including trim; snap pitch and visual bounds are deliberately separate.

Most pivots are ground-center at source Z=0 / Unity Y=0. WallCorner uses its outside corner (0,0), extending into positive source X/Y. FloorSection uses top-surface center at Z=0, with its entire thickness below the pivot. TorchHolder uses the bottom of its mounting plate at the wall plane Y=0; its bracket projects toward source +Y. All rotations/scales are applied to geometry and all mesh origins coincide with the root.

## Module inventory and dimensions

Dimensions below are **Unity X × Y × Z** in meters. Structural dimensions define snapping; exact visual bounds and per-LOD triangle counts are in the manifest.

| Module suffix | Structural dimensions / placement | Notes |
| --- | --- | --- |
| WallStraight | 4 × 3 × 1 | Trim reaches 1.12m depth, but 4m end-to-end cadence remains exact |
| WallDamaged | 4 × up to 3 × 1 | Stepped missing crown, same endpoints; sparse face trim |
| WallCorner | 4 × 3 × 4 | L-shaped footprint, 1m thick arms; outside-corner pivot |
| Tower | 4 × 6.8 × 4 | 6m shell plus 0.8m crenels, hollow center, 2m-wide front opening |
| Gate | 8 × 6 × 1 | Open gate frame, 6m clear width at ground; spring height 2.5m; no moving door |
| Arch | 4 × 4 × 1 | 2m clear width at ground, spring height 2.5m |
| Stairs | 2 × 2 × 4 | Ten 0.2m rises / 0.4m treads; climbs toward source +Y / Unity +Z |
| Battlement | 4 × 1.4 × 1 | Place pivot on wall top; three broad merlons |
| Pillar | 1 × 3 × 1 | Base/capital with 0.7m shaft |
| FloorSection | 4 × 0.25 × 4 | Top pivot; bottom at -0.25m |
| BrokenWall | 4 × up to 2.3 × 1 | Descending ruin profile, shared 4m cadence |
| RuinedArch | 4 × approximately 4 × 1 | Missing voussoirs and shortened right pier |
| CollapsedPillar | 2.6 × 1 × 1 | Horizontal closed shaft and fallen base |
| Rubble01 | approximately 0.45 × 0.3 × 0.5 | Single small stone |
| Rubble02 | approximately 1.08 × 0.42 × 0.72 | Two stones |
| Rubble03 | approximately 1.69 × 0.54 × 0.93 | Three stones |
| Crate | approximately 1 × 1 × 1.02 | New kit crate; does not replace the calibrated SM_TestCrate |
| Barrel | 0.89 × 1.2 × 0.89 | 16-sided wood body with iron bands |
| TorchHolder | approximately 0.27 × 0.81 × 0.48 | Wall-mounted bracket; no flame, VFX or real-time light |
| WoodenBarricade | 3 × 1.6 × 1 | Sparse posts/rails, no invented interaction logic |
| WeaponRack | 2 × 1.8 × 1 | Empty rack with hooks; no duplicate weapon meshes or inventory system |

The arch profile is constructed from closed ring segments, not a solid box with a painted opening. Gate and arch collisions preserve the opening. The ruined arch is a static ruin, not a destructible structure. Tower interior is hollow; the upper platform is architectural illustration, with no ladder/vertical traversal feature added.

## Assembly rules and test fortress

Join straight wall centers every 4m. At a right-angle junction use WallCorner and align the incoming wall endpoints to its arm ends; do not stack a straight wall through the corner's occupied core. Tower attachment and wall thickness offsets should be resolved with the 1m grid, not nonuniform scaling.

Example adjacent straight modules: centers `(0,0,0)` and `(4,0,0)` in Unity. Their structural end planes meet at X=2. Floor tiles use the same 4m center cadence. Battlement on a 3m wall is positioned at Unity Y=3. A 2m staircase reaches a floor whose top is at Y=2; a second 1m rise would require another appropriately designed module. Do not stretch the stairs to a 3m wall.

The generated test section has a 16 × 12m tiled courtyard, 6m clear gate, repeated side walls/battlements, two towers, internal arch and props, plus a separate ruined-module gallery. A front prop lineup exposes the crate, barrel, wall-mounted holder, barricade and rack. The JSON placements reproduce the same assembly in Unity. Both contexts are architectural test fixtures: the demonstration staircase is not a completed walkable route to the parapet and no NavMesh/gameplay route is claimed. This does not replace the existing battlefield's fortress or routes.

## Geometry, UVs and LODs

LOD0 uses explicit one-segment 18mm bevels and a limited number of silhouette/trim pieces. The current kit ranges from 44 to 748 visual triangles per module. LOD1 removes bevels and optional trim, ranging from 12 to 204 triangles; every LOD1 has fewer triangles than its LOD0. All final render/collision meshes are explicitly triangulated with outward normals and closed component shells. Components may touch/intersect; this is a modular prototype mesh, not a boolean-unioned watertight solid suitable for fabrication.

UV0 is smart-unwrapped on each final mesh, including beveled faces. This is valid placeholder mapping, not a finished trim-sheet layout or approved texel density. Unity's established first-import environment profile generates lightmap UVs. Production texture work must normalize density, review seams and plan trim-sheet reuse before baking; no new texture maps are required here.

The Unity builder creates an LODGroup with screen-relative transitions at 0.18 (LOD0→LOD1) and 0.015 (cull). These are starting values requiring review at the actual camera, not performance-validated thresholds. Collision remains independent of visual LOD/culling. For a larger production map, review batching, material slots, occlusion and draw calls in-engine rather than adding more LOD levels preemptively.

## Collision contract

Each FBX includes one `COL_` mesh containing low-poly structural components only. Optional trim is omitted. The builder disables the collision renderer and assigns that mesh to a **non-convex static MeshCollider**. No Rigidbody is added. Do not use these prefabs as dynamic physics objects: they need a separately reviewed primitive/convex collision setup first.

Stairs use stepped collision matching their geometry, not an invisible ramp. Arch and ruined arch use segmented collision preserving visible openings, rather than an enclosing box. Disconnected closed shell components are allowed. Collision proxies are not claimed as optimized primitive colliders; static cooking, step navigation and edge catches remain Unity acceptance checks.

## Rebuild, export and verification

Run from the project root with the actual Blender path:

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/create_fortress_kit.py -- .
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/validate_fortress_kit.py -- .
```

The generator rebuilds only its own named outputs. Do not run it over future hand-edited kit sources without first preserving that work. An individual edited source can instead use the existing command:

```powershell
python Tools/ArtPipeline/blender_tool.py --blender 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --export ArtSource/Environment/FortressRuins/SM_FR_WallStraight.blend --output Assets/Art/Environment/Models/FortressRuins/SM_FR_WallStraight.fbx
```

After deliberate geometry edits, update the corresponding manifest measurements/material lists before running the validator or Unity builder. Do not rerun the procedural generator just to refresh metadata for a hand-edited model.

Offline generation and independent reimport both passed on Steam Blender 5.3.0. The round-trip validator imports all 21 actual FBXs and checks three meshes per asset, expected triangle counts, nondegenerate triangles, UV0, material slots, manifold component shells, meter-scale world bounds and the three basis markers. Results are saved in `FortressRuins.validation.json`. The test-section render was inspected. Blender reports API deprecation notices and a denied extension-cache write outside the workspace; deliverables still save and both processes exit successfully.

This uses the existing export code unchanged, but Blender 5.3.0 differs from the pinned Blender 4.5 LTS toolchain. A successful Blender→Blender round-trip does not establish Unity importer equivalence; run the existing calibration acceptance before approving this toolchain for production exports.

## Unity MCP handoff and acceptance

1. Pull sources/FBXs, ensure Git LFS content is hydrated, and open the project in the expected Unity version. Let Unity generate new `.meta` files and compile Editor tooling. Preserve all existing `.meta` files. Inspect Console and fix any compilation/import errors before continuing.
2. Re-run the existing art calibration to review the Blender version difference. Do not compensate for a failed axis/scale check by rotating/scaling prefab wrappers or changing global importer settings.
3. Through MCP invoke `HeroesOfDragonland.Art.Editor.FortressRuinsKitBuilder.Build()` in Edit Mode (menu: **Heroes of Dragonland → Art Pipeline → Build Fortress Ruins Kit (M55)**). The builder checks imported model bounds, identity transforms and basis sockets before generating prefabs. It follows existing first-import settings, creates shared external URP materials, and retains linked imported model instances.
4. The builder refuses to replace an existing kit prefab/test scene, avoiding accidental loss of authored edits. It creates wrappers in a temporary preview scene, saves a separate additive test scene, restores the previous active scene, and does not add the test scene to build settings. If creation fails after some outputs were saved, inspect and repair those outputs through MCP; no automatic delete/rebuild recovery is provided.
5. Run `FortressRuinsKitBuilder.Validate()` through MCP. Check all 21 linked prefabs, two LOD levels, material references, static collision and test scene. Open the test scene and inspect module seams, floor top alignment, pivots and team-neutral palette with a 1m reference and existing approximately 1.8m humanoid.
6. Enter Play Mode. Test gate/arch passage, stair collision with a temporary independent physics probe, collision against walls/floors, no invisible blockers in ruin openings, LOD transitions and collision persistence across LOD changes. The current player motor is planar; the presence of a stairs mesh does not establish new climbing functionality. Inspect Console again and save all modified assets/scenes through MCP.
7. Review scale, material brightness and edge readability at the existing 60° / 5m third-person camera. Confirm the static fixture contains no networking/gameplay systems and no production map was modified. Save evidence and record actual results here; these steps are currently **pending**, not passed.

## Git and change summary

New content: 21 `.blend` modules, 21 `.fbx` exports, one demo `.blend`, manifest, validation report, two Blender scripts, one Editor-only builder, this document and one preview PNG. There are no runtime architecture changes and no deletions. Previous M54 outputs remain separate. Existing `.gitattributes` already tracks `.blend`, `.fbx` and `.png` through Git LFS; `.blend1` backups remain ignored. New Unity metadata/prefabs/materials/scene will join a later integration commit after Unity generates and validates them. No commit or push is performed automatically.
