# Milestone 58 — Visible Equipment System

## Scope and current status

M58 extends the existing `EquipmentVisualHooks → CharacterEquipmentVisual → CharacterVisual` presentation path to all six equipment slots. Sword, Greatsword, Spear and Staff continue to use their actual existing WeaponDefinition prefabs. Head, Chest, Hands and Legs use configurable modular rigid armor pieces attached to Humanoid bones.

**Implemented in source:** recipe/catalog assets, event-driven slot reconciliation, idempotent weapon/armor updates, immediate removal, hand/back handling, inert-prefab safety, reconnect suspension handling, an explicit Unity setup command, Play validation and an opt-in actual-network observation probe.

**Not executed here:** Unity compilation, Console inspection, asset/prefab creation, rig alignment review, Play validation or real host/client/death/respawn/reconnect tests. This computer has no Unity Editor/MCP; `dotnet --list-sdks` also finds no installed SDK, so no standalone C# compilation is claimed. M58 requires the Unity handoff below before being considered complete. No serialized Unity assets have been manually edited.

AGENTS.md was read. DESIGN.md remains absent. The existing character rig, animation, weapon definitions, all six equipment slots, public snapshot adapter and reconnect suspension code were inspected. No server-authoritative equipment, inventory, stats, combat, item definition, respawn or reconnect implementation was changed.

## Architecture decision

Use **modular rigid bone attachments**, not skinned-mesh retargeting or wholesale body replacement. The current mannequin already has a valid Humanoid rig and deliberately segmented rigid weighting, so head protection, a breastplate frame, gauntlets and articulated thigh/shin guards can follow the matching bones without a second skeleton or skin-remapping system.

This is the smallest approach compatible with the current character pipeline. The original body/Animator, gameplay capsule, targeting socket, root motion policy and animation controller remain intact. No body renderer is globally hidden, keeping the existing team tint visible; the sample chest frame leaves its central field open. Human review of both teams with every armor combination is still required.

The sample armor is functional placeholder geometry for system acceptance, not finished production armor. Cloth, continuous deforming garments and skinned replacement bodies are outside this milestone. `SkinnedMeshRenderer`/Animator prefabs are deliberately rejected by this rigid-part path; such content needs a separately reviewed binding implementation, not an unbound skin silently attached to a hand.

## Data and resolution

`EquipmentVisualDefinition` is a presentation-only ScriptableObject with an EquipmentSlot, one or more part recipes and an optional main-hand stowed pose. Each part names a prefab, socket or Humanoid bone, and local position/rotation/positive scale. Bone references use the Humanoid API, not hardcoded transform-name paths.

`EquipmentVisualCatalog` resolves:

1. An explicit item-ID override, when configured.
2. A main-hand WeaponArchetype recipe for Sword, Greatsword, Spear or Staff.
3. A default armor recipe for Head, Chest, Hands or Legs.

Definitions validate mounts, finite poses, slot consistency and safe prefab content. Catalog validation rejects duplicate IDs/archetypes/default slots. ItemDefinition, WeaponDefinition and the authoritative item catalog do not gain new mutable presentation state.

Weapon recipes may leave their prefab empty. The view then uses `item.Weapon.WeaponPrefab`, preserving distinct item art such as IronSword/Dragonblade instead of replacing every sword with one generic model. An item-specific recipe can override that visual deliberately. Without an assigned visual catalog, the existing weapon-prefab fallback continues to work; armor requires the new catalog/defaults and otherwise clears stale armor with a warning.

## Slot and mount contract

| Slot | Sample representation | Mount |
| --- | --- | --- |
| MainHand | Actual Sword/Greatsword/Spear/Staff prefab | Existing RightHand socket, or Back via explicit local stow hook |
| OffHand | Existing weapon fallback or an explicit offhand recipe | Existing LeftHand socket |
| Head | Open-face helmet shell | HumanBodyBones.Head |
| Chest | Open breastplate frame, preserving a center team-color field | HumanBodyBones.Chest |
| Hands | Two gauntlet pieces | LeftHand and RightHand bones |
| Legs | Two thigh and two shin guards | Left/RightUpperLeg and Left/RightLowerLeg bones |

Weapons and gauntlets can coexist because armor uses separate instances on bones and does not occupy CharacterVisual's weapon attachment dictionary. Socket transforms are never repositioned by equipment. Poses are applied only to spawned part roots.

The setup command calculates sample armor offsets from the approved prototype's rest-space measurements and actual mapped bone transforms. These are art profiles for that rig, not a claim that all future humanoid bodies have identical dimensions. Recalibrate recipes for a new body in Unity. No scale correction or body/hitbox resizing is performed by the runtime.

`SetWeaponStowed(bool)` remains a **local presentation hook**. It reparents the same main-hand instance and applies the selected recipe pose. It creates no additional weapon and introduces no input, combat state or network field. The existing Back socket already owns the rest orientation. Replicated stow behavior would require an accepted gameplay state to drive this hook on each replica; this milestone does not pretend a local hook is a synchronized stow action.

Unequipped weapons are removed. Optional decorative unequipped weapons on the back are intentionally not implemented, avoiding stale gear and any need to reveal private inventory.

## Events, duplicates and ownership

EquipmentVisualHooks already publishes all six changed slots. CharacterEquipmentVisual now subscribes to all of them and queries the full current state on enable. It keeps one owned view per EquipmentSlot, containing the item ID, resolved recipe/prefab and instantiated parts.

Repeated snapshots/Refresh calls with the same item/recipe reuse existing instances. A changed or empty slot clears only its previous owned parts. Old objects are deactivated immediately before deferred destruction, preventing visible same-frame duplicates. Disable/despawn clears all owned views and unsubscribes; re-enable reconstructs from EquipmentVisualHooks rather than relying on missed events. Refresh can also reconstruct a part that was externally destroyed.

Armor mounts are all resolved before any part is spawned, preventing a half-equipped set when a required bone is missing. Invalid/missing recipes do not leave the previous item's armor visible. Weapons continue to use CharacterVisual's existing right/left/back attachment API, preserving its callers and tests.

`EquipmentVisualSafety` inspects prefab components **before Instantiate**. Only rigid mesh/transform/LOD and native collider/rigidbody components are allowed; scripts, missing components, Animator, skinned renderers, lights, cameras and particle systems are rejected. Instances are created under an inactive temporary parent; colliders, including inactive descendants, are disabled and rigidbodies made kinematic/non-colliding before activation under the mount. Visual art cannot create a new damage component or network object through prefab Awake.

## Networking and lifecycle

The existing authority path remains:

```text
Accepted server equip/unequip
  → Equipment.Changed
  → NetworkEquipment publishes six public equipped IDs
  → NetworkEquipment.Render on a replica
  → Equipment.ApplyNetworkSnapshot
  → EquipmentVisualHooks
  → local presentation spawning
```

No new RPC, Networked property, inventory mutation, stat change, client equip prediction or authority bypass is added. Remote visuals resolve public equipped IDs through existing local content; private inventory contents/quantities are not needed. Visual asset catalogs must be included in the same content build for all peers.

**Death:** gear remains attached to the animated body and follows its bones. Death does not spawn dropped copies, detach armor or alter item ownership. No additional damage/physics hitboxes are enabled.

**Respawn:** the visual follows whatever authoritative equipment state the existing respawn logic retains or clears. A retained avatar keeps its pieces; a recreated/reenabled presentation queries all slots. There is no invented restore-inventory policy.

**Reconnect:** the existing NetworkTestPlayer suspension pass caches and hides renderers/colliders. The equipment view additionally hides its owned part roots after suspension rendering, defers snapshot-driven spawning while suspended and refreshes after resume. This avoids armor appearing on a hidden reserved avatar or missing an update that arrived while it was hidden. It reads the existing suspension state only when the NetworkObject is valid; no reservation or admission code is changed.

**Late join/remote player:** the same full-state query and existing public snapshots reconstruct visible equipment independently of whether the observer saw the original equip event.

## Unity setup through MCP

1. Open Unity 6000.6.0f1 on the main computer. Let it import new scripts and generate `.meta` files; preserve existing metadata. Inspect Console and fix compile/import errors before running setup. Confirm the existing Humanoid visual/Animator/socket pipeline is valid.
2. In Edit Mode invoke `HeroesOfDragonland.Art.Editor.VisibleEquipmentSetup.Build()` through MCP, or the menu **Heroes of Dragonland → Characters → Build Visible Equipment (M58)**. It creates only presentation assets under `Assets/Art/Equipment/` and assigns the shared catalog to existing humanoid network-player prefabs under Assets/Game/Data.
3. Generated assets: one shared iron material; five armor-part prefabs (Head, Chest, Hand, Thigh, Shin); eight visual definitions (four weapon families plus four armor slots); one EquipmentVisualCatalog. Existing authored meshes/recipes/catalog assets are retained on rerun. Player prefab GUIDs and their existing CharacterVisual references are preserved.
4. The setup adds an inert, opt-in VisibleEquipmentNetworkProbe to the player prefabs for development validation. It is not a NetworkBehaviour and does not add replicated state. Its observation code is compiled out of non-development players.
5. Run `VisibleEquipmentSetup.Validate()` through MCP. Check all player catalog references and family/slot coverage. Inspect prefab hierarchy, assigned renderers/materials, all mount poses, hand/back orientation, and animation clearance. Save reviewed prefabs/assets through MCP. No scene edits or manual YAML changes are needed for setup.

Current gameplay content contains ScoutArmor in Chest but no production Head/Hands/Legs item definitions. Setup therefore creates visual defaults for those slots without inventing item statistics or adding loot. Play validation uses temporary in-memory armor item definitions to exercise those slots. Actual production head/hand/leg items can resolve these defaults or receive explicit visual overrides when their game data is authored.

## Tests and their precise limits

No Unity tests below have been run on this computer. Source review and scoped Git whitespace/change checks do not establish Unity compilation or runtime correctness.

| Requested scenario | Prepared automated coverage | Remaining live check |
| --- | --- | --- |
| Equip | Four weapon families, all four armor slots, correct socket/bone, no visual collision | Inspect pose/clipping/team identity in actual gameplay |
| Unequip | Immediate hide/removal, each armor slot and replicated all-empty state | Accepted and rejected real RPC requests on host/client |
| Death | Real Animator death pose on a Unity fixture; pieces remain attached, equipment revision unchanged | Actual authoritative death seen by owner and remote observer |
| Respawn | Disable/re-enable/recreation of the view reconstructs retained gear | Actual server respawn policy and remote observation |
| Reconnect | Lifecycle reconstruction in fixture; read-only probe checks hidden/resumed live gear | Actual disconnect/reservation/rejoin, including changed/cleared state |
| Remote player | Trusted ApplyNetworkSnapshot adapter on a non-authority fixture, late subscription, stale revision, cannot locally unequip | Two real Fusion processes, owner/observer gear agreement and late join |

### Play fixture command

In Play Mode, after setup, invoke `HeroesOfDragonland.Art.Editor.VisibleEquipmentValidation.Start()` through MCP. Read its `Status` until PASS/FAILED and inspect Console. It uses real Unity prefabs, Animator, inventory/equipment logic and the existing trusted snapshot adapter. It checks weapon hand/back movement, all armor mounts, idempotent refreshes, removal, disabled collision, death pose, late subscription and view reconstruction.

Fixtures use inactive roots configured before Inventory.Awake, cloned in-memory item/weapon definitions with valid unique IDs, and an isolated ItemCatalog. Required attributes are removed only on those temporary definitions. It does not mutate production assets, grant live-match gear, fake a Fusion runner or claim a simulated disable is a real network reconnect. All fixtures are destroyed after the coroutine, and no test object is saved.

### Actual network probe and manual sequence

Build development players with the existing scene and start both processes with `-hodrVisibleEquipmentCheck`. In the Editor, a probe can also be enabled explicitly with `EnableForValidation()` through MCP. The probe is read-only: it observes the six public IDs and local Equipment model after a short settling delay, checks expected visual counts/activity and disabled visual colliders, and emits `[VisibleEquipment] PASS/FAIL` with owner/remote, death and suspension state. It does not execute the user actions; a PASS log alone is not proof that all lifecycle scenarios were exercised.

Use existing authoritative equip/unequip UI/RPC paths to equip each weapon and ScoutArmor, swap without unequipping, unequip, die, respawn, disconnect involuntarily, reconnect within the existing reservation window, join late and repeat a rejected/stale equip request. Confirm matching expected visuals on both peers and preserved team/target visibility. Test four-piece armor combinations in a controlled validation-content session containing the corresponding valid item definitions; do not alter server equipment arrays directly or infer private inventory from another player.

While an avatar is suspended, every retained equipment part must remain invisible. On resume, only the current authoritative gear must appear, with one instance per configured part. Verify that game root colliders, inventory/equipment revisions, combat reach, respawn timers and reservation state are unaffected by visual refresh/stow calls.

Record actual Unity Console results, fixture Status, development build result, both peers' scenario logs and screenshot evidence here before marking M58 complete. Console, Play, visual pose quality, true multiplayer respawn/reconnect and production armor art remain pending.

## Files and architectural changes

Modified:

- `Assets/Game/Presentation/CharacterEquipmentVisual.cs`: all-slot modular presentation, ownership/idempotence, suspension and lifecycle refresh.
- `Assets/Game/Presentation/CharacterVisual.cs`: existing attachment API now uses shared inert-prefab/physics safety, including inactive descendants.

New:

- `Assets/Game/Presentation/EquipmentVisualDefinition.cs`: rigid-part/pose recipe data.
- `Assets/Game/Presentation/EquipmentVisualCatalog.cs`: item/family/armor-default presentation lookup.
- `Assets/Game/Presentation/EquipmentVisualSafety.cs`: pre-instantiation validation and safe spawn/release.
- `Assets/Game/Presentation/VisibleEquipmentNetworkProbe.cs`: opt-in read-only development observation.
- `Assets/Game/Art/Editor/VisibleEquipmentSetup.cs`: MCP setup and asset/reference checks.
- `Assets/Game/Art/Editor/VisibleEquipmentValidation.cs`: isolated Play validation.
- `Documentation/VisibleEquipment.md`: this architecture and handoff record.

The only architectural extension is a presentation catalog plus modular rigid pieces. No changes to PlayerController, Equipment, NetworkEquipment, Inventory, ItemDefinition, network snapshots, combat, health, respawn or reconnect policy. No deleted files, auto commits or pushes. Generated presentation assets and new metadata/prefab assignments are still future Unity integration outputs, not files already created here.
