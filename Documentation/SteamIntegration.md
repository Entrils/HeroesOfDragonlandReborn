# Steam integration foundation

The Windows Development and Release builds run with the local platform provider. It uses the display name from `PlayerProfile`, leaves the authenticated account ID empty, and stores earned achievements in the same profile. Players can still copy a private room code and enter it through Private Match. The foundation has no Steam SDK dependency.

## Provider contract

Implement `IGamePlatform` in a Steam-specific assembly and install it with `GamePlatform.Install(provider)` on the Unity main thread after initialization. Call `GamePlatform.Install(null)` when initialization fails or the adapter shuts down. Keep SDK startup, native API calls, callbacks, and account state in that assembly.

- Return Steam's authenticated account identifier and display name from `Identity`. The menu falls back to the local profile name when the platform name is empty. The account identifier is available to the integration layer and is not used to authorize Photon actions or replace local profile storage.
- Advertise `SupportsInvites` only while the invite overlay is available. `TryShowInvite` receives the current private match's display code from the lobby. Translate that into the platform invite payload.
- Raise `PrivateRoomJoinRequested` on the Unity main thread after the player accepts an invite. The session bridge checks the code and idle connection state, then uses the existing private-room join flow. Photon still controls admission, room capacity, and match phase. The payload should contain only the private room code.
- Map `PlatformPresence` to display strings for menu, lobby, match, or results and the corresponding queue. Presence carries no room codes, player coordinates, ratings, or private session credentials. Clear it when the provider is replaced or the session bridge shuts down.
- Map the achievement IDs below to the platform's configured achievement identifiers. Return true from `TryUnlockAchievement` after the adapter acknowledges the ID. Treat repeat submissions as idempotent. The bridge retries unacknowledged IDs every five seconds while the game session component is active and resubmits saved achievements when a provider is installed.

## Achievement IDs

| Stable ID | Display name | Award condition |
| --- | --- | --- |
| `FIRST_VICTORY` | First Victory | Win a completed match |
| `KILL_ENEMY_PLAYER` | Kill an Enemy Player | Receive kill credit for an enemy player |
| `CLEAR_10_CAMPS` | Clear 10 Camps | Earn ten camp clears across completed matches |
| `DEFEAT_NEUTRAL_BOSS` | Defeat a Neutral Boss | Receive kill credit for a neutral boss |
| `KILL_ENEMY_KING` | Kill the Enemy King | Receive kill credit for the opposing King |
| `WIN_WITHOUT_DYING` | Win Without Dying | Win a completed match with zero deaths |

Achievement evaluation runs when the profile accepts an authoritative match result. Rewards are saved with that result. Duplicate results do not grant progress, and aborted matches produce no award. Kill credit and camp credit use the existing server combat and objective rules.

## Validation

Local profile files are development storage. A future production adapter should decide whether to import local unlocks or require account-verified achievements.

Use the local provider for development and profile persistence checks. Use a test adapter to exercise provider installation, invite callbacks, presence changes, and achievement retries without a Steam session. When an SDK adapter is added, verify those paths end to end with two Steam accounts and a configured test app before enabling distribution.

Run `./Tests/Standalone/RunPlatformAchievements.ps1 -Executable <development-player>` to check all six local unlocks across two player launches. The test uses an isolated profile and submits the same synthetic match receipt twice to verify deduplication.
