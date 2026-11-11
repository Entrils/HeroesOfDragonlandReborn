# Milestone 40 — humanoid character visual pipeline

The four existing network player prefabs now use a reusable humanoid visual child. The original server movement, collision capsule, combat, dodge, inventory, progression and respawn controllers are retained. The mannequin is original placeholder art generated through the Milestone 39 Blender workflow; it has a real weighted SkinnedMeshRenderer and a valid Unity Humanoid Avatar, rather than a collection of unrigged gameplay primitives.

## Assets and hierarchy

```text
Gameplay player root                       # Existing NetworkTestPlayer / NetworkTransform
  CapsuleCollider / stats / combat / etc.   # Existing gameplay authority
  Targetable                               # Aim reference points to the animated head socket
  NetworkCharacterVisual                   # Reads existing public network snapshots
  CharacterEquipmentVisual                 # Uses existing equipment visual events
  CharacterVisual                          # Local offset (0,-1,0), beneath center-rooted player
    CharacterVisual component              # Animator parameters, tint, attachment presentation
    Imported humanoid / Animator           # applyRootMotion = false
      Root / Hips / Spine / Chest / ...
        Socket_RightHand
        Socket_LeftHand
        Socket_Back
        Socket_Head
        Socket_Chest
```

The reusable visual prefab is `Assets/Art/Characters/Prefabs/PV_PrototypeHumanoid.prefab`. It has no NetworkObject, player controller, Rigidbody or collider. The existing gameplay-root capsule stays enabled and retains its original dimensions. The legacy capsule renderer and its visual-only child collider are disabled and retained for reference; no assets are deleted.

Source: `ArtSource/Characters/SK_PrototypeHumanoid/SK_PrototypeHumanoid.blend`. Model: `Assets/Art/Characters/Models/SK_PrototypeHumanoid.fbx`. Materials: `Assets/Art/Materials/Characters/M_PrototypeHumanoid.mat` and `M_PrototypeHumanoidDark.mat`. Animator controller and six animation-only FBXs are under `Assets/Art/Animations/PrototypeHumanoid/`.

## Rig and import contract

The 22-bone mannequin contains all required humanoid bones plus shoulders, neck, chest, toes and a root. Its rest pose is a T-pose. Body segments are intentionally simple and rigidly weighted to individual bones; this is functional placeholder skinning, not final organic deformation or finished animation art. Character front is Blender +Y → Unity +Z, up Blender +Z → Unity +Y, using the established meter-scale export profile.

The character model imports as Humanoid / Create From This Model with explicit HumanTrait bone mapping. Animation FBXs import as Humanoid / Copy From Other, referencing that same Avatar. **Preserve Hierarchy is enabled on both**: otherwise Unity can rename the single root after each filename and reject the copied Avatar configuration. Optimize Game Objects is off, keeping socket bones accessible. The builder rejects invalid/non-human Avatars or non-humanoid clips.

Milestone 41 extends this setup with all 16 required locomotion/combat motions, a three-layer production controller and a clip library. See [AnimationArchitecture.md](AnimationArchitecture.md) for the current state and parameter contract. The legacy Attack asset/controller are preserved. Translation and rotation remain baked into the pose and Animator root motion is disabled. Accepted dodge displacement, invulnerability and attack damage still belong to the existing gameplay components.

## Presentation components

`CharacterVisual` is network-independent. It owns the Animator reference, body renderer tint and five socket references, and accepts a `CharacterVisualState` through `Apply`. It provides `GetSocket`, `Attach`, `ClearAttachment` and `MoveAttachment`. Spawned attachment instances have physics collision disabled, so a weapon visual does not become a damage/hurtbox implementation.

`NetworkCharacterVisual` is a presentation adapter on the gameplay root. It runs for local owners, bots and real remote replicas. Locomotion is derived from rendered transform displacement; teleports are filtered. Team tint and death use existing replicated player/stats data. Attack/dodge presentation uses accepted sequence/tick/busy snapshots and the runner's local/remote render clock. No Animator networking component, new gameplay RPC or extra input authority is introduced. Visual rotation inherits the accepted root rotation.

`CharacterEquipmentVisual` subscribes to `EquipmentVisualHooks`. Main-hand equipment resolves `ItemDefinition.Weapon.WeaponPrefab`, so both authoritative equipment changes and public replicated equipment snapshots create the same weapon visual. It can move the equipped weapon between RightHand and Back through `SetWeaponStowed`. That method is a presentation hook; a gameplay stow action would need an accepted gameplay state before using it across peers. There is no new stow input or inventory mutation here.

| Animator parameter | Meaning |
| --- | --- |
| Speed | Rendered planar speed, m/s; drives Idle/Walk/Run blend |
| MoveX / MoveZ | Local-space m/s driving lock-on directional clips |
| ActionSpeed | Fits the placeholder attack/dodge pose to the accepted action duration |
| Dead | Death state, held until normal revival |
| Attacking / Dodging | Accepted action presentation |
| Blocking / Staggered | Accepted defense presentation with dedicated clips |
| LockedOn | Server-observed cosmetic lock-on mode, including remote replicas |

This M40 table is a subset; the full current parameter list, clip mappings, layer routing and animation event policy are documented in [AnimationArchitecture.md](AnimationArchitecture.md).

The visual adapter does not read private remote inventory quantities or synthesize remote inputs. Main-hand appearance uses the existing public equipment snapshot. Head/chest/left-hand sockets are available for equipment presentation; no armor collection or skinned-armor retargeting system is fabricated in this milestone.

## Target lock and death

Targetable remains on the existing gameplay root, with its aimPoint bound to Socket_Head. Stats/death legality and targeting identity remain on that root. The camera and target selection systems are reused. The socket follows the animated head, including rotation. Death changes the humanoid pose through the Animator; it does not rotate the gameplay capsule, apply root motion, create a ragdoll or alter damage rules. Normal respawn clears replicated death and returns the Animator to locomotion.

The existing NetworkActionPresentation keeps its compatibility diagnostics, but suppresses the old procedural swing mesh and capsule roll when a CharacterVisual is present. Existing test scenes with the updated player prefabs receive the same visual setup.

## Authoring/rebuilding

```powershell
python Tools/ArtPipeline/blender_tool.py --create-humanoid
```

Then run **Heroes of Dragonland → Characters → Build Humanoid Visual Pipeline** in Edit Mode through Unity MCP. The generator is for the controlled placeholder source; do not run it over a finished artist-authored character. The builder preserves an existing controller/visual prefab and keeps player prefab GUIDs and gameplay components. It configures the shared Avatar/clip imports and installs the visual references on the four NetworkTestPlayer prefabs under Assets/Game/Data.

For final art, import a reviewed meter-scale Humanoid rig, create the visual prefab with CharacterVisual and an Animator following this parameter contract, bind the five socket references to mapped humanoid bones, and assign its renderers. Replace the visual child and update the NetworkCharacterVisual / CharacterEquipmentVisual references and root Targetable head reference. Keep the root offset appropriate to the unchanged gameplay capsule. Review model bounds for death/actions and retain root motion off. No art name or bone path is hardcoded in the runtime components.

## Verification

`HumanoidVisualValidation.ValidateAssets()` checks the valid Avatar, Humanoid motions, weighted bone references, all sockets, absence of gameplay authority/colliders in the visual prefab, root-motion policy, root capsule and head aim reference. Play checks cover locomotion parameters, right/left/back attachments, collision-free weapon visuals, an actual bone-changing death pose, revival, lock acquisition/rotation and root separation.

The opt-in development probe `-hodrHumanoidCheck` is inert in ordinary sessions. In a test host/client session it supplies normal human input intentions, equips catalog swords through the existing inventory/equipment path, and triggers deaths through server stats for acceptance testing. Both processes must observe local and **real remote** motion, remote equipped visuals, the Death Animator state and return to locomotion after normal respawn. It never replaces gameplay movement or sends a fabricated animation state.

Real host/client validation used two development players plus four bots (six total participants). Both logs reported `[HumanoidNetwork] PASS`; logs are local artifacts in `Builds/HumanoidValidation/host.log` and `client.log`. The successful development build had existing package/API/shader warnings, with no build errors. `Builds/humanoid-preview.png` is the inspected third-person preview. Long-session performance, final animation quality, foot planting and visual quality on final character art remain art/playtest work.

## File summary

Added runtime presentation: `Assets/Game/Presentation/CharacterVisual.cs`, `NetworkCharacterVisual.cs`, `CharacterEquipmentVisual.cs`, and opt-in `CharacterVisualNetworkProbe.cs`.

Added Editor tooling: `Assets/Game/Art/Editor/HumanoidAssetBuilder.cs`, `CharacterAnimatorBuilder.cs`, `HumanoidPrefabSetup.cs`, `HumanoidVisualValidation.cs`; added `Tools/ArtPipeline/create_humanoid.py` and test command wrapper. Added the Blender source, skinned FBX, visual prefab, two materials, controller, six clip FBXs and corresponding Unity metadata.

Modified: `Tools/ArtPipeline/blender_tool.py` and `export_fbx.py` for humanoid generation/animation-only export; `Assets/Game/Art/Editor/ArtAssetPostprocessor.cs` for preserved animated hierarchies; `Assets/Game/UI/NetworkActionPresentation.cs` for legacy presentation fallback; the BattlefieldPlayer, NetworkCampPlayer, NetworkFortressPlayer and NetworkTestPlayer prefabs. Updated ArtPipeline.md naming/import notes. No movement-controller code or gameplay scenes are rewritten, and no assets are deleted.
