# M63 Team pings

In a live Battlefield match, aim the center dot and press **T** for a contextual ping, **Y** for Danger, or **U** for Retreat. Quick ping marks the aimed surface, a visible enemy, a neutral camp, the boss, a fortress, a King, or an unopened chest. Aim at the ground near a camp or fortress landmark to mark that objective. Danger and Retreat mark your footing if the reticle points into the sky. Ping controls are inactive while gameplay input is blocked.

Allies see a label anchored to the world position and a symbol on the tactical map. The label shows the match-assigned player name, such as `Player 3`, and the minimap symbol carries the same player number. Pings are snapshots. An enemy ping stays where the enemy was seen and does not follow the enemy.

The player prefab exposes lifetime, cooldown, and active-ping cap. The defaults are 6 seconds, 1 second between requests, and 3 active pings per player. The server checks the owning connection, admission, living state, current round, valid map bounds, 42-metre reach, and line of sight. Target IDs must resolve to an eligible object. Untargeted pings must resolve to a real visible surface. The server sends one targeted event per admitted ally and does not replicate persistent ping objects.

## Verification

Run `./Tests/Standalone/RunPlaytestFeature.ps1 -Feature TeamPing -Executable Builds/M63/Dragonland.exe` for a host, allied client, and opposing client. The fixture checks delivery in both allied directions, sender identity, opponent isolation, cooldown, active cap, malformed coordinates, and expiration.

The Windows development build passed the three-peer fixture. See `Builds/Playtest-TeamPing-76427925e62c/`.

In a rendered match, aim at each supported target, check the world and minimap labels against the aim point, and confirm that opening a menu blocks the hotkeys. Repeat beside cover to check that an occluded target is rejected.
