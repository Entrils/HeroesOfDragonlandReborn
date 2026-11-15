# Milestone 51 - Multiplayer resilience

## Connection behavior

Networking remains Photon Fusion Host/Server/Client. MultiplayerBootstrap exposes typed NetworkConnectionState and player-readable NetworkMessage independently of diagnostic Status logs. States cover Offline, Connecting, Connected, Interrupted, Reconnecting, Disconnecting, FailedJoin, SessionFull, SessionClosed, SessionNotFound, JoinRejected, ConnectionLost and ReconnectExpired. SDK shutdown reasons map to actionable explanations. The lobby panel also appears over an interrupted match and exposes cancellation and manual reconnect controls. Duplicate join requests produce a notice and preserve the current runner/connection attempt.

A client that receives no new confirmed server tick for three seconds displays Interrupted and blocks local gameplay input without pausing simulation. Tick progress automatically restores Connected. Fusion's own transport timeout remains responsible for declaring disconnect. Missing server input remains neutral, so a stalled client cannot continue moving/attacking on held input.

After an involuntary client transport loss, the old runner is released outside Fusion callbacks before retry. Three bounded attempts use 2/4/8-second backoff and the same random 32-byte connection credential. Cancellation interrupts pending StartGame and invalidates delayed recovery attempts. Startup/shutdown callbacks check their runner identity; failures keep their final explicit error after cleanup. A failed initial join never creates a room or enters an automatic retry loop.

A lost host/server cannot be replaced in this milestone. Clients attempt only the same room as Client and ultimately show ReconnectExpired when that server cannot be reached. No client creates a replacement room, invents a winner or restores server state from local data. The UI can create/join another lobby after recovery fails.

## Same-round reconnect

ReconnectReservations owns server-only, bounded 90-second reservations. The credential is generated securely in memory per new client session, passed through Fusion's ConnectionToken and never logged or stored on disk. It is a bearer credential for this room and process, not an account-authentication system. A future authenticated matchmaking/session service should issue or bind reconnect credentials to its own trusted player identity.

While Playing, a departed client with a valid credential retains its existing server-owned avatar and team slot. Input authority and private inventory/resource replication to the old connection are removed. Combat, dodge and ability actions are cancelled. The suspended avatar has no admission to gameplay mutations, rewards, chest claims or respawn callbacks, and its renderers/colliders are hidden/disabled. Health regeneration and damage are blocked while suspended. Existing inventory, equipment, progression and death/respawn state remain on that same server object.

A matching credential can reclaim the reservation while the original server and round remain active. The roster transfers to the new PlayerRef, the same avatar receives input authority, private snapshots are enabled only for the new owner and attack-press baselines reset. Dead players retain their original death/respawn timer; reconnect does not recreate a living full-health avatar. If the existing timer elapsed while disconnected, normal respawn can proceed after readmission.

Transport admission stays open so reconnects can reach the server; server admission still rejects new participants outside Lobby, duplicate live credentials and roster overflow. New avatars/spawn retries also recheck live connections, capacity and phase. Connected and pending-spawn identities share duplicate admission checks.

Voluntary Leave asks the server to forfeit reconnect rights and waits for acknowledgement (up to two seconds) before shutdown. If the acknowledgement cannot travel, the grace timeout is the fallback. Lobby departures and departures at match end despawn immediately. Expired reservations release the actor and roster entry. Every round reset invalidates outstanding reservations and despawns their avatars before resetting connected players; stale state cannot reappear in a later round. An empty dedicated-server match waits for reservation expiry before the existing abandonment cleanup.

Suspended reconnect protection is a vertical-slice policy, not ranked anti-abuse design. It can remove a disconnected body from combat after transport loss is declared. Persistent accounts, process-restart recovery, cross-server recovery, host migration and ranked disconnect penalties are outside this milestone.

## Chest, equipment and death safety

NetworkParticipant now verifies both current PlayerObject ownership and membership in Fusion ActivePlayers, and rejects suspended objects. This closes the window between transport departure and the leave callback. Inventory/equipment/skills/combat/reward paths reuse that common admission check. Inventory and resources explicitly stop private replication to the departed owner and enable it for the replacement owner.

Existing network chest deposits are atomic and require capacity for the whole roll. No intermediate chest ownership/partial reward transaction crosses frames. A claim accepted before departure remains committed exactly once; a request from a departed sender is rejected. Reconnect retains the committed inventory and cannot reopen the looted chest. Uncommitted claims leave the original server roll available; client intent never supplies a recipient or loot contents.

Death uses the same avatar and timer across reservation/reconnect. At Finished/ReturningToLobby, existing round cleanup wins over reconnect; disconnect cannot resurrect a player into an ended round or produce a second result.

## Verification

The existing twelve LobbyValidation Play Mode assertions pass after the networking changes. Builds use the existing Windows development player builder. ResilienceNetworkProbe is inert without -hodrResilienceControl and has no release test logic. Builds/m51_stress.py launches only its own local hidden headless players, sends opt-in file commands, suspends/resumes only its tracked test processes and stops every instance in finally. It tests pending/connected duplicate joins, temporary server pause, death/equipment/private inventory reconnect, chest commit with departure, repeated reconnects, a real client process stall through timeout, disconnect at Finished, host loss, six human connections plus a seventh join, and missing-room failure. Final results and artifact paths are recorded below after acceptance.

## Acceptance completed — 2026-11-15

Resumed the interrupted milestone by inspecting the implementation and previous failed runs, rebuilding the Windows development player, and repeating the complete acceptance driver. No additional gameplay changes were needed after the existing fixes.

`Builds/Milestone51/Stress-8dbf9502/result.txt` records `PASS all resilience acceptance scenarios`. Its per-instance logs cover:

- Pending and connected duplicate requests, ready/start flow, and a five-second host process pause with automatic recovery on the existing connection.
- Disconnect during death, preserving the same avatar, team, death sequence, equipment, progression and the replacement owner's private inventory snapshot.
- Atomic chest commit before disconnect, rejection of duplicate claims and departed senders, and no duplicate loot after reconnect.
- Four additional disconnect/reconnect cycles without reservation leaks, followed by a real 38-second client process suspension and successful recovery after transport timeout.
- Disconnect after match result, exactly one result, return to Lobby, and removal of the previous avatar and reservations.
- Host process termination, bounded recovery attempts and a stable terminal ReconnectExpired state.
- Six simultaneous human connections and an explicit SessionFull state for the seventh; a missing room produces SessionNotFound; cancellation of a pending join returns Offline.
- Dedicated server process termination and terminal ReconnectExpired on its client.

All instances created by the driver were stopped by its final cleanup. No resilience assertion failures, NullReferenceException, InvalidOperationException or C# compiler errors were found in this run's player logs. Expected Fusion diagnostics for intentionally failed connections are not gameplay assertion failures.

Unity MCP verified the active DragonlandBattlefield scene's multiplayer root, player/chest prefab references, lobby panel and gameplay input gate, then saved assets and the scene. The development build succeeded with SDK/shader warnings. A fresh Editor Play Mode run completed `[LobbyValidation] PASS ALL 12` and released its session; the Editor was returned to Edit Mode. The final Console check found no errors. Reservation expiry after the full 90-second grace period and visual presentation of error panels remain manual checks; the automated run verifies reservation cleanup at round boundaries.

### Milestone file inventory

New: Core/NetworkConnectionState.cs, Core/ReconnectReservations.cs, Core/ResilienceNetworkProbe.cs, their Unity metadata, Documentation/Milestone51.md. Local stress driver and logs live under Builds/.

Modified: Core/MultiplayerBootstrap.cs, Core/NetworkTeamRoster.cs, Core/NetworkParticipant.cs, Core/NetworkCharacterStats.cs, Core/NetworkMatch.cs, Player/NetworkTestPlayer.cs, Player/NetworkPlayerDodge.cs, Combat/AttackPressSequence.cs, Combat/NetworkPlayerCombat.cs, UI/MultiplayerLobbyPanel.cs and Scenes/DragonlandBattlefield.unity.

No deletions or automatic commits. Existing milestone work is preserved. Architecture adds a bounded reservation service and typed error mapping; existing match, inventory, chest, equipment and respawn systems remain authoritative.
