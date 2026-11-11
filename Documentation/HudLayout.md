# Third-person HUD layout

Player resources are anchored top left, match/objective and King bars top right, lock-on health bottom center, and respawn/result notices around the middle. Controls remain bottom left, with inventory/skills buttons bottom right. UI Toolkit uses 1280x720 reference fit scaling (Expand), keeping the reference layout inside 16:9, 4:3 and ultrawide viewports.

Developer connection and bot controls share that scaling and occupy separate left/right areas below the HUD. They are hidden while the third-person cursor is captured. Escape releases the cursor and shows them; Enter resumes gameplay. The existing inventory and skill windows retain their own input gates.

BattlefieldLabelVisibility hides overhead map annotations, camp debug labels and actor debug text in the third-person scene. Close attack windup warnings remain visible inside viewport margins. Boss ground telegraphs and the target-lock HUD are preserved. The underlying TextMesh objects and assets are retained.

Changed: Assets/Game/UI/GameplayHudView.cs, Assets/Game/UI/MultiplayerTestPanel.cs, Assets/Game/AI/DeveloperBotService.cs, Assets/Game/Data/MultiplayerFortress/GameplayHudPanel.asset and Assets/Scenes/DragonlandBattlefield.unity.

Added: Assets/Game/UI/DeveloperUiLayout.cs, Assets/Game/UI/BattlefieldLabelVisibility.cs, Assets/Game/World/Editor/HudLayoutValidation.cs and Tests/EditorCommands/HudLayoutValidation.cs, with Unity metadata where applicable. No deleted assets or gameplay changes.

HudLayoutValidation tests visible panel/button bounds and main-panel non-overlap at 960x540, 1280x720, 1920x1080, 1024x768 and 2560x1080 using render-texture panel targets. It restores the original panel targets afterward. Builds/hud-layout-WIDTHxHEIGHT.png contains the UI render for each size; Builds/hud-in-game.png captures the actual Game View including the world and UI for visual review.
