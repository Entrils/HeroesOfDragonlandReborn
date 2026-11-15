# Milestone 49 - Match pacing and strategic comeback

## Progression analysis

The existing six-level curve requires 100/150/250/400/600 XP (1,500 cumulative) and awards 1/2/2/3/3 allocation points. These points buy combat attributes; equipment also persists through respawn. Death already retained progression and gear. The battlefield previously used 5-second respawns, increasing to 15 seconds between 10 and 20 minutes, with 3 seconds of protection after respawn. Camps paid 25/50/90/300 XP to every nearby living avatar, including idle opponents; loot was a single first-claim chest. There was no player kill or assist XP.

The main snowball risks are repeatable camp/loot control converting into permanent attributes and equipment; a team ahead arriving first at every central respawn; group proximity multiplying XP without participation; and a new PvP reward turning a weak victim into a repeatable XP source. Punishing deaths with lost levels would compound these problems. A forced stat bonus would obscure combat outcomes. None is introduced here.

The unchanged level curve remains deliberately capped at six levels. Default death XP loss is zero. Returning players keep earned levels, allocation points, skills, inventory and equipment. Counterplay comes from shorter early respawns, publicly displayed PvE catch-up, multiple simultaneous flank opportunities, recurring high-value neutral objectives and a capped late respawn window that lets successful assaults finish.

## Centralized configuration

`Assets/Game/Data/Battlefield/MatchBalance.asset` (`MatchBalanceConfig`) is referenced by `BattlefieldMatch.prefab`. It owns the progression curve reference, respawn ramp and level scaling, death progress-loss rule, kill/assist rewards, assist window, repeat-victim cooldown, camp participation window, four camp pacing profiles, catch-up rules and late-opportunity start. Runtime battlefield code reads this asset. Older test scenes without it retain their existing respawn/camp definitions. The installer preserves an existing config's designer edits.

| Rule | Baseline |
| --- | --- |
| Respawn | 5s, linearly ramps to 18s between 5 and 20 minutes |
| Level contribution | +0.5s per earned level above 1; total capped at 22s |
| Death XP | Retain all XP by default; optional fraction of current next-level progress, capped at 50 XP |
| Player kill | 60 XP; no catch-up multiplier |
| Assists | 30 XP total, equally divided among eligible helpers, remainder discarded |
| Assist eligibility | At least 1 confirmed damage within 12s of death; same team as killer |
| Repeated victim | No further kill/assist reward to the same recipient for 60s |
| Camp participation | At least 1 confirmed damage in the last 60s; living and within the camp's reward radius at clear |
| PvE catch-up | Starts at a 0.75 average-level deficit; +20% per level behind, capped at +50% |
| Late opportunity | 10 minutes of actual Playing time |

Respawn duration is committed on death, replicated by the existing timer and does not change while waiting. The same time/level rule applies to both teams. Spawn-protection duration and resource restoration remain under the existing `SessionRespawnConfig`, which governs respawn safety rather than match economics.

Death penalties never delevel, remove allocated attributes, deduct unspent points or destroy equipment. The common progression boundary checks server authority, current session and unique death IDs. Default zero-loss deaths are still consumed once. Kill/assist attribution reads accepted `DamageReceiver.DamageTaken` events after lethal damage, then settles on a network tick so the synchronous death event cannot precede last-hit attribution. Suicide, friendly damage, missing/disconnected sources and environmental/NPC deaths do not generate PvP XP. A real player must land the lethal hit; old damage does not turn an environmental death into a kill. Dead admitted helpers can receive an assist within the window. The repeat-victim ledger is scoped to recipient/victim network avatar IDs in the round; it is not an account-level anti-collusion service.

## Camps, contested objectives and late opportunities

| Tier | Base XP per eligible participant | First spawn | Repeat after clear | Late XP | Late repeat |
| --- | ---: | ---: | ---: | ---: | ---: |
| Home / Tier 1 | 25 | Immediate | 75s | 25 | 75s |
| Flank / Tier 2 | 60 | Immediate | 105s | 120 | 75s |
| Central / Tier 3 | 120 | 2:30 | 165s | 180 | 120s |
| Stone Warden / Contested | 360 | 2:30 | 210s | 540 | 150s |

XP goes to nearby, living contributors on the team with the greater sum of recent confirmed camp damage. Merely standing nearby no longer earns XP, including teammates. A tie pays neither team. Each member clears its contribution ledger when it evades/returns, so reset damage cannot establish reward ownership. Disconnected actors cannot claim XP. The single loot chest remains independently contested by both teams, even if one team secured XP. No team lock is added to the encounter or loot.

The catch-up modifier uses the mean earned level of each team's admitted avatars, including dead avatars and developer bots. Both teams must have participants. It applies only to camp XP after team eligibility, proximity and survival checks. It never affects damage, health, defense, speed, cooldowns, loot rolls or PvP XP. It turns off at parity or for the leading team, and applies symmetrically if the lead reverses. The HUD displays both teams' current percentages.

After ten minutes, recurring flank objectives pay double XP and reappear sooner; the center/Warden also increase in value and turnover. This provides simultaneous routes to progression rather than forcing all players into one central fight. The existing Warden's Epic/Legendary reliquary remains the high-value physical prize. Existing map routes, fortresses and camp locations are preserved. Late XP is read at clear; a timer already committed at a previous clear is not silently shortened when the clock crosses ten minutes. The next clear uses the late repeat duration.

Camp labels display the current configured XP and next-spawn countdown. The match HUD announces Warden availability, the late surge countdown/state, both catch-up percentages, participation rules and PvP/death rules. Stale static XP signs are replaced with role signs during setup. `MatchPacingPresenter` handles presentation separately from reward policy.

## Authoring and verification

`Heroes of Dragonland > Battlefield > Configure Match Pacing` creates the initial asset if missing, wires the match/player prefabs and HUD, saves assets and scene, and refreshes Fusion registration through Editor APIs. `Validate Match Pacing In Play Mode` checks configuration boundaries, respawn scaling/cap, catch-up parity/threshold/cap, late objectives, death-loss authority/idempotence/session rules, retained levels/attributes, and saved references. All 42 assertions passed; the 69 M48 fortress assertions also passed.

An opt-in `-hodrPacingCheck` development fixture exercises a real host/client session with four roster bots. It uses accepted melee/projectile attacks for kill and assist attribution, checks repeat-victim reward denial, respawn and death retention, camp contribution rewards, replica mutation denial, late opportunity snapshots and a normal King-death round reset. It fast-forwards target health and the match clock and pauses AI to isolate economy/network behavior. It does not measure match difficulty or real match duration. Final build/network results are recorded after acceptance.

Final network acceptance: both peers logged `[PacingNetwork] PASS` in `Builds/PacingAcceptance-611720ae/host-check.log` and `client-check.log`. The Windows development build succeeded with zero errors; build summary is `Builds/Milestone49/build-result.txt`. Both acceptance processes were stopped. A local six-player HUD preview was rendered and inspected in `Builds/Milestone49/pacing-hud.png`. The build initially waited for Unity's modified-scene save dialog; saving the scene resumed it. Final presentation changes are recompiled and included in the final build. The pacing panel's height is accounted for when positioning the boss panel on narrow screens.

Human playtesting remains necessary for time to level six, camp rotations under pressure, repeated-kill cooldown, whether the underdog can access a flank, and coordinated fortress assault success. These are baseline tuning values, not a measured claim of a balanced match.

## Files

New: `Core/MatchBalanceConfig.cs`, `Core/NetworkMatchPacing.cs`, `Core/MatchPacingNetworkProbe.cs`, `Combat/DamageContributionLedger.cs`, `Progression/NetworkPlayerExperienceRewards.cs`, `UI/MatchPacingPresenter.cs`, `World/Editor/MatchPacingSetup.cs`, `World/Editor/MatchPacingValidation.cs`, `Data/Battlefield/MatchBalance.asset` and their Unity metadata.

Modified: `Core/NetworkMatch.cs`, `Player/NetworkPlayerRespawn.cs`, `Progression/SessionProgression.cs`, `Progression/NetworkSessionProgression.cs`, `World/Camps/NetworkCamp.cs`, `UI/NetworkCampPresentation.cs`, `UI/GameplayHudView.cs`, battlefield match/player prefabs and battlefield scene. No new match manager, AI controller or combat-stat adjustment system. No deletions or commits; pre-existing M45-48 work remains intact.
