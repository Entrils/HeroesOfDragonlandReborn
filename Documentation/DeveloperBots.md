# Developer multiplayer bots

Open `DragonlandBattlefield`, enter Play Mode, and use the **Developer bots** panel at the bottom left. Choose **0–5 bots**, then **Start local match** to run without a server connection, or use the existing Host button for a networked session. **Fill 3v3** requests five bots; with one human this fills both teams to three and satisfies the normal lobby requirement. The scene defaults to zero bots.

The server can change bot count during a session. Real players and bots share the existing six-participant roster and team capacity. If humans already occupy slots, only the remaining capacity is filled. A full roster rejects additional connections; reduce bots before joining another human. Pending spawns retry when a legal fortress spawn point becomes available. Clients cannot create or control bots.

Development builds can use `-hodrMode host -hodrRoom my-test -hodrBots 5`. In the Editor, bot count is also exposed on DeveloperBotService. Bot controls, local single-player launch and AI are compiled for the Editor / Development Build only; release builds retain an inert scene component.

Bots reuse BattlefieldPlayer, the normal server motor, melee attacks, stamina costs, dodge, damage policies, team identity, progression, inventory and respawn. They have no input-owning connection. Their avatars are admitted only through the server bot registry; setting an IsBot marker alone does not grant admission. Normal owner-only RPCs still reject connectionless requests. The server chest path validates admission, reach, line of sight, life state and inventory capacity through the existing LootChest logic. OpenedByBot records the winning avatar ID separately from a human connection.

Simple priorities:

1. Attack a nearby opposing player.
2. Approach and open a nearby unopened chest.
3. Alternate between farming neutral enemies and attacking the opposing fortress.
4. Attack the opposing guard, then King; roam if no objective exists.

Farm/siege windows are offset by spawn lane to spread activity. Bots dodge occasionally near combat. NavMesh paths produce movement intentions; the existing player motor handles actual movement, walls, collisions and protected spawn areas. There is no separate bot movement or damage implementation. Bots do not equip loot or spend progression points automatically, coordinate tactics, predict player movement, or optimize fights. They are gameplay test actors.

Bots count in lobby slots, receive nearby camp XP, retain inventory through normal deaths, respawn at their fortress, and are recreated with fresh state on round reset. Disconnect removes their avatars and reservations. A requested count survives reconnection as a developer preference.

Validation helpers live in `Assets/Game/World/Editor/DeveloperBotValidation.cs` and are invoked through Unity MCP. The live gameplay scenarios test balanced 3v3 admission, connectionless RPC rejection, camp damage, chest collection, PvP, guard and King damage, and respawn. Scenarios reposition actors and remove a guard to isolate each behavior; all tested damage and chest claims still use normal bot inputs and gameplay paths. Lifecycle checks exercise every count from 0 to 5, a King-triggered round finish/reset, fresh bot avatars, disconnect cleanup and reconnect. Separate natural match runs check pathing and activity. Bots ignore players inside opposing protected spawn areas, where they cannot legally follow. Remote-client presentation and long-session strategy/balance require manual playtesting.

Changed files for this feature:

- Added `AI/DeveloperBotService.cs`, `AI/DeveloperBotBrain.cs`, `AI/DeveloperBotNavigation.cs`, `Core/NetworkParticipant.cs`, and their Unity metadata under `Assets/Game`.
- Added `World/Editor/DeveloperBotValidation.cs`, `Tests/EditorCommands/DeveloperBotValidation.cs`, and this document.
- Updated `Core/MultiplayerBootstrap.cs`, `Core/NetworkMatch.cs`, `Core/NetworkPlayerResources.cs`, `Player/NetworkTestPlayer.cs`, `Player/NetworkPlayerRespawn.cs`, `Combat/NetworkPlayerCombat.cs`, `Inventory/NetworkInventory.cs`, `Progression/NetworkSessionProgression.cs`, and `Skills/NetworkSessionSkills.cs` for admitted server-owned participants.
- Updated `World/Camps/NetworkCamp.cs`, `World/NetworkLootChest.cs`, `UI/NetworkPlayerPresentation.cs`, and `UI/NetworkFortressPresentation.cs` for bot rewards, claims and labels.
- Saved `Assets/Scenes/DragonlandBattlefield.unity` with developer controls and zero bots by default.
- No deleted assets. No separate combat, movement, camp or respawn implementation.
