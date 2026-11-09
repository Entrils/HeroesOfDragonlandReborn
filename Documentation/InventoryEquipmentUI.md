# Inventory and equipment UI

The UI is installed in `Assets/Scenes/MultiplayerFortressTest.unity` under the multiplayer bootstrap. Open it with **I** or the Inventory button; close it with **I**, **Escape**, or Close.

- The bag shows every inventory slot, item icons (category placeholders when no sprite is assigned), quantities, rarity colors, selection, and hover tooltips.
- Equipment exposes MainHand, OffHand, Head, Chest, Hands, and Legs.
- Item details list every required attribute, its current value, minimum, and exact deficit. Requirements use the equipment system's canonical base plus allocated attributes.
- Equip is disabled when requirements fail. Unequip is disabled when the bag cannot accept the item. Requests are also blocked while dead, busy, synchronizing, outside the playing phase, or waiting for a server response.
- Consumables display their quantity and an explanation with disabled Use. The existing gameplay/network APIs do not implement consumable effects; the UI does not remove items to simulate consumption.

## Code and authority

`InventoryEquipmentView` builds the UI Toolkit window. `InventoryEquipmentPresenter` binds only to the local player's actor, observes inventory/equipment/attribute events, and sends actions through `NetworkEquipment.RequestEquip` and `RequestUnequip`. `InventoryItemText` formats shared details and tooltips. UI code never calls inventory mutation methods. The server validates requests and replicated gameplay state updates the view.

`LocalGameplayInputGate` blocks gameplay input while the window is open without pausing the network simulation. Closing, disabling, or rebinding the presenter releases its input block.

`InventoryEquipmentSceneBuilder.Install` installs and saves the scene composition through the Unity Editor. `InventoryUiAcceptanceProbe` runs only when explicitly requested in the Editor or development builds.

## Verification

On 2026-11-07, `Tests/Standalone/RunInventoryUi.ps1` passed with the existing `Builds/InventoryUI/DragonlandInventoryUI.exe` development build for both host and client. Its managed gameplay assembly was built after the inventory UI source files. The recorded build result is `Succeeded errors=0 warnings=516`.

Acceptance covered local ownership, all bag slots, missing requirement details, disabled equip, unsupported consumption preserving quantity, replicated equip/unequip, full bag rejection, attribute updates enabling equip, item conservation, and releasing the gameplay input block. Logs: `Builds/InventoryUiAcceptance-f14e19df/`.

The Unity MCP relay returned no tools during this verification, including outside the sandbox, so a fresh Editor Console check and Editor validation command could not run. Manually verify visual layout, hover positioning, actual mouse/keyboard interaction, and assigned artwork in Play Mode. No scenes or gameplay code were changed during this verification pass.
