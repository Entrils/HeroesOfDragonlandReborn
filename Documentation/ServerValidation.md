# Milestone 87 — Server validation and anti-cheat hardening

The authority boundary is the admitted player avatar in the current Fusion runner. Private gameplay RPCs must belong to that avatar. Shared ready, leave and chest RPCs resolve their authenticated Fusion sender to the admitted avatar. Only server state applies outcomes.

| Surface | Authoritative validation |
| --- | --- |
| Movement and teleport | Inputs contain normalized movement and facing intent. Nonfinite or out-of-bounds values suppress the sample. The server integrates its own timestep, sweeps the capsule, and applies arena and spawn bounds. There is no client position or teleport request. |
| Dodge and strike rate | Server-side input counters, action windows, costs, and cooldowns gate actions. Busy and denied edges are consumed. The client cannot choose roll distance, invulnerability, damage, or attack speed. |
| Combat targets and King damage | Server hit geometry, obstruction, team rules, per-swing deduplication, and defense decide each hit. Player, enemy, fortress gate, and King melee policies require the live resolving hit window. Projectile and magic policies retain their separate active-attack checks. |
| Inventory and equipment | Authenticated sender, request budget, slot bounds, current snapshot revision, ownership, catalog, slot compatibility, and requirements are checked on the server. Consumables also check cooldown and need. Successful item use changes inventory revision, so an old request cannot consume again. |
| XP, attributes, and skills | XP has no client grant RPC. Server reward IDs and session identity reject repeats. Attribute allocation uses the existing authority-only API with an allowed stat and available points. There is no remote attribute allocation endpoint. Skill requests validate node, session, expected rank for purchases, prerequisites, points or mana, and cooldown. |
| Chest and trap | The server checks chest reach and obstruction before rolling or transferring loot. Trap requests validate inventory, finite coordinates, owner reach, line of sight, floor, clearance, spacing and protected spawn rules. |
| Camp rewards | A single completion transition issues loot and unique XP receipts, using server-recorded contribution and distance. Disconnected, stale or foreign actors are excluded. |
| Match | The server roster assigns teams at admission. Ready and leave use the authenticated sender's avatar. Death, respawn timers, spawn points and protection are server-owned. There is no manual respawn request. |
| Other RPCs | Team pings validate round, type, coordinates, budget and server visibility. Appearance validates catalog encoding and applies display-only state once. Cosmetic ownership lives in the local collection. Client user messages and reliable data callbacks have no gameplay handler. |

The `[ServerValidation]` log records a reason code, Fusion player reference, and tick for selected malformed inputs or sender mismatches. Each player/reason can log once per two seconds, with at most 16 lines in a ten-second window for the runner. Ordinary stale revisions, cooldowns, and resource failures remain gameplay rejections. Logs do not kick, ban, or inspect client software.

## Verification

Run `ServerValidationChecks.Run()` for vector bounds and the diagnostic budget. The local Fusion check **Heroes of Dragonland → Combat → Validate M84 Local Network Impact** also verifies authenticated sender ownership and refusal of damage outside resolution before confirming a real hit. Existing multiplayer fixtures cover the normal lobby, combat, inventory, loot, trap, skill, and respawn paths.

Unity compiled the project and the Windows Development build at `Builds/M87/Dragonland.exe` with zero errors. The local Fusion check passed both rejection and legitimate-hit checks. Standalone host/client probes passed for inventory ownership and equipment, skill prerequisites and costs, respawn, contested loot and duplicate XP denial, traps, and fortress damage and reset. The trap probe ran with four peers.

Changed the Fusion sender boundary, movement and combat intent handlers, the King / gate / enemy hit policies, the inventory and skill RPCs, chest and camp rewards, and the appearance and ping payload checks. Added `ServerActionAudit.cs`, `ServerValidationChecks.cs`, and this audit. Extended the existing local Fusion impact check. No files were deleted.
