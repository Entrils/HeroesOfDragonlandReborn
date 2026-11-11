# Milestone 41 — locomotion and combat animation

The character presentation remains a child of the existing gameplay player. The server motor, capsule, melee hit geometry, resource costs, invulnerability, defense and respawn remain authoritative. Animator root motion is disabled. No state machine behaviour or animation event calls gameplay code.

## Data flow

```text
Player input / bot intent → existing server movement, combat, dodge and stats
                         ↓ accepted replicated snapshots
Rendered root velocity + NetworkCharacterAnimationState observation
                         ↓ NetworkCharacterVisual
CharacterVisualState → CharacterVisual → Animator / presentation cues
```

`NetworkCharacterAnimationState` adds cosmetic metadata to all four player prefabs: accepted input's lock-on mode, observed grounding/vertical velocity, damage sequence/tick, death tick and stagger start tick. Only admitted state-authority server players/bots publish it. It observes `DamageReceiver.DamageTaken` and `CharacterStats.Died`; it cannot authorize damage, movement, targeting or defense. Lock-on input is a presentation flag, never proof that a target or attack is legal.

`NetworkCharacterVisual` runs for local, bot and remote replicas. Planar velocity comes from the rendered transform in accepted root-local coordinates; large teleport displacements are filtered. Attack/dodge sequence, busy state, start tick and duration come from existing combat/dodge replication. Blocking, stagger and death come from existing public snapshots. It uses the runner's local/remote render clock, including accepted action age when an observer joins mid-action. No local input prediction starts combat animations before acceptance.

## Small Animator modules

Controller: `Assets/Art/Animations/PrototypeHumanoid/AC_CharacterProduction.controller`.
Clip mapping: `CharacterAnimationLibrary.asset` alongside it. The legacy M40 controller is preserved.

| Layer | Contents | Responsibility |
| --- | --- | --- |
| 0 Base Layer | Locomotion, LockOnLocomotion; Airborne submachine: Jump/Fall/Land | Always evaluated; movement pose |
| 1 Combat | Attacks: LightAttack/HeavyAttack; Defense: Block; Reactions: HitReaction; Empty | Override with Humanoid upper-body mask; legs/root remain locomotion |
| 2 Actions | Abilities: Dodge; Reactions: Stagger; Life: Death; Empty | Full-body override for committed whole-body poses |

The free blend tree uses Speed with Idle 0, Walk 1.8 and Run 5 m/s. The lock-on directional blend tree uses local MoveX/MoveZ, with Idle, forward Walk/Run, Backward and left/right Strafe. Facing still inherits the existing accepted gameplay rotation. The lock-on graph never rotates the motor.

Each layer has one-way Any State routes selected by integer parameters. Modules contain at most three states; there are no pairwise transitions between every action. Routes cannot transition to themselves and have no exit-time authority. Write Defaults is off throughout. `CharacterVisual` fades override weights at 15/second and explicitly starts a newly accepted sequence with a short crossfade and accepted clip offset. Repeated attacks of the same kind therefore restart without trigger RPCs. Empty/zero-weight layers release their pose to locomotion.

Presentation priority: Death → Stagger → Dodge → HitReaction → Light/HeavyAttack → Block → locomotion. This resolves overlapping snapshots for display only. Server components independently decide which actions are legal. Death holds its final non-looping pose until replicated revival; respawn releases both override layers. Attack exit/interruption emits WeaponTrailOff to clean up presentation.

## Parameter contract

All parameters are written by `CharacterVisual.Apply`; Animator parameters are never read to decide gameplay.

| Parameter | Type | Meaning / source |
| --- | --- | --- |
| Speed | Float | Rendered planar m/s, clamped to 8, smoothed 0.1 s; zero when dead or match inactive |
| MoveX / MoveZ | Float | Rendered local velocity in m/s: right/forward positive |
| LockedOn | Bool | Server-observed cosmetic lock-on input; selects directional locomotion |
| Grounded | Bool | Inverse of accepted airborne observation |
| VerticalSpeed | Float | Server observed vertical m/s, clamped ±30; diagnostic/extension hook |
| AirState | Int | 0 Grounded, 1 Jump, 2 Fall, 3 Land; selects base module |
| Action | Int | Resolved presentation action; diagnostic mirror of the enum below |
| CombatAction | Int | Selected action if assigned to layer 1, otherwise 0 |
| FullBodyAction | Int | Selected action if assigned to layer 2, otherwise 0 |
| ActionSpeed | Float | Clip length / accepted action duration, clamped 0.1–8; otherwise 1 |
| Dead | Bool | Replicated stats death |
| Attacking / HeavyAttack | Bool | Accepted attack in swing interval / accepted attack kind |
| Dodging | Bool | Accepted dodge within its duration |
| Blocking / Staggered | Bool | Accepted combat defense state |
| HitReacting | Bool | Accepted damage sequence within a 0.25 s presentation interval |

Action values (`CharacterAction`): 0 None, 1 Dodge, 2 LightAttack, 3 HeavyAttack, 4 Block, 5 HitReaction, 6 Stagger, 7 Death. Enum values and layer names/paths are part of the controller contract. `CharacterAnimationModel` centralizes this mapping and the display priority. Do not independently add different priorities in the network adapter or controller.

Airborne display selects Jump for positive vertical velocity above 0.1 m/s, otherwise Fall. A grounded edge displays Land for 0.16 s. Initial observation does not invent a landing. The current multiplayer motor is planar and has no jump gameplay input: these three states support observed server vertical motion, and are exercised by controlled fixtures. Adding real jumping/gravity is a separate authoritative movement task. Ground observation currently matches the existing one-meter root-to-feet capsule convention, casts downward 0.4 m, ignores self/triggers and requires an upward surface normal. Update that observation if the gameplay capsule convention changes.

## Safe animation events

`CharacterAnimationEvents` lives on the Animator object and forwards only to `CharacterVisual.PresentationCue`. Allowed cues are Footstep, WeaponTrailOn, WeaponTrailOff, Impact and DodgeWhoosh. Footsteps require grounded motion; attack cues require a current light/heavy action; dodge cues require Dodge; dead characters reject clip cues. Sounds, trails and particles can subscribe to that event. Blend-tree events may overlap; audio consumers can throttle footsteps.

**Impact is a VFX/audio cue, not a damage callback.** Do not enable hitboxes, spend resources, change invulnerability, start/finish gameplay actions, or send damage RPCs from these methods. Existing `MeleeAttack.TickAttack` / `NetworkPlayerCombat.AfterTick` retain server hit windows and damage policy. Delayed/stale Animator events are allowed to affect presentation only. No gameplay hit-window event was added because existing server windows already handle this safely.

## Authoring and extension

Milestone 42 supplies the weapon-specific override integration through [WeaponMovesets.md](WeaponMovesets.md). Accepted weapon item identity and move index select per-step LightAttack/HeavyAttack clips without changing this graph or its parameter contract.

The Blender placeholder source now supplies distinct in-place Humanoid clips for every required motion (plus the preserved legacy Attack clip). Idle/Walk/Run/Backward/StrafeLeft/StrafeRight/Fall/Block/Stagger loop; other clips do not. Generated FBXs and their Humanoid imports use the shared Avatar and baked pose/root transforms.

Regenerate controlled placeholder art with `python Tools/ArtPipeline/blender_tool.py --create-humanoid`, then run **Heroes of Dragonland → Characters → Build Humanoid Visual Pipeline** through Unity MCP in Edit Mode. This updates the library, visual prefab and four network player prefabs, including Fusion's prefab table. The production builder preserves an existing controller so reviewed artist changes survive rebuilds; structural controller migrations should be explicit Editor tooling, not destructive automatic regeneration.

Replace placeholder clips through the library **and** the relevant controller state/blend child, keeping valid Humanoid retargeting, loop/import settings and presentation-only events. For weapon-specific variants, use an AnimatorOverrideController with the same parameter/layer contract and matching duration mappings. Do not create a second gameplay controller inside the visual. Existing hand/back/head/chest sockets and target aim remain compatible.

Network layout/input changed: rebuild both host and clients from the same revision. Mixing an older M40 player with M41 is unsupported.

## Verification

`CharacterAnimationValidation.ValidateAssets()` checks all 16 motion mappings, Humanoid clips, three bounded layers, upper-body mask, Write Defaults policy, lack of pairwise transitions, allowed events and all player prefab references. `Start()` in Play Mode creates an isolated visual fixture and verifies actual Animator states for every required motion, free/lock routing, mid-action phase, repeated sequence restart, death priority, trail cancellation, revival and stale-event rejection. It also verifies no root displacement or gameplay colliders are introduced. The existing M40 humanoid/network checks use the new full-body death layer.

`-hodrAnimationCheck` installs a development-only acceptance probe in explicitly launched test players. It sends normal input intentions through `MultiplayerBootstrap` and records real remote poses. Its server fixture arranges two humans for genuine melee, applies a server stagger/death for lifecycle checks, and supplies a brief vertical displacement solely to exercise future vertical-motion presentation. It does not fabricate replicated animation actions or bypass damage policy. Run matching development host/client builds in one room with four bots on the host. Both processes should report `[AnimationNetwork] PASS`; reports identify any missing remote motions. The probe is absent from normal sessions and its code is excluded from release builds.

Final animation quality, weapon-specific authored moves, foot planting and latency/jitter polish still require art and playtesting. This milestone establishes production separation and extensibility using placeholder motion art.

## Changed files

New runtime: CharacterAnimationModel, CharacterAnimationLibrary, CharacterAnimationEvents, NetworkCharacterAnimationState and opt-in CharacterAnimationNetworkProbe under `Assets/Game/Presentation/`. New Editor tooling: ProductionCharacterAnimatorBuilder and CharacterAnimationValidation under `Assets/Game/Art/Editor/`.

Updated: CharacterVisual, NetworkCharacterVisual, CharacterVisualNetworkProbe; NetworkPlayerInput, NetworkLocalTargeting and MultiplayerBootstrap for cosmetic lock-on metadata; HumanoidAssetBuilder, HumanoidPrefabSetup and HumanoidVisualValidation; Blender humanoid generator and its source/FBXs; shared visual prefab and all four existing player prefabs. Added production controller/library, required clips and Unity metadata. No assets deleted; no gameplay movement or damage algorithm rewritten.
