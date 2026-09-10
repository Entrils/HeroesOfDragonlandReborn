# Multiplayer Milestone 3 — networked melee and dodge

## Authority and action flow

Owner input carries movement, attack/dodge buttons and an optional planar facing direction. It carries no damage value, hit list, resource value, action duration or invulnerability flag. The server admits input only for the connection's registered player object. Nonfinite movement/facing becomes zero; oversized vectors are clamped. Host/Server simulation remains the authority; Shared mode is unsupported.

The server motor processes dodge intent first, then attack intent, then movement. Dodge wins simultaneous idle requests if it can pay its cost. Neither action interrupts an existing attack/dodge or its recovery. Holding a button deliberately repeats actions, but every accepted action must finish recovery and pay stamina again. Missing input stops movement and new requests while accepted actions continue to their end.

`NetworkPlayerCombat` reuses `MeleeAttack` and its `MeleeAttackDefinition`. Stats pays stamina exactly once when a swing starts. The server publishes sequence, start tick, committed facing and busy state. During a swing, movement uses the definition's multiplier and facing remains committed. After all player movement ticks finish, server physics performs the existing overlap/obstruction tests. The existing per-swing `HashSet<IDamageable>` prevents duplicate damage from multiple colliders and repeated active-window ticks. Admission, living source/target, same-runner and hostile-team checks still guard `DamageReceiver`; Health/death remain Milestone 2 server state.

`NetworkPlayerDodge` reuses `DodgeDefinition` and `CharacterStats`, without attaching the offline controller or creating a second resource system. Accepted state includes sequence, start tick, direction, facing and busy flag. Distance/duration/recovery/cost and the half-open invulnerability window come from the server's definition. The `IInvulnerabilityProvider` implementation only grants protection on a living server-authoritative Stats instance. It never trusts a client's visual roll or flag. Death/disable removes protection and stops dodge motion. This arena motor is grounded and planar; it does not implement airborne dodges, slopes or a general character controller.

Walking and dodge offsets use a server capsule sweep and existing arena bounds. The capsule dimensions match the saved test prefab. Visual body rotation and the practice blade have no enabled colliders; a separate root capsule owns collision and hit reception. Roll direction is committed and cannot be steered during the action.

## Local targeting and presentation

`NetworkLocalTargeting` enables the existing `TargetLock` only on the owning live avatar. Target selection, switching, camera reference and UI state stay local. The input sends only normalized facing; the server's actual overlap and visibility checks determine hits. Death, target invalidation/range loss and despawn release the local target. Idle locked dodge moves away from the target; movement input supplies directional dodge. Offline targeting keeps its existing coordinator.

`NetworkActionPresentation` reads server start ticks and shared definition durations. The host uses local render time; non-predicted clients use Fusion remote render time, matching their transform interpolation. A simple practice-blade sweep and body roll show accepted actions on owners and observers. Old snapshots/late joins do not replay already-finished animations because progress derives from absolute time rather than arrival of an RPC. Death hides action visuals. Presentation never calls gameplay attack/dodge/damage methods.

Controls: WASD movement; Space/left mouse attack; Left Shift dodge; middle mouse toggle lock; Q/E switch target. Held action buttons repeat subject to server validation.

## Lag compensation boundaries

This milestone intentionally uses current server geometry and server tick windows, with no client hit authority or physics rewind. High RTT can make a swing that looked in range locally miss on the server, and a locally requested dodge can arrive after a server hit. Visual playback is delayed until accepted state arrives. Movement remains server-simulated/interpolated, without client prediction.

Future work should evaluate bounded, validated historical hitboxes at the attack's input tick; rewind relevant moving obstructions along with targets; decide how historical invulnerability/death/team transitions interact with a rewound hit; and cap rewind age to resist stale-input abuse. Never rewind stamina spending, cooldowns or apply the same hit twice. Owner animation/movement prediction needs reconciliation for rejected actions and a consistent render timeline. Moving-platform/airborne collision and higher player counts need a broader motor/physics design. At present simultaneous lethal hits resolve in server callback order, not as guaranteed trades. Fixed-tick quantization affects window boundaries by up to one simulation tick.

## Validation

Status: **automated acceptance passed on 2026-09-10**. Implementation from the previous session was reviewed and validated against a fresh build. No further gameplay changes were required.

The previous 2026-09-09 build eventually completed. On 2026-09-10 Unity MCP refreshed the current sources, inspected the current scene, verified the saved network prefab's components and assigned action/presentation references, and built `Builds/MultiplayerActions/DragonlandActions.exe` again. The fresh BuildReport recorded `Succeeded errors=0 warnings=496`. Warnings include package/shader warnings, deprecated Fusion callbacks and Unity development-build directives, and an absent optional Pipeline runtime configuration. MCP reports a warning-bearing build as a partial command error; the BuildReport and completed executable establish build success.

Results from the fresh build:

- Play Mode melee validation: **32 checks passed**, covering geometry, windup/recovery, stamina, attack spam, repeated active ticks, multiple hurt colliders, range and wall obstruction.
- Saved prefab validation: **passed**, including melee/dodge definitions, Stats configuration, action presentation references, enabled root hit collider and replicated corpse configuration.
- Six actual Host/Client scenarios: **152 network action checks passed** with `VerifyNetworkActions.py`.
- Milestone 2 combat/resource regression verifier against those same fresh logs: **116 checks passed** with `VerifyMultiplayerCombat.py Builds/MultiplayerActionValidation`.
- Unity Console after testing: **0 errors, 0 warnings**; editor returned to Edit Mode. Build warnings above remain a separate result.

Two dodge startup attempts encountered Photon connection timeouts before gameplay; their logs were preserved in `dodge-cloud-timeout` and `dodge-host-timeout`. The subsequent isolated attempt completed and passed, including before/inside/after server invulnerability checks, replicated roll presentation and exhaustion-induced gaps. Successful scenario logs are under `Builds/MultiplayerActionValidation/<scenario>/`.

Nonfinite/oversized intent sanitization was reviewed in source. It is not counted as a runtime test: MCP rejected a proposed private-method reflection check, which was removed. Manual visual review of the blade/roll and local lock-on, and testing under deliberately increased latency, remain outstanding; headless checks validate presentation state rather than rendered appearance.

Prepared tests:

- `NetworkMeleeValidation.cs`: windup/recovery, attack spam/cost, multiple hurt colliders and active ticks, range, wall obstruction and exhausted stamina.
- `NetworkActionsPrefabValidation.cs`: saved components and required action/presentation references.
- `RunMultiplayerCombat.ps1`: Host attacks Client, Client attacks Host, friendly attacks, graceful/abrupt combat disconnect, plus owning-client dodge spam while simultaneously requesting attack.
- `VerifyNetworkActions.py`: shared accepted start ticks, cadence, mutually exclusive state, at most one damage per swing/victim, fixed server damage, remote presentation state, server before/inside/after invulnerability checks and exhaustion-induced gaps.
- Milestone 2 Stats and combat verifiers remain regression checks.

The dodge probe's synthetic one-point damage/heal checks run only in development automation on the server with `-hodrCombat dodge`; clients cannot request test damage from the server. Headless presentation-state checks do not replace a human visual/latency review.

## Files

New gameplay components (and Unity-generated metadata): `Assets/Game/Player/NetworkPlayerDodge.cs`, `Assets/Game/Player/NetworkLocalTargeting.cs`, `Assets/Game/UI/NetworkActionPresentation.cs`.

Modified gameplay: `NetworkPlayerInput.cs`, `NetworkTestPlayer.cs`, `NetworkPlayerCombat.cs`, `MultiplayerBootstrap.cs`, `MultiplayerCombatProbe.cs`, `MultiplayerTestPanel.cs`; `MeleeAttack.cs` exposes its existing definition for presentation, and `TargetLock.cs` accepts a local camera binding. The saved `NetworkTestPlayer.prefab` gains action/targeting/presentation components, collider separation and a practice blade. Stats, damage policy, inventory and AI architecture are unchanged.

New tests: `Tests/EditorCommands/SetupNetworkActions.cs`, `Tests/EditorCommands/NetworkMeleeValidation.cs`, `Tests/Standalone/VerifyNetworkActions.py`. Completion review adds `Tests/EditorCommands/NetworkActionsPrefabValidation.cs` and updates `Tests/Standalone/RunMultiplayerCombat.ps1` to use the Milestone 3 executable/log directory by default, preventing accidental testing of the old Milestone 2 build. This report was updated with the completed validation. Ignored MCP helper/build/test artifacts live under `Builds/`.

No assets deleted; no commit. Pre-existing unrelated working-tree changes are preserved. `SampleScene` was clean at the start of the completion review and was not edited.
