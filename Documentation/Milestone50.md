# Milestone 50 - Pre-match multiplayer lobby

## Player flow

Open DragonlandBattlefield. Create lobby hosts a Photon Fusion session; Join lobby joins the exact shared lobby name. Joining never creates a missing room. The lobby is private by name and has six participant slots, balanced automatically across Team A and Team B (three each). The list shows human connections, bots, open slots, avatar preparation and replicated Ready / Not Ready states. This milestone uses connection labels rather than account identities.

Every human must explicitly ready up. Only the authoritative host/server can start, and only when all participants have avatars, both teams are represented, no preparation error exists, and the participant minimum is met. The production default is six humans. Development builds and Editor expose a smaller-match option (minimum two participants across both teams) and fill/remove controls using the existing developer bot service. One human plus five bots supports a full development 3v3. Bots occupy real team slots and are automatically ready. Remove bots to free slots for human joins.

Changing the human roster, spawned bot count or short-match requirement clears all human readiness. A departing player releases their avatar and team slot. Room admission closes during countdown, play, results and cleanup, and reopens in Lobby. Disconnect during countdown cancels the start if the participant requirements cease to hold. During play, remaining participants continue; an empty match follows the existing abandonment return path.

King defeat uses the existing Finished -> ReturningToLobby -> Lobby lifecycle. The room and connected players remain; round objects and avatars reset through NetworkRoundReset. Human readiness is cleared, preventing an automatic rematch. Leave controls are available in both lobby and match. Host departure ends the session and returns remaining clients to the connection screen after Fusion detects loss; host migration is deliberately unsupported.

## Architecture

Reuses MultiplayerBootstrap, NetworkTeamRoster, NetworkMatch, NetworkRoundReset and DeveloperBotService. NetworkLobbySlot now replicates Ready separately from HasAvatar. RPC_SetReady authenticates its sender using Fusion RpcInfo.Source and accepts changes only for active humans in Lobby. Clients cannot supply another player's identity or invoke server start/configuration methods successfully. Host-mode RPCs use SourceIsHostPlayer so host readiness has the correct player identity.

MultiplayerLobbyPanel owns presentation. StartSession remains the session-entry boundary for a future matchmaking service; authoritative admission, readiness and match lifecycle remain independent of how a room was selected. Private exact-name rooms are the current discovery mechanism. No ranked queues, ratings, persistence, reconnect reservations or host election were introduced. The existing developer network panels are suppressed behind the actual lobby panel while in Lobby.

The previous automatic-start behavior is intentionally replaced. Older opt-in milestone probes that assume automatic starts need explicit ready/start orchestration before being reused; ordinary matches require the lobby flow.

## Verification

LobbyValidation.Run() in Editor Play Mode passed all 12 assertions twice: balanced six-player roster; no automatic start; unready start denial; seventh participant denial; ready host start; countdown to Playing; readiness mutations rejected during play; return readiness reset; six-participant default; development short-match configuration; readiness reset on configuration change; two-participant start. This local fixture calls the existing return path directly to isolate cleanup.

A Windows development build succeeded with zero errors. It retains existing package/API/shader warnings. Real host/client acceptance uses the inert-by-default LobbyNetworkProbe component and -hodrLobbyCheck, together with -hodrMode host/client and a shared -hodrRoom name. Both peers logged COMPLETE in Builds/Milestone50/host.log and client.log. They verified replicated teams/readiness, unready start denial, host start, client start denial, Playing, a normal authoritative King defeat, return to the same room, retained connections and cleared readiness. This is a network acceptance fixture, not a gameplay balance test.

Disconnect/rejoin and final visual inspection results are recorded below after completion. Builds/Milestone50/lobby-preview.png is the lobby layout capture. Run manual internet playtesting with six human clients for latency and usability across target resolutions.

## Files for this milestone

New: Assets/Game/UI/MultiplayerLobbyPanel.cs, Assets/Game/Core/LobbyValidation.cs, Assets/Game/Core/LobbyNetworkProbe.cs and Unity-generated metadata; Documentation/Milestone50.md.

Modified: Assets/Game/Core/MultiplayerBootstrap.cs, Assets/Game/Core/NetworkMatch.cs, Assets/Game/UI/MultiplayerTestPanel.cs, Assets/Game/UI/NetworkFortressPresentation.cs, Assets/Game/UI/GameplayHudPresenter.cs, Assets/Scenes/DragonlandBattlefield.unity.

No deletions or commits. Existing milestone 45-49 working-tree changes are preserved. No new networking stack, match manager or bot AI system.

Final acceptance: both final-host.log and final-client.log contain [LobbyNetwork] COMPLETE from the final build. Killing the first client produced Left owner=[Player:2] count=1/6 on the host; a new client then joined the same room as Player 3. Killing the final host produced Connection lost: Timeout, Stopped: Ok and the Create or join a lobby to reconnect status on the final client. Bootstrap now schedules runner release outside Fusion callbacks after connection loss; logging the loss alone had left the old runner active, and this was corrected before final acceptance. All acceptance processes were stopped.

The final lobby panel was visually inspected at 1085x401, including its scrollable controls and opaque background. Battlefield references and installed lobby/probe components were checked through Unity MCP; the scene and assets are saved in Edit Mode. Final Unity Console error check is clear. Build result: Builds/Milestone50/build-result.txt. The full working-tree diff check reports existing Unity-generated blank-name whitespace; the changed M50 C# files pass their scoped diff check.
