# Milestone 85 — Third-Person Camera Polish

The free camera follows a damped pivot with an adjustable shoulder offset. Pointer sensitivity and stick speed still produce immediate local yaw intent. The view eases into that orbit without delaying camera-relative movement input. Teleports and avatar changes reseed the camera at the new position.

The camera checks clearance for its near plane, shoulder shift, and boom. It retracts immediately against a wall and eases back out when clear. If a wall clips the shoulder path, the rig can center through the opening. Moving characters do not retract the boom. Closed fortress gates remain solid to the camera.

Lock-on uses the target's horizontal bearing, elevation, and body bounds for composition. Close crossings hold the orbit bearing. Large targets get more room and can be selected by their visible body when the aim marker is off-screen. Q/E orders eligible switches by bearing from the player, so a compressed boom or shoulder offset cannot reorder them.

The isolated spectator view smooths its current ally and checks only the public scenery already allowed by that view. Cycling to another ally resets its follow history. Combat impulses still use the render-only camera path and can be disabled independently of directional hit feedback or rumble. Movement, bow aim, and pings continue through the existing local input and server validation paths.

## Tuning and validation

The `ThirdPersonCamera` Inspector groups free orbit, world clearance, lock-on composition, and local presentation settings. In Play Mode, enable **Debug Framing** and select the camera with Gizmos enabled. Cyan draws the safe pivot, shoulder, and resolved boom. Yellow shows the desired boom and lock composition point.

With Battlefield open in Play Mode, run **Heroes of Dragonland → Camera → Validate M85 Rig** for the geometry and lock-on fixtures. Run **Validate M85 Local Match** from the same menu for local binding, movement intent, spectator handoff, and avatar replacement.

## Files

- Updated `ThirdPersonCamera.cs`, `TargetLock.cs`, `Targetable.cs`, and `PlayerController.cs` for the local rig, selection, and input basis.
- Updated `BattlefieldCamera.cs`, `BattlefieldSpectator.cs`, and `CombatViewFeedback.cs` for local binding, isolated follow, and the impulse toggle.
- Added `CameraClearance.cs` and `CameraRigValidation.cs`. Updated `BattlefieldCameraValidation.cs` for the damped orbit.
