# Milestone 39 — Blender → Unity art pipeline

## Scope and project context

This is the authoring/import contract for future Heroes of Dragonland 3D assets. The current renderer is URP, with gameplay in meters and a third-person camera. Gameplay, networking, targeting, equipment data and existing placeholders remain unchanged. The completed sample is **SM_TestCrate**, a 1m prop with Blender source, FBX, one small texture, an external URP material, a box collider, a linked Unity prefab and a separate calibration scene.

`AGENTS.md` was read. `DESIGN.md` was not present in this checkout, including hidden project folders, at implementation time. These standards implement the milestone requirements and existing project conventions; they do not establish a new art style or replace a missing design specification.

Validated toolchain: **Blender 4.5.15 LTS**, Unity **6000.6.0f1**, URP **17.6.0**. The portable Blender download is from the [official LTS release](https://www.blender.org/releases/4-5/), SHA-256 checked against the official release checksum list. Local binaries/configuration live under ignored `.utmp/Blender/`, with no system-wide install or Blender MCP server required.

## Folder and ownership contract

```text
ArtSource/                         # Editable DCC sources; outside Unity's asset importer
  Characters/<AssetName>/
  Weapons/<AssetName>/
  Environment/SM_TestCrate/
    SM_TestCrate.blend
    SM_TestCrate.pipeline.json     # Calibration expectations, not a Unity runtime dependency
  Creatures/<AssetName>/
  Animations/<CharacterName>/
Assets/Art/
  Characters/                     # Character FBXs; use Models/ and Prefabs/ per asset/category
  Weapons/                        # New weapon meshes/prefabs
  Environment/
    Models/SM_TestCrate.fbx
    Prefabs/SM_TestCrate.prefab
    Scenes/ArtPipelineValidation.unity
  Creatures/
  Materials/Environment/M_TestCrate.mat
  Textures/Environment/T_TestCrate_BaseColor.png
  Animations/
Tools/ArtPipeline/                # Headless Blender scripts
Assets/Game/Art/Editor/           # Unity import defaults, fixture builder and validation
```

All seven requested `Assets/Art` category folders exist. Existing `Assets/Art/Models/Weapons` content is retained in place: moving old assets is not part of this milestone. Third-party assets stay in `Assets/ThirdParty`; first-import defaults do not modify them. Do not put `.blend` sources inside Assets: Unity must consume the reviewed FBX, without depending on an artist's installed Blender version. Preserve `.meta` files when moving/renaming in Unity and re-export onto the same FBX path. Commit source, FBX, texture, Unity material, prefab and metadata together. The small sample does not require Git LFS; agree on team LFS rules before committing large production binaries.

## Units, axes and transforms — acceptance gates

| Property | Blender authoring | Unity result |
| --- | --- | --- |
| Units | Metric, Unit Scale **1.0**, Length Meters | **1 unit = 1 meter** |
| Right | **+X** | **+X** |
| Forward/front of asset | **+Y** | **+Z** |
| Up | **+Z** | **+Y** |
| Main root | Location 0,0,0; applied rotation; scale 1,1,1 | Position 0,0,0; rotation 0,0,0; scale 1,1,1 |

Use these numeric directions rather than inferring front from a Blender viewport label. The asymmetric front latch and `Socket_Front`, `Socket_Right`, `Socket_Up` markers verify the exact source-to-Unity mapping. A symmetric cube alone cannot verify front direction. Do not repair an incorrect import with a rotated/scaled Unity wrapper, a 0.01/100 Scale Factor, negative scale or an altered project-wide unit convention.

Set real dimensions before applying rotation and scale in Blender. Apply mirror/negative scale before final normal baking, check outward normals and remove accidental duplicate geometry. Preserve intentional socket/hinge transforms. Save authoring scenes in Object Mode. For rigs, establish transforms before skin binding; do not apply transforms to an already-approved skinned rig without rechecking the bind pose and animations.

## Origins and pivots

- Static props and creatures: ground/feet contact at Z=0 in Blender; origin at the bottom center unless the asset brief specifies a hinge or attachment pivot.
- Modular walls/floors: origin on the agreed snap corner or module intersection; use documented 1m or 0.5m increments, exact dimensions and no compensating prefab scales.
- Doors/lids: hinge origin at the actual rotation axis, with an explicit parent assembly root. Do not silently substitute a center pivot.
- Characters: rig/object root at ground-center between the feet; the approved skeleton owns pelvis/root-motion placement.
- Weapons: origin at the primary hand grip, blade/barrel/shaft direction Blender +Y → Unity +Z, top Blender +Z → Unity +Y. Provide `Socket_Grip`, `Socket_Tip`, and optional offhand attachment markers as needed. An approved grip pose may require a documented visual-child offset, not resizing the gameplay avatar.

SM_TestCrate is exactly 1 × 1 × 1m, with pivot (0,0,0), Unity mesh center (0,0.5,0) and visual minimum Y=0.

## Naming

Use ASCII identifiers, PascalCase words after prefixes, underscores for semantic separators, and no spaces, duplicate `.001` suffixes, or names such as Final2/NewCube.

| Content | Example |
| --- | --- |
| Static mesh/source/prefab | `SM_TestCrate`, `SM_IronSword`, `SM_StoneArch` |
| Skinned mesh | `SK_Hero`, `SK_ForestBear` |
| Reusable character visual prefab / Animator controller | `PV_PrototypeHumanoid`, `AC_PrototypeHumanoid` |
| Animation-only FBX/action | `AN_Hero_Idle`, `AN_Hero_AttackLight01` |
| Unity material / Blender material slot | `M_TestCrate`, `M_HeroArmor` |
| Texture | `T_TestCrate_BaseColor`, `T_Hero_Normal`, `T_IronSword_Mask` |
| LOD geometry | `SM_StoneArch_LOD0`, `_LOD1`, `_LOD2` |
| Attachment/helper | `Socket_Grip`, `Socket_Front`, `COL_StoneArch` |
| Collections | `EXPORT` for delivery; `WORK` / `REFERENCE` for non-export content |

Match material slot names and ordering across re-exports. Keep one logical asset and its LODs/sockets in one EXPORT collection; do not export cameras, lights, reference models, hidden work-in-progress or an entire Blender scene accidentally.

## FBX export — use the shared profile

`Tools/ArtPipeline/export_fbx.py` is the executable profile. It preflights metric units, scale, root transforms, UV0 and material slots, then exports only the EXPORT collection. The filename must begin SM_, SK_ or AN_. For a rigged export, SM_ is rejected.

| Setting | Value |
| --- | --- |
| Export scope/types | Selected EXPORT objects: Mesh, Armature, Empty |
| Global Scale | **1.0** |
| Apply Unit | On |
| Apply Scalings | **FBX Units Scale** |
| Forward / Up | **-Z / Y** |
| Use Space Transform | On |
| Apply Transform / Bake Space Transform | On for static meshes; **off for armatures** |
| Geometry | Apply modifiers; explicit triangulation before tangent/normal acceptance; smoothing Face; tangent space exported |
| Armatures | Add Leaf Bones off; deform bones only, retaining required hierarchy/root |
| Animation | Rigged assets only; active action, no NLA batch, no all-actions batch, simplify 0 |
| Textures | No embedding; Strip path mode; textures delivered separately |

The script's rig branch is a baseline, not a substitute for character acceptance. Export each animation action deliberately; verify frame range and action assignment before exporting. Static colliders and LOD behavior are assembled in Unity prefabs, not guessed from FBX filenames.

The [Blender FBX manual](https://docs.blender.org/manual/en/4.5/addons/import_export/scene_fbx.html) describes the axis/export options. This project's final contract is the measured calibration result with these exact exporter and importer settings.

## Unity model import

`ArtAssetPostprocessor` supplies **first-import defaults** for new FBXs under Assets/Art, excluding the legacy Models folder. Settings are serialized in `.meta`; subsequent reimports preserve deliberate artist overrides. The test builder explicitly applies the same profile to its own fixture, so a file imported before the postprocessor compiled is still calibrated correctly.

- Scale Factor 1, Convert Units/useFileScale on, **Bake Axis Conversion on**. Unity documents that [Bake Axis Conversion transforms imported data into Unity coordinates](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ModelImporter-bakeAxisConversion.html).
- Cameras/lights off; automatic collider generation off; Read/Write off; mesh compression off for a reliable baseline.
- Import normals; calculate MikkTSpace tangents. Review hard edges, mirrored seams and normal-map shading together.
- SM_ assets: Animation/Rig None. Environment SM_ assets get generated lightmap UVs by default; turn this off in the tracked importer when delivering reviewed authored UV1.
- Skinned content in Characters defaults Humanoid; Creatures and other animated categories default Generic. Animation-only FBXs need explicit rig/avatar review. Use the [Unity model import reference](https://docs.unity3d.com/6000.0/Documentation/Manual/FBXImporter-Model.html) when changing an exception.
- No embedded/auto-generated Unity materials. Create external Materials assets and assign them in the linked prefab; use explicit material remaps when a production asset requires them.

An intentional import exception must be recorded in that asset's source notes and `.meta`. A toolchain upgrade, new rig convention or exporter flag change must rerun calibration before production adoption. Do not apply these defaults recursively to existing art or third-party packages.

## Materials and textures

Author for **URP Lit**, metallic workflow. Blender Principled nodes are authoring/preview data; procedural graphs are baked to textures. Unity materials are explicit external assets in Assets/Art/Materials and are shared by prefabs. Prefer one material slot for a simple prop; additional slots require a visible reason. Keep alpha opaque unless required; transparency/cutout is a reviewed material choice. Avoid material-instance proliferation and per-renderer runtime copies.

| Texture suffix | Space / packing | Unity treatment |
| --- | --- | --- |
| `_BaseColor` | sRGB RGB, no baked lighting | Base Map; sRGB on |
| `_Normal` | Tangent-space, +Y/green-up convention | Normal Map type, no automatic green flip |
| `_Mask` | Linear RGBA: R metallic, G AO, B reserved 0, A smoothness | sRGB off; use for MetallicGlossMap and OcclusionMap |
| `_Emission` | sRGB color | Emission Map, enable the material feature deliberately |
| `_Roughness`, `_AO`, `_Height` | Linear grayscale intermediate/source maps | sRGB off; bake/pack the reviewed runtime maps |

URP smoothness is **1 − roughness**; do not plug roughness directly into smoothness. Preserve mask alpha. Use PNG/TGA for ordinary maps, EXR only when HDR data is needed. Prefer power-of-two sizes: 256–512 for small props, 1024 typical, 2048 for reviewed heroes; larger textures require a measured need. First-import max size is 2048, mipmaps on, compressed, Read/Write off. Set platform compression overrides for the intended build targets before release; this milestone does not invent mobile budgets.

The sample uses one 256×256 Blender-generated BaseColor texture, M_TestCrate (metallic 0, smoothness 0.2), and no unnecessary normal/mask textures. The `.blend` packs its source texture for portability; the FBX does not embed it.

## UVs and geometry

- UV0 is the material/bake channel. Keep texel density consistent within an asset set; use a measured target such as 256 px/m for ordinary environment props, 512 px/m for hero surfaces, and document exceptions.
- Unique UV0 for unique bakes; intentional overlap/tiling is allowed for shared tiling materials. Bake padding must survive mipmaps: start around 8–16 pixels at 1024 and inspect the lowest useful mip.
- Unity UV1 (second channel, often called UV2 in the UI) is for lightmaps: unique islands, no overlap, sufficient margins. Generated lightmap UVs are the environment default. The crate's UV0 and generated second channel are verified.
- Triangulate deliberately before accepting normal bakes; eliminate degenerate/non-manifold faces and unintended internal geometry where relevant. Retain intentional hard edges and UV seams. Review silhouette and shading in Unity under grazing light.

## LODs and performance

LODs are required for expensive/distant assets, not automatically for every tiny prop. Use the same pivot, orientation, bounds, rig and material layout for every LOD. Start with LOD1 at roughly half and LOD2 at roughly one fifth of LOD0 triangles, then review silhouette and actual frame cost. An initial Unity LODGroup screen-height profile is 0.5 / 0.2 / 0.02, with culling below the final threshold; tune per asset category and camera. Review shadows and transitions in a moving third-person camera. Use one stable collider set across LODs.

Small static props should generally stay within a few hundred to a few thousand triangles. Character/creature budgets depend on simultaneous onscreen count, skinning and target hardware; record their reviewed budget with the asset rather than treating a universal number as a guarantee. SM_TestCrate is a small calibration prop, so it intentionally has one mesh and no artificial LOD set.

## Collision and prefab assembly

Prefer Box/Sphere/Capsule colliders, with compound primitives for complex movable props. Static architecture may use a simplified non-convex MeshCollider after review; dynamic Rigidbody geometry requires suitable convex/primitive collision. A `COL_` mesh is only an authoring helper: Unity does **not** automatically turn an Unreal-style UCX/COL name into the desired prefab collision.

Keep colliders separate from visual LODs, do not leave renderers on helper collision meshes, and use real-scale bounds. Static props have no Rigidbody unless they actually move. For moving props specify mass, center of mass and collision mode in their gameplay prefab. Weapons/characters use existing gameplay attack/hurtbox systems; a render mesh does not become an active damage collider automatically.

The crate prefab is an identity root plus a nested FBX model instance, external material override and one BoxCollider at center (0,0.5,0), size (1,1,1), not a trigger. Re-exporting the model preserves the linked visual; gameplay components belong on the Unity wrapper/variant. Never unpack an imported FBX merely to fix its scale.

## Humanoid rigs, creatures and animation acceptance

Use an approved master skeleton for humanoids, a valid symmetric T-pose/rest pose, consistent bone names/roll and normalized weights. Limit to four meaningful influences per vertex as a baseline; inspect elbows, shoulders, knees and extreme poses. Keep root transforms applied, place the root at ground center and preserve the required hips/root-motion hierarchy. Do not rename/reparent approved bones casually. Export deform bones and required ancestors; exclude control rigs and Blender-only constraints after baking.

In Unity, configure the Humanoid Avatar and confirm it is valid/human with correctly mapped bones. Animation FBXs must use the matching approved Avatar, with reviewed clip ranges, looping, root rotation/position and in-place versus root-motion policy. The current server-driven player motor owns translation: locomotion/attack clips should be in-place unless a gameplay milestone explicitly changes that authority. Quadrupeds/non-humanoid creatures use Generic rigs and a consistent root node. Check retargeting and skinning before assigning Animator controllers.

No character, humanoid skeleton or animation set is created here. Their standards and review gates are defined; their first real delivery still requires Avatar, deformation and clip tests. This milestone's end-to-end proof is the static crate.

Milestone 40 adds the first validated Humanoid delivery; see [CharacterVisual.md](CharacterVisual.md). Its shared model/animation imports preserve hierarchy for Avatar reuse, and its animation-only export excludes mesh objects.

## Category delivery rules

- **Static meshes:** SM_ prefix, real dimensions, reviewed pivot, applied transforms, UVs, normals, one clean model hierarchy; reviewed lightmap/static flags at placement time. Destructible/movable variants must not inherit static assumptions.
- **Weapons:** SM_ visual, grip origin and forward tip, measured size, attachment markers, intentional material slots, no live collider in the visual asset. Reference the finished prefab through the existing WeaponDefinition.weaponPrefab; damage/range remain gameplay data.
- **Environment props:** modular dimensions/snap points, ground or hinge pivot, simple collision, appropriate lightmap UVs/LODs, no cameras/lights exported accidentally. Preserve a clean visual prefab and use gameplay variants for interactable behavior.
- **Characters/creatures:** SK_ prefix, approved rig/rest pose, feet/root reference, valid weights/Avatar or Generic root, reviewed capsules/hurtboxes in the gameplay wrapper, and matching animation deliveries.

## Daily workflow and automation

Run commands from the project root with Python 3.11+:

```powershell
# Resolve an existing executable (BLENDER_EXE and --blender are supported).
python Tools/ArtPipeline/blender_tool.py

# On Windows x64, provision the verified portable pinned LTS if absent.
python Tools/ArtPipeline/blender_tool.py --download

# Rebuild ONLY the milestone calibration source/texture/FBX.
python Tools/ArtPipeline/blender_tool.py --create-test

# Normal future re-export from an existing reviewed Blender source.
python Tools/ArtPipeline/blender_tool.py --export ArtSource/Environment/SM_TestCrate/SM_TestCrate.blend --output Assets/Art/Environment/Models/SM_TestCrate.fbx
```

1. Model in Blender under EXPORT, measure, apply transforms and check UVs/material slots. Keep WORK/REFERENCE outside EXPORT.
2. Save the `.blend` under ArtSource. Export through the shared script; do not run the test generator on production source files.
3. Let Unity import the FBX and textures. Inspect the defaults, rig mode and material mapping; preserve `.meta` files.
4. Create/reuse external URP materials. Assemble a Unity prefab around the linked model, with explicit collision and any approved LODGroup/rig configuration.
5. Compare against the 1m Unity reference and verify front/right/up, pivots, normals, material appearance and collision in the calibration scene.
6. Test the actual gameplay prefab at third-person distance before replacing a placeholder. Record size, pivot exceptions, texture/triangle budget and rig/LOD decisions with the asset.

Unity menus: **Heroes of Dragonland → Art Pipeline → Create Required Folders** and **Build Test Crate Prefab and Scene**. Close the calibration scene before rebuilding it. The latter regenerates the controlled test scene and reapplies only the crate's own import defaults; it retains the existing prefab and its GUID. Do not use it as an uncontrolled batch-prefab generator.

## Milestone verification and review checklist

The Editor validator fails on wrong scale, front/right/up markers, corrective transforms, missing UV channels/materials/texture, broken prefab links or the wrong collider. Play validation drops a temporary Rigidbody sphere onto the crate and checks that it rests on the 1m top surface. The temporary probe is not saved. The source re-export path is exercised separately from procedural generation. Reimport the FBX and rerun the validator before accepting a toolchain change.

- Source: `ArtSource/Environment/SM_TestCrate/SM_TestCrate.blend` and calibration manifest.
- Model: `Assets/Art/Environment/Models/SM_TestCrate.fbx`.
- Material/texture: `M_TestCrate.mat`, `T_TestCrate_BaseColor.png` in the category folders above.
- Prefab: `Assets/Art/Environment/Prefabs/SM_TestCrate.prefab`.
- Scene: `Assets/Art/Environment/Scenes/ArtPipelineValidation.unity` (open it directly to inspect; it is not added to gameplay build scenes).
- Validator: `ArtPipelineTestAsset.Validate(scene)` and `ArtPipelinePlayValidation.Start()` via Unity MCP; command wrappers are under Tests/EditorCommands.
- Visual evidence after Play validation: `Builds/art-pipeline-calibration.png` (local ignored output).
- Final review: metric bounds and all axes pass, outward shading/UVs are sensible, external material assigned, collider blocks physics, Console has no errors, source/FBX/material/prefab/.meta are included together.

New files are confined to ArtSource, Tools/ArtPipeline, Assets/Game/Art, the requested Assets/Art categories, this document and Editor command wrappers. Existing art and gameplay scenes are retained; no assets are deleted. No runtime gameplay architecture is added. The project is returned to its previous scene after calibration testing.
