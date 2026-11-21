# M66 Online matchmaking

## Player flow

| Entry point | Behavior |
| --- | --- |
| Quick Play | Find an open, compatible Battlefield 3v3 session in the current Photon region. If the queue is empty, Photon creates a public host room. |
| Private Match | Create an unlisted room and share its ten-character code and region. Enter a code to join. Codes are case-insensitive. |
| Custom Lobby | Create or join by exact lobby name. The in-room view shows both teams, players, open slots, and ready state. Each player controls their own readiness. The host starts when the roster is ready and both teams meet the match rules. |

An active Quick Play match is withdrawn from random discovery. The server's existing admission gate still guards against stale joins and only grants an active-match reconnect to a reserved connection. An interrupted pre-match lobby is released and the menu offers another search or code. Full, missing, closed, refused and failed joins have separate player-facing states.

## Ownership boundaries

| Boundary | Owner |
| --- | --- |
| Party | `OnlineParty` represents the current solo entrant. A future party service can submit its own roster before making a queue request. |
| Matchmaking | `OnlineMatchmaking` defines the queue namespace, map/mode compatibility properties, names, and code rules. |
| Transport | `MultiplayerBootstrap` starts and tears down Fusion runners and owns connection and reconnect state. |
| Lobby | `OnlineLobbyState` owns readiness and clears it when membership changes. `NetworkMatch` exposes the replicated lobby slots. |
| Team assignment | `NetworkTeamRoster` owns balanced, server-assigned team slots. |
| Match session and result | The existing authoritative round lifecycle in `NetworkMatch` owns gameplay and King victory. Its `ResultPublished` event emits the room, round, winning team, and duration as an immutable value. A future result service can consume that contract with persistent identity and ranking rules. |

The Quick Play queue uses Fusion's filtered random join-or-create. Named rooms use exact room lookup. The project currently pins Photon to the EU region, so the room code and lobby name resolve there. See [Photon Fusion matchmaking](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking).

Run `./Tests/Standalone/RunOnlineMatchmaking.ps1 -Executable Builds/M66/Dragonland.exe` for a live Photon smoke test. It isolates Quick Play with a unique development queue name.

The Windows development build passed Quick Play creation and joining, active-match exclusion, lower-case private code entry, host loss in a lobby, missing and invalid codes, and the seventh client being refused from a six-player custom lobby. Logs are in `Builds/OnlineAcceptance-9331c2ab/`. The two-round gameplay and victory regression passed separately in `Builds/LifecycleAcceptance-078b5e59/`.
