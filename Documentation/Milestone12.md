# Milestone 12: session experience and leveling

Player now has SessionProgression. Each fresh SampleScene Play Mode session starts
at level 1, 0 XP and 0 progression points. Clearing Test Neutral Camp grants that
player 150 XP: level 2, 50 current XP, 150 required for the next level and 1 point.
The camp's chest still remains closed until the player opens it manually.

## Curve and points

Create > Heroes of Dragonland > Level Progression creates a curve. Each entry is
the XP cost for one level transition and the progression points awarded for that
transition. Thresholds are incremental, not cumulative lifetime XP totals.

DefaultLevelProgression.asset contains:

| Transition | Required XP | Points awarded |
| --- | ---: | ---: |
| 1 -> 2 | 100 | 1 |
| 2 -> 3 | 150 | 2 |
| 3 -> 4 | 250 | 2 |
| 4 -> 5 | 400 | 3 |
| 5 -> 6 | 600 | 3 |

The last entry determines the level cap (6 for the test curve). At the cap,
CurrentXP and XPRequiredForNextLevel are 0; excess XP is discarded and further
grants are rejected. Curves require 1 to 999 transitions with positive thresholds
and nonnegative point rewards. Each session copies its curve so live asset edits
only apply to the next session.

CurrentXP is the remainder toward the next threshold. A grant can cross multiple
thresholds, granting each level's points and carrying the remainder forward. XP
and accumulated points use long values; grants subtract bounded thresholds rather
than adding potentially overflowing amounts. Even long.MaxValue is handled in at
most the configured number of level transitions.

ProgressionPoints are unspent session points. No spending, skill tree, automatic
attribute allocation, health increase or resource refill is introduced.

## Session boundary

The scene's Session Progression object has ProgressionSession with an explicit
player roster. Its early Awake starts a unique session before camp reward sources
bind. A match host can call TryStartSession(newSessionId, out reason) on that
component, or TryBeginSession on a newly spawned player's SessionProgression using
the shared match ID. Configure the player's curve before beginning its session.

A different session ID resets level, XP, points, revision and reward deduplication.
Repeating the current ID is idempotent and preserves earned progression. Ordinary
disable/enable, death and authority changes do not reset it. Per-player data and
received reward IDs are runtime fields only: no PlayerPrefs, save file, static
progression store or DontDestroyOnLoad is used.

The session component resets progression only. A full match host must also recreate
the world/camps for a new match, normally by reloading the scene. Old camp instances
retain their original session binding and cannot grant into a new session merely
because a player was reset. Respawn within a match should retain the progression
component; replacing the player object requires the future match host to transfer
that match's runtime state. No respawn or network snapshot system is added here.

## XP sources and events

All sources use the same SessionProgression API:

```csharp
progression.TryAddExperience(
    500, authoritativeMatchId, "quest:server-issued-reward-id", out string reason);
```

Supply a positive amount, the current match ID and a unique reward ID. For each
player, a reward ID can pay only once in that session. An old match ID, duplicate
reward ID, nonpositive amount, missing ID, uninitialized player or lack of
authority is rejected without changing state or emitting progression events.

CampExperienceSource adapts the existing Camp.ExperienceAwarded event. Each camp
explicitly lists its recipients; each receives the full configured camp reward.
The test camp lists the current player. There are no inferred proximity, kill-credit
or party-sharing rules. The adapter uses camp:<CampId> as its reward ID and captures
each recipient's session at its own Awake. Initialize participants before activating
a new source. Disabling/re-enabling a source retries a recorded clear, and
TryDeliverReward also supports explicit retry after recipient authority returns.
Per-player deduplication prevents a second award. Zero-XP camps do not grant XP.

SessionProgression exposes Level, CurrentXP, XPRequiredForNextLevel,
ProgressionPoints, MaximumLevel, IsMaximumLevel, SessionId and Revision, plus:

- SessionStarted(sessionId) after a fresh reset.
- ExperienceGained(amount) once per accepted grant, containing the submitted
  amount (including any overflow discarded at the level cap).
- LeveledUp(LevelUpEvent) once per crossed threshold, in ascending order. The
  payload gives previous/new levels, that level's points and its cumulative total.
- Changed after a reset or accepted XP grant.

The complete grant commits before observers run, so component properties show
the final level/remainder even during earlier level-event notifications. Reentrant
XP grants, resets and authority changes are rejected during event delivery. Each
subscriber is isolated so a throwing presentation listener cannot stop later
level notifications. Subscribers should read committed state and schedule further
commands after notifications.

CharacterAttributes now resolves ItemStat.Level from SessionProgression when
present, so equipment requirement checks use the actual session level. Gear cannot
modify that level. Actors without progression retain the existing Inspector-based
attribute behavior. Other attributes and PlayerController are unchanged.

Authority is local scaffolding for a future server adapter, not network security.
The server must choose recipients, amounts, session IDs and reward IDs; client
requests must not supply trusted XP amounts. Replica grants/resets are gated, but
transport, replica state application and authority migration are not implemented.

## Debugging and validation

In Play Mode select Player and use SessionProgressionDebug context menus:

- Progression/Log Session Progression prints level, remainder/threshold and points.
- Progression/Grant Debug XP adds the configured Debug XP (500 by default) through
  the same reward API. Every level-up is logged by this separate debug component.

On the Session Progression scene object, Progression/Start New Session resets the
roster with a fresh ID. Reload Play Mode to restart both progression and camps.

Passed through Unity MCP:

- ProgressionValidation.cs: thresholds and exact boundaries, XP remainder,
  multi-level rewards and point totals, ordered events, duplicate/stale/invalid
  grants, independent players, new/same-session behavior, frozen curves,
  authority and reentrancy, disable/enable, actual attribute level, cap and
  long.MaxValue overflow protection, invalid configurations.
- CampProgressionValidation.cs: fresh scene session, partial camp kills, actual
  camp -> 150 XP -> level/points, one-time delivery, separate manual chest loot,
  new-match reset, stale camp/reward rejection and a generic future XP source.
- Fresh Play Mode restart: level 1, XP 0/100, points 0 and a fresh active camp.
- Existing EquipmentValidation.cs regression: slots, requirements, transactions,
  authority, modifiers and melee damage. Final Console: zero errors/warnings.

Run both through Unity MCP in fresh Play Mode; the camp test temporarily pauses
player input/AI, moves the player and changes the match ID. Exit afterwards to
restore scene state. Gameplay balance and physical input remain manual playtests.

## Files

Added: Assets/Game/Progression/LevelProgressionDefinition.cs, LevelUpEvent.cs,
SessionProgression.cs, ProgressionSession.cs and SessionProgressionDebug.cs;
Assets/Game/World/Camps/CampExperienceSource.cs;
Assets/Game/Data/Progression/DefaultLevelProgression.asset;
Tests/EditorCommands/SetupSessionProgression.cs, ProgressionValidation.cs and
CampProgressionValidation.cs; this document and Unity-generated asset metadata.

Modified: Assets/Game/Equipment/CharacterAttributes.cs, Assets/Scenes/SampleScene.unity
and the Milestone11 documentation cross-reference. No files were deleted and no
commit was created. Earlier milestone changes remain in the working tree.
