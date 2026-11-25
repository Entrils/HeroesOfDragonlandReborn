# Milestone 88 — Advanced match bots

Development bots use the ordinary server player motor and combat input. They acquire items from normal loot, respect equipment requirements, allocate earned progression, learn legal skill nodes, and activate abilities and consume potions through the same authority and resource checks as players.

## Decisions

- Low-level or lightly equipped bots scout nearby public camp sites and farm visible neutral enemies. A close, visible chest can take priority over farming. A chest near a visible opponent becomes a contest.
- Bots fight visible opponents, prioritize threats near an ally, and defend their own fortress when they see an enemy nearby. Low health or bad local odds can trigger a retreat toward friendly cover.
- A healthy bot may contest a visible boss with nearby visible support. A solo decision also requires enough level and equipment. A developed bot or one that has spent enough time in the round pushes the opposing fortress, prioritizes nearby guards and closed gates on the approach, and attacks a visible King.
- Combat choices include light or heavy swings, bow aim when equipped, reacting to visible windup cues with block, dodge, or an occasional parry, and situational skills and consumables. Reaction delay and ordinary stamina, cooldowns, attack windows, inventory revisions, and movement sweeps still apply.

## What bots can observe

Tactical actors must be in range and visible through scene geometry. The bot may navigate toward the last visible location for a short time. It does not update that location while the target is occluded. Public camp sites and fortress landmarks can be used as waypoints without granting live knowledge of activity behind walls. Ally and enemy presence is local to the bot. Equipment quality, level, and detailed resource state are sampled from the bot's own avatar.

## Difficulty and setup

The development bot panel on the host offers Recruit, Standard, and Veteran. The `DeveloperBotService` Inspector exposes those profiles for sight range and arc, decision interval, reaction delay, last-seen memory, retreat threshold, attack interval, action probabilities, and solo-boss level. Development players can also start with `-hodrBotDifficulty Recruit`, `Standard`, or `Veteran`. Profiles influence decisions. Every bot uses the same gameplay costs, damage, and collision rules as its equipped player avatar.

Open Battlefield in Play Mode and select **Heroes of Dragonland → Bots → Validate Advanced Bots**. The fixture opens a local Fusion lobby, confirms unscripted 3v3 opening travel, then isolates camp combat, chest claiming, blocked sight, retreat, potion use, PvP, skill use, guard and King combat, and respawn.

The bot brain now separates observation, strategy, and combat decisions. New files include `DeveloperBotSenses.cs`, `DeveloperBotTuning.cs`, and the strategy and combat partials. Updated the bot service, existing loadout logic and validation, and the server consumable path. No scenes or assets were deleted.

## Verification results

Unity compiled the changes. The local Fusion fixture passed opening travel, team admission, camp combat, chest claiming, blocked sight, retreat, potion and skill use, PvP, guard and King damage, and respawn.

A separate 55-second unscripted six-bot run cleared seven camps, killed 19 neutral or enemy actors, and claimed all 17 generated items. At least one bot reached level 2. The run recorded zero gameplay exceptions, network errors, stuck-AI events, invalid inventories, or duplicate loot. It stopped at the configured wall-clock limit before a King victory. Full-round balance, boss contests, and the feel of each difficulty preset remain playtest items. Local report: `Builds/M88-Soak-c045c134/summary.json`.
