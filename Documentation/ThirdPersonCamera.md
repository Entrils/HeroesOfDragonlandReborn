# Battlefield third-person camera

DragonlandBattlefield uses the existing ThirdPersonCamera orbit, lock-on framing and wall collision. BattlefieldCamera only binds it to the local human avatar and manages local input/cursor state. The camera is perspective (60-degree FOV), follows at 5m, and starts behind the avatar. The center-rooted network capsule uses a 0.6m pivot offset. There is no automatic overhead gameplay mode.

WASD is transformed by camera yaw before sending human movement intent to the server. Bot inputs remain in world space. MMB locks a target and Q/E switch targets using the existing TargetLock. Mouse rotates the camera. At match start the cursor is captured; Escape releases it for developer buttons and Enter resumes control. Inventory/skill UI can independently block input and release the cursor. Network simulation continues while menus are open.

The local player's overhead debug label is hidden in this camera mode because the HUD already displays player state. Other actors retain their existing labels. The camera binds again when a round recreates the local avatar; bots and remote players never receive camera ownership. The lobby without a local avatar uses the saved side-view perspective position.

Changed: World/BattlefieldCamera.cs, Player/ThirdPersonCamera.cs, Core/MultiplayerBootstrap.cs, UI/MultiplayerTestPanel.cs, UI/NetworkPlayerPresentation.cs, World/Editor/BattlefieldSceneBuilder.cs and Assets/Scenes/DragonlandBattlefield.unity. Added World/Editor/BattlefieldCameraValidation.cs and Tests/EditorCommands/BattlefieldCameraValidation.cs. Paths without an Assets prefix are under Assets/Game. No assets deleted.

The Play Mode validation checks perspective, local-only binding, initial direction, camera-relative forward movement, orbit, wall collision and rebinding after avatar recreation in a live local match. A rendered frame is saved to Builds/third-person-restored.png for visual inspection. Free-look feel and lock-on framing during combat still benefit from a manual mouse playtest.
