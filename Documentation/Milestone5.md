# Milestone 5: dodge

Left Shift or gamepad B/Circle starts a grounded dodge. Input is edge-triggered:
holding does not repeat, and presses during a dodge/recovery are discarded.
Movement uses the camera-relative input direction captured at the start. With no
input, dodge forward when unlocked or away from the target when locked. Target
facing remains independent of travel direction. Attacks and dodges cannot overlap.

Tune Assets/Game/Data/DefaultDodge.asset: 25 stamina, 4 metres, 0.4 seconds,
0.3 seconds recovery. Invulnerability start/end are normalized roll times
(defaults 0.15–0.75, equivalent to 0.06–0.30 seconds). Start is inclusive,
end exclusive. Recovery blocks movement, jumping, attacks and another dodge.
Collision may shorten travel; gravity remains active. No root motion is used.

PlayerDodge owns action state and exposes Direction, Progress, IsRolling, IsBusy,
DodgeStarted and DodgeFinished for animation integration. DodgeFinished includes
recovery completion or cancellation; use IsRolling/Progress for the roll pose.
PlayerMovement remains the only CharacterController motor. IInvulnerabilityProvider
is checked by CharacterStats.ApplyDamage, covering DamageReceiver and direct calls.
Timing uses scaled delta time; the controller ticks dodge once per frame.

## Validation

Unity Play Mode checks passed: one-time stamina debit; 20 repeated requests;
insufficient stamina; attack exclusion; damage before/start/end of the window via
both damage entry points; configured distance; target facing during sideways
travel; recovery rejection/completion; idle fallbacks; disable cancellation;
collision against a thin wall with a long frame; airborne rejection.
Unity Console: zero warnings/errors after validation.

Tests/EditorCommands/DodgeValidation.cs is a Unity MCP RunCommand script, outside
Assets so it is not compiled into the game. Run in Play Mode in SampleScene.
Exit Play Mode after testing (also after any assertion failure) to restore scene
state. It creates temporary floor/wall objects and drives the real player systems.

Manual follow-up: check keyboard/gamepad feel with a held button and rapid taps,
all movement directions while acquiring/switching/losing lock, and slopes/steps
in the level. Visual movement is a placeholder slide until roll animation is added.
