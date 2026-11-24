# Milestone 83 — Main menu and Battlefield HUD

**Status:** Implemented in the project. Unity compilation, Game View checks, and a Windows player build are pending.

[Инструкция для домашней машины с Unity: меню, HUD, волк и медведь](HomeUnitySetupM83.md)

Open the [main menu preview](Milestone83Preview.html) or the [Battlefield HUD preview](Milestone83Preview.html#hud) directly. Each fills the browser viewport. The menu preview includes Play and all four match types. The HUD preview uses sample match values over the existing Ancient Forest art-board image.

The “Main menu / Battlefield HUD” bar belonged to an earlier browser mockup. M83 removed it from the HTML and refreshed both captures. In the game, session and match state control the transition from title to lobby to Battlefield.

## Main menu

The title and brass crown sit directly over the fortress illustration. A compact, bevelled panel groups Play and Training above three labelled symbols for Profile, Settings, and Quit. Skip appears beside Training when relevant. Match results and reconnect appear when available. Play opens Unranked, Ranked, Private, and Custom. The game retains room input across tabs and shows connection status and cancellation while a request is pending. [Menu capture](Milestone83Menu.png)

The title view uses UI Toolkit and a runtime `UIDocument` sharing the scene's existing HUD panel settings. The lobby component binds the buttons to the existing session, training, profile, settings, results, and quit actions. Connected lobby, pause, settings, profile, and detailed results keep their existing screens. The title document is installed only when needed and disposed with its host.

Unranked and Ranked each put the 3v3 search action first. Ranked displays the local rating and provisional status. Private offers room creation and code entry. Custom offers create and join actions for a named lobby. Opening Play moves keyboard focus to the selected mode. Back or Escape returns focus to Play. While cancellation is available, the Cancel connection button handles the pending request.

See [title view](../Assets/Game/UI/DragonlandMenuView.cs), [session bindings](../Assets/Game/UI/MultiplayerLobbyPanel.cs), [stylesheet](../Assets/Art/UI/Resources/Dragonland/FrontEnd.uss), and [title illustration](../Assets/Art/UI/Resources/Dragonland/TitleValley.png).

## Battlefield HUD

[Open the combat preview](Milestone83Preview.html#hud) or [view the browser capture](Milestone83Hud.png).

- The compact minimap at upper left uses team colors, marker shapes, and a compass bearing. Hold **H** during gameplay for the legend, objective, controls, resource totals, and camp pacing.
- At upper center a brass crown sits between the two King health bars. A boss encounter adds its name and health below the round indicator. Locking that boss keeps the world reticle and shows one health bar.
- At lower left the player panel contains health, stamina, mana, and XP in descending bar thickness. Current HP and the equipped quick item remain readable at a glance. The quick-item line appears when the selected slot has an item.
- Interactions, low stamina, King warnings, death recap and results appear when relevant. The lower-right rune and pouch buttons open Skills and Inventory with **K** and **I**.

The expanded HUD requires a local player, game-window focus and a captured gameplay cursor. Releasing **H** collapses it. During a boss fight, shockwave, enrage, reset and current attack messages appear when applicable. The lock reticle still tracks the boss while the encounter bar is visible. Switching to an ordinary enemy restores its target panel.

The HUD presenters keep their current event and data bindings. Existing match, combat, progression, and minimap rules drive the same values.

See [HUD presentation](../Assets/Game/UI/GameplayHudView.cs), [drawn meters, frames, and symbols](../Assets/Game/UI/HudMetalwork.cs), [match visibility](../Assets/Game/UI/GameplayHudPresenter.cs), and [minimap layout](../Assets/Game/UI/Minimap/TacticalMinimapView.cs).

## Verification completed without Unity

- Chrome rendered the final previews at viewport widths of 960 and 1280 pixels. Opening the menu showed the title screen directly. Play exposed all four modes, and Escape returned to the title screen.
- In the HUD preview, holding and releasing **H** showed and hid the details. At both widths the minimap cleared the player panel and round indicator. The saved menu and HUD images are 1280 × 800 browser captures.
- Local checks covered resource and document links, Unity asset metadata, and `git diff --check`. The Unity Editor, Unity MCP and a current player executable were unavailable on this machine.

## Unity acceptance pending

1. Open `DragonlandBattlefield` in Unity 6000.6.0f1 and enter Play Mode. Check Console for C# and USS import errors. Confirm art, mouse focus and keyboard focus on the title screen.
2. Exercise Play → each match type. Try an invalid private code, cancel a connection attempt, then connect to a lobby. Verify Training Grounds and return, Settings and Profile back navigation, reconnect, results, and quit.
3. Start a host/client round. Hold and release **H** with the gameplay cursor captured. Verify resources, quick-item selection, both King bars, the minimap and team pings. Lock the nearby boss, then switch to an ordinary enemy. Verify the reticle and health panel follow the selection. Check interaction feedback, the death recap, and the post-match transition.
4. Run the [HUD layout validator](../Assets/Game/World/Editor/HudLayoutValidation.cs) for 960×540, 1280×720, 1920×1080, 1024×768 and 2560×1080. Inspect Game View over both bright and dark scenery, including simultaneous target lock and interaction feedback.
5. Build the Windows Development player and run `./Tests/Standalone/RunGameplayHudRespawn.ps1 -Executable $player -SpectatorCheck`. Check the full Windows Release player on a machine without Unity.

## Changed files

| Area | Modified files | Change |
| --- | --- | --- |
| Main menu | [MultiplayerLobbyPanel.cs](../Assets/Game/UI/MultiplayerLobbyPanel.cs) | Creates the title document, binds existing actions, and manages visibility and cleanup. |
| Combat HUD | [GameplayHudView.cs](../Assets/Game/UI/GameplayHudView.cs), [GameplayHudPresenter.cs](../Assets/Game/UI/GameplayHudPresenter.cs), [GameplayHudPresenter.Context.cs](../Assets/Game/UI/GameplayHudPresenter.Context.cs) | Compact meters and notices, held-key details, match visibility, target tracking, and shorter level, camp and lock feedback. |
| Boss encounters | [BossHealthPresenter.cs](../Assets/Game/UI/BossHealthPresenter.cs) | Shares boss selection with the lock-on display and shows phase changes and attack warnings. |
| Tactical map | [TacticalMinimapView.cs](../Assets/Game/UI/Minimap/TacticalMinimapView.cs) | Smaller upper-left map, shape and color markers, heading and bearing. |
| Shortcuts | [SessionSkillsPanel.cs](../Assets/Game/UI/SessionSkillsPanel.cs), [InventoryEquipmentView.cs](../Assets/Game/UI/InventoryEquipmentView.cs) | Compact rune and pouch controls with key labels and tooltips. |
| Validation | [GameplayHudAcceptanceProbe.cs](../Assets/Game/UI/GameplayHudAcceptanceProbe.cs), [HudLayoutValidation.cs](../Assets/Game/World/Editor/HudLayoutValidation.cs) | Checks collapsed objective details in batch mode. The layout validator hides the title overlay during HUD capture, checks minimap bounds and overlap with the player panel, then restores the prior display state. |

## New files

- `Assets/Game/UI/` contains `DragonlandMenuView.cs` for title navigation and `HudMetalwork.cs` for bevelled plates, meters, crown and shortcut symbols. Each has a matching `.meta` file.
- `Assets/Art/UI/Resources/Dragonland/` contains `FrontEnd.uss` and `TitleValley.png`, their `.meta` files, and folder metadata for `UI`, `Resources`, and `Dragonland`.
- `Documentation/` contains this report, `Milestone83Preview.html`, `Milestone83Menu.png`, and `Milestone83Hud.png`.

M83 required no file deletions or serialized scene edits.

The title illustration was created with the built-in image generation tool. Prompt used:

> Create a single 16:9 full-bleed title-screen background illustration for the original medieval fantasy game Heroes of Dragonland Reborn. Cinematic painterly realism, a slate fortress perched along the RIGHT side of a wild pine valley, paired towers and weathered red pennants, autumn trees and muted gold sunset breaking through fog, dramatic deep teal limestone and warm atmospheric light, richly detailed near the castle with broad photographic depth. LEFT third much darker and visually quiet, layered mist and charcoal silhouettes to support an overlaid game logo and menu. No text, emblems, typography, interfaces, borders, or watermarks.
