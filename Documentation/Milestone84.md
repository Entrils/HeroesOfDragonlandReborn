# Milestone 84 — Combat Feel Polish

Tune the eight profiles in [CombatFeedbackProfiles.asset](../Assets/Game/Data/Resources/Dragonland/CombatFeedbackProfiles.asset). Weapon profiles cover sword, greatsword, spear, bow and magic. Block, parry and guard break select their own VFX, audio pitch, recoil, camera impulse and rumble. A heavy input adds a small presentation multiplier. The greatsword has the longest stop and trail among the weapons. Bow and magic leave animation playback running.

- Melee anticipation, contact and follow-through sample the chosen animation against the accepted move's existing active window. The trail and swing sound begin in that window. The trail stops emitting during follow-through and clears on interruption. Timing values on weapons and movesets retain their existing gameplay meanings.
- Confirmed damage carries a contact point and direction into the existing state-authority RPC. The render path aligns the cue to the source's render clock with a bounded wait. Bow and spell surface impacts still have their projectile fallback. A block that breaks guard uses the guard-break presentation once.
- Hit stop is a short animation hold. Camera shake is a small, decaying render-only translation, with directional marks for incoming contact and an optional gamepad pulse. Player movement, aiming, simulation clocks, costs, damage and server hit validation use their existing paths.

## Verification

In Play Mode on `DragonlandBattlefield`, run `WeaponMovesetValidation.StartFeedback`, `AdvancedDefenseValidation.Start` and `MagicValidation.ValidateFeedback` for weapon contacts, trail windows, confirmed defense cues and fire-projectile contact. Use **Heroes of Dragonland → Combat → Validate M84 Local Network Impact** to check that a confirmed Fusion hit reaches the existing VFX pool.

In an actual match, compare a sword light strike, a greatsword light and heavy strike, and a spear thrust from the same camera position. Block and parry frontal melee attacks, then allow a blocked impact to exhaust stamina. Check arrow and fire impacts on a target and on a wall. With a gamepad connected, check motor decay and focus-loss reset.
