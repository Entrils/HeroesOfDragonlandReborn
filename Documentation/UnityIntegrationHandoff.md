# Home-computer handoff — M54–M59

## What is delivered

This batch was authored on a computer without Unity. It contains source models/FBXs/textures, look-development boards, documentation, Unity Editor setup/validation tools, modular equipment presentation and pooled VFX code. It does **not** contain the Unity-generated integration results: new metadata, imported material remaps, new prefabs/scenes or final biome placement. Those need to be produced and checked in the existing Unity project through Unity MCP.

M52 and M53 were explicitly deferred. Do not implement them or mark them complete as part of this handoff. Existing gameplay layout/camps, authoritative equipment, combat rules and metadata must be preserved. M58/M59 runtime/Editor scripts have not been compiled in Unity here; fix compile/weaver errors before running any builder.

Read AGENTS.md and the original DESIGN.md if it exists on the home computer. DESIGN.md was absent from this clone and was not ignored by the checked ignore rules. Preserve any local original rather than inventing/replacing it. Reconcile conflicts with the approved ArtDirection before costly production art changes.

## Safely transfer from GitHub

Close Unity before updating files. Open a terminal at the existing home project root, not inside Assets.

```powershell
git status --short
git branch --show-current
git lfs version
```

If status is clean and the intended branch is main:

```powershell
git lfs install --local
git switch main
git pull --ff-only origin main
git lfs pull
git status --short
git log -1 --oneline
```

Pull transfers missing Git objects and updates changed tracked files; it does not re-clone the whole project. LFS fetches the binary content required for the checked-out revision that is absent from the local cache. `Library`, `Temp`, `Logs` and local tool binaries remain ignored/local. The first Unity import of the new assets can take time even though Git downloaded only the missing changes.

If there are local edits or untracked files, **do not run a reset/clean or overwrite them**. Save them in a deliberate local commit or a named stash before pulling. Example optional stash workflow:

```powershell
git stash push --include-untracked -m "home-before-m54-m59"
git pull --ff-only origin main
git lfs pull
git stash apply
git status --short
```

`stash apply` retains the saved stash, including if conflicts occur. Do not drop it until the restored changes have been reviewed. A stash does not include already committed local history. If `--ff-only` refuses because home and origin/main have diverged, stop and reconcile the actual commits deliberately with the coding agent. Do not force-reset, force-pull, delete the project or copy the entire Assets folder over it.

## Integration order

1. **Preflight.** Read this document plus ArtDirection.md, ArtPipeline.md, ModularEnvironmentKit.md, AncientForest.md, Biomes.md, VisibleEquipment.md and GameplayVfx.md. Inspect current Hierarchy, open scene, existing player/enemy/camp prefabs, local edits and Console through MCP. Let Unity generate new metadata once; preserve existing `.meta` files. Resolve every compile/weaver error before setup. Do not start new milestones.
2. **Calibration.** Existing pipeline was pinned to Blender 4.5 LTS, while these offline exports used Steam Blender 5.3.0. Check imported meters, pivots, Front/Right/Up sockets, materials and normals in Unity using the existing calibration tools. Do not compensate with 0.01/100 scales, rotated wrappers or a global importer change.
3. **M54 benchmark.** Create/save `Assets/Art/Environment/Scenes/VisualDirectionBenchmark.unity` through MCP using existing humanoid/crate/weapon assets and simple placeholder materials. The Blender board is in ArtSource/Benchmarks/VisualDirection. Include comparable A/B silhouettes/crests, chest/camp/boss samples and warning/readability checks. Follow ArtDirection.md; keep it out of build scenes.
4. **M55 kit.** Through MCP invoke `HeroesOfDragonland.Art.Editor.FortressRuinsKitBuilder.Build()` and `.Validate()`. Creates 21 linked prefabs, shared materials and FortressRuinsKitTest.unity. Existing output protection is intentional: if already created, inspect/repair it without deleting GUIDs or blindly rebuilding. Check seams, scale, arch/gate openings, colliders and LODs. No final-map fortress replacement is requested.
5. **M56 forest assets.** Invoke `AncientForestAssetBuilder.Build()` and `.Validate()` in the same namespace. Requires M55 Crate/Barrel prefabs. Review the separate forest scene and shared material/texture references. In the saved battlefield, invoke `AncientForestBattlefieldPass.Apply()` only after inspecting actual camps/cover. It preserves original collision and serialized camp data and installs conservative tree/material/vegetation presentation. Solid prop placement and visual review remain work to complete through MCP.
6. **M57 set.** Invoke `BiomeSetBuilder.Build()` and `.Validate()`. Review the four-region comparison. Optionally apply `.ApplyLoadedGrounds()` to the saved battlefield, then fit actual transition collars, regional props and limited light accents through MCP. Preserve the existing region footprints, paths, camp/objective anchors and rotational fairness. These ground passes mark the scene dirty; review and save it through MCP before the next command that requires a clean scene.
7. **M58 equipment.** Invoke `VisibleEquipmentSetup.Build()` and `.Validate()`. This creates presentation assets and assigns the catalog to existing player prefabs. Run `VisibleEquipmentValidation.Start()` in Play Mode and inspect Status/Console. Then test accepted/rejected equip/unequip, all four weapon families, four armor slots, death, respawn, late observer and actual reconnect on host/client. The production item catalog currently lacks Head/Hands/Legs items; isolated Play validation uses temporary definitions and does not add stats/loot. Do not silently invent production gear to make the test pass.
8. **M59 VFX.** Invoke `GameplayVfxSetup.Build()` and `.Validate()`, then `.InstallLoadedScene()` for the intended active scene. It creates the 17 pooled effects and installs presentation/outcome adapters. Check/bake/reimport Fusion scene/prefab behavior data after adding NetworkImpactVfx and rebuild both peers from matching content. Run `GameplayVfxValidation.Start()` in Play Mode; then test actual confirmed impacts, magic, progression, loot/camps, objective deaths, victory and reconnect/late observation on two processes.
9. **Combined acceptance.** Check both team identities, lock-on/camera space, chest prompts, camp recognition and all three boss telegraphs against every biome. Profile a six-player session and effects overlap. Verify original authority/gameplay behavior and traversal remain intact. Rebake the existing NavMesh asset only if a reviewed collider/geometry change actually requires it, preserving its GUID.
10. **Save and report.** Inspect Console again, save all reviewed scenes/prefabs/materials/data through MCP and record actual test outcomes in the milestone docs. Distinguish passed, failed and still pending work. List modified/new/deleted files and architectural changes. Do not automatically commit or push the home integration without a new instruction.

All names above are under `HeroesOfDragonland.Art.Editor`. Each milestone document contains precise paths, acceptance criteria and limitations. Prefer finishing one dependency stage and fixing its issues over blindly executing the whole list against partially valid assets.

## Work that must not be repeated blindly

- Do not rerun `create_*.py` source generators over reviewed/manual art. They overwrite their own named outputs. FBXs and editable sources are already delivered; re-export an individual source only when a specific fix requires it.
- Do not rerun the full humanoid pipeline or reset existing scenes simply to assign new catalog references. Inspect and reuse the current rig/Animator/prefabs.
- Do not manually edit serialized Unity YAML or manufacture `.meta` GUIDs.
- Do not claim prebuilt Unity prefabs, passed compilation, networking, visual approval or measured performance based solely on offline reports.
- Do not redesign combat, networking, inventory, camp positions, map layout or M52/M53 while completing this integration.

## Checks already run before transfer

- M55: 21 FBX round-trips; bounds, UVs, material slots, topology and basis markers.
- M56: 19 FBXs and 13 texture maps; geometry/UVs, static collision shells, masks and normals.
- M57: five FBXs, 24 maps and five PBR transition pairs, including endpoint matching.
- M59: 24 real pure C# policy checks for pool budgets/ownership/sequence handling; SVG design-sheet schema/XML check.

These are useful preflight evidence, not replacements for the Unity/Play/multiplayer checks above. M58 and the Unity-dependent portions of M54–M59 remain unverified until performed on the home computer.

## Prompt to paste into the home coding session

```text
Continue Heroes of Dragonland Reborn on the main computer with Unity and Unity MCP.

Read AGENTS.md, the original DESIGN.md if present, and Documentation/UnityIntegrationHandoff.md.
Inspect Git status, current branch, current Unity scene/Hierarchy and Console first. Preserve all local changes and existing .meta/GUIDs. If the transferred work has not been pulled yet, safely update from origin/main with Git LFS; never use reset --hard, clean or force operations to resolve local changes/divergence.

Finish the Unity integration of M54–M59, following the dependency order and precise commands in UnityIntegrationHandoff.md. The offline batch contains sources/exports/textures and setup code; actual Unity prefabs/scenes and full validation are not already complete.

Fix all compile/import/Fusion-weaver errors before invoking builders. Use Unity MCP for scene/prefab/component/material changes, Hierarchy, Console and saves. Inspect existing outputs instead of overwriting authored assets or GUIDs. Do not regenerate Blender sources or rebuild the whole humanoid pipeline without a specific demonstrated need.

Complete the M54 benchmark, M55 kit prefabs/test section, M56/M57 biome integration and transitions, M58 visible equipment and M59 pooled VFX. Preserve existing map layout/camp positions, server authority, gameplay rules and team/telegraph readability. M52 and M53 remain deferred and must not be implemented as part of this task.

Run the prepared validators, Play checks and actual host/client equip/unequip/death/respawn/reconnect/late-observer tests. Verify Fusion prefab/scene baking and matching builds after the new NetworkImpactVfx behavior. Test combined 3v3/PvE/VFX readability and profile six-player performance where possible.

Proceed through all authorized implementation, fixes and checks. Save reviewed scenes/assets through MCP. Update the milestone docs with real outcomes and remaining manual checks; do not report a scenario passed if it was not run. At the end list changed files, architecture decisions, Console/build results and remaining issues. Do not automatically commit or push the home integration.
```
