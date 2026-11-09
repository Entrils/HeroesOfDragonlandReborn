# First gameplay HUD

Implemented and verified 2026-11-06 in `Assets/Scenes/MultiplayerFortressTest.unity`.

The screen-space UI Toolkit HUD uses placeholder panels and colored bars. Bottom left shows the local player's health, stamina, mana, team, level and XP progress. Top right shows match phase, both Kings' public health and objective status. A selected local target adds a health panel and a projected lock marker, hidden when outside the camera view. Respawn countdown and victory/defeat appear at the bottom center. Maximum level, occupied respawn points, disconnected players and unavailable objectives have explicit display states.

## State ownership

- `GameplayHudView` builds and updates visual elements only. It has no gameplay or network dependencies and ignores pointer picking.
- `GameplayHudPresenter` binds only the current runner's registered local player with input authority. It subscribes to `CharacterStats` resource events, `SessionProgression.Changed`, `TargetLock.TargetChanged`, respawn events and match phase/result events. Existing network adapters hydrate those same domain events on clients. No gameplay system references the HUD.
- Initial snapshots populate the HUD immediately on binding. Discovery in LateUpdate handles avatar replacement, joins, despawns and King ID changes; timers/roster and marker position are sampled because those values have no complete change-event API. Subscriptions are removed on rebind, disable and disconnect.
- Remote information is restricted to health for the local lock's selected same-session target, both Kings, and public match state. Other players' stamina, mana, XP and respawn timers are never bound. Existing owner-only resource replication remains unchanged. Dedicated servers show no private player HUD.
- The presenter's cached team avoids reading a despawned avatar during match callbacks at round boundaries.
- `GameplayHudSceneBuilder.Install()` installs the saved document, panel settings, theme and presenter reference through Editor APIs. It is repeatable and disables the former fortress debug overlay. Existing connection controls remain usable; while connected they reduce to disconnect and input hints.

The HUD currently targets the network fortress vertical slice. Earlier offline/test scenes are unchanged. Host-only authoring-error retry remains available through the existing `NetworkMatch.RetryPreparation()` API; the HUD displays preparation errors but does not add gameplay controls.

## Verification

- Editor validation: saved composition, exactly one HUD, bootstrap reference, UI theme, screen scaling, disabled legacy overlay and retained connection controls passed via Unity MCP (`Tests/EditorCommands/GameplayHudValidation.cs`).
- Fresh Windows Development build: `Builds/GameplayHud/DragonlandHud.exe`, `Succeeded errors=0 warnings=27`. Warnings concern existing obsolete/development directives and optional Unity Pipeline configuration, including the same development directive used by the new opt-in probe.
- `Tests/Standalone/RunGameplayHud.ps1`: PASS, host/client two-round lifecycle plus HUD ownership, live health/resources/XP/level, local target lock/unlock, King defeat/result, and replacement-avatar rebind. Logs: `Builds/LifecycleAcceptance-74ed1a11/`.
- `Tests/Standalone/RunGameplayHudRespawn.ps1`: PASS, host/client two respawn cycles plus local countdown, private resource updates and results. Logs: `Builds/RespawnAcceptance-c8b1868e/`.
- `GameplayHudAcceptanceProbe` is inactive unless explicitly launched with `-hodrHud` in Editor/development builds. Its test-only resource spending and target selection exercise the production event path. It never runs during ordinary play.
- Editor Play Mode: connected host/client scene inspected; normal `TargetLock.Toggle()` acquired a target and showed its health and projected marker. Screenshots reviewed: `Builds/hud-preview.png`, `Builds/hud-target-preview.png`.
- Editor disconnect cleared private values, target, countdown and result. A connected dedicated server correctly hid private HUD and target panels.
- Final Unity Console error inspection: zero errors. Assets/scenes saved; Editor returned to the original SampleScene in Edit Mode with no unsaved changes.

## Files

New runtime/UI: `Assets/Game/UI/GameplayHudView.cs`, `GameplayHudPresenter.cs`, `GameplayHudAcceptanceProbe.cs`, `GameplayHudTheme.tss` and Unity metadata.

New authoring/assets: `Assets/Game/World/Editor/GameplayHudSceneBuilder.cs`, `Assets/Game/Data/MultiplayerFortress/GameplayHudPanel.asset` and metadata.

Modified: `Assets/Game/UI/MultiplayerTestPanel.cs`, `Assets/Scenes/MultiplayerFortressTest.unity`.

New validation/documentation: `Tests/EditorCommands/GameplayHudValidation.cs`, `Tests/Standalone/RunGameplayHud.ps1`, `RunGameplayHudRespawn.ps1`, this report. Builds, MCP requests, logs and screenshots are local artifacts under `Builds/`.

No gameplay/network rules changed, no assets deleted, no automatic commit. Final styling and manual usability review are intentionally left for later art/design work.
