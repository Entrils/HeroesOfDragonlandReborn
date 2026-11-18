# M52 automated match simulation

Development soak for Dragonland Battlefield. It uses ordinary combat, the existing server-owned developer bots and the production match lifecycle.

## Operation

Development/Editor only. `AutomatedMatchSimulation.StartRun(new MatchSimulationOptions { ... })` requires Play Mode and an idle configured battlefield bootstrap. It installs a runtime-only component, starts a local Fusion Single runner, excludes its local connection from avatar/roster admission, requests six existing server-owned developer bots and starts the balanced 3v3 lobby. Ordinary lobby still requires humans and ordinary developer controls still cap at five bots. Release builds contain none of the simulation code.

Successful rounds return through the existing King/Finished/ReturningToLobby/reset pipeline and automatically start again. Timeouts are failures: the driver disconnects/cleans the failed session and starts another attempt, without inventing a King death or counting this as a successful round reset. `StopRun()` cancels and disconnects. The wall-clock limit also records an unfinished round as failed/interrupted. Default limits are three attempts, 1800 wall seconds, 900 seconds per round, 30 seconds per stuck episode, seed 52. Unity random state is restored afterward; production chest rolls retain GUID-based RNG and full determinism is not claimed.

In Unity, open `Assets/Scenes/DragonlandBattlefield.unity`. In Edit Mode, use **Tools → Dragonland → Match Simulation → Build Windows Player** to build `Builds/M52/Player/Dragonland.exe`. To run directly in Unity, enter Play Mode without creating a lobby and select **Start Soak** in the same menu. **Stop Soak** flushes the report and disconnects the test session.

For a separate Windows process with incremental reports:

```powershell
Tests/Standalone/RunMatchSimulation.ps1 -Matches 3 -Seconds 2400 -MatchTimeout 780 -StuckSeconds 30
```

Raw development-player arguments are `-hodrSoak -hodrSoakMatches 3 -hodrSoakSeconds 2400 -hodrSoakTimeout 780 -hodrSoakStuck 30 -hodrSoakSeed 52 -hodrSoakOutput <fresh-directory>`. The soak driver owns startup. The Windows player flushes its reports and exits automatically. An unsuccessful run returns a nonzero exit code. A passing run requires the requested number of King victories and validated resets, with zero errors, invalid inventories, duplicate loot claims or dropped error logs. Stalls are reported for investigation. A local Single run covers server gameplay and lifecycle. Remote transport and replication need a separate peer run.

```powershell
python Tests/Standalone/SummarizeMatchSimulation.py <report-directory>
```

## Diagnostics and definitions

Reports are incremental `events.jsonl`, `summary.json` and `summary.txt`, under the configured fresh output directory or Application.persistentDataPath/MatchSimulation. Each record has UTC time, elapsed wall seconds, round and actor/team/position/goal when applicable. Round summaries add simulation time in Playing, winner or failure reason, and per-attempt counters. The cumulative summary distinguishes started, completed, failed and naturally reset rounds. A reset validates the 3v3 roster, reconnect reservations, cleanup status and despawn of round-owned network objects. Actor samples record health and level. Kills count credited lethal bot attacks on players, NPCs and objectives. PvP kills, destroyed gates and King deaths have separate counters. Deaths count bot deaths. Camp counts use authoritative Cleared events. Items count units actually rolled and claimed, including failed-capacity rolls before any claim. Empty rolls count zero.

Existing items are fungible definition-ID stacks, not uniquely identified item instances. A developer-only GUID identifies each real chest reward batch; generation and claim counts are deduplicated and quantity-checked. This detects repeated/inflated claims of an instrumented batch, not arbitrary creation by unrelated systems or a complete per-item provenance audit. Legitimate equipment transfers and death/respawn are not treated as new loot. Inventory checks validate capacity, resolved definitions, positive stack amounts, stack limits and equipment-slot compatibility.

Stuck AI means a live, playing bot wants to travel, is not busy attacking, and remains within one metre of its progress anchor for the configured threshold. One event is emitted per episode; progress or legitimate idle/death resets tracking. Unreachable paths still count as inability to progress. This is a diagnostic heuristic, not proof of a pathfinding defect. Exceptions/errors and network failure messages are collected through a bounded background-thread queue, truncated to 8 KiB per message and drained on the main thread. Overflow is explicitly reported. Subscriptions and round tracking are released at boundaries and shutdown.

## Real evidence so far

- Preflight: main at 7ed0bde, only the previously prepared M52 prompt untracked. Saved DragonlandBattlefield in Edit Mode, no dirty scene, Console errors zero. DESIGN.md absent.
- Initial six-bot natural run: `Builds/M52/Smoke/`, 155.75 wall seconds; 1 started, 0 completed, 1 explicitly interrupted, 9 camps cleared, 10 item units generated/claimed, highest level 2, 8 bot deaths, 4 PvP kills (initial collector counted only PvP), 8 stuck episodes, 0 reported exceptions/network errors/invalid inventories/duplicate batch claims. Stopped cleanly to apply observed fixes. No natural victory or reset claimed.
- 21 deterministic loot-ledger/stall/configuration checks passed through Unity MCP (`Builds/M52/invariant-checks.txt`).
- Demonstrated navigation issue: bots stalled near ridge/tower corners despite complete NavMesh paths. The old loop required reaching intermediate corners within 2 cm and checked 80 cm ahead. Corner tolerance now advances only with a clear next NavMesh segment; probe/obstacle lookahead shortened while normal authoritative capsule sweep remains unchanged.
- Existing bot brain never targeted closed gates. Added the real enemy gate as a siege goal before guards/King. All damage still uses ordinary melee/team/authority policies. Soak-only loadout decisions equip legal loot upgrades and spend earned progression points through existing APIs.

## Windows verification, 2026-11-18

- The saved Battlefield scene built as a Windows development player with zero errors and 135 warnings. The Editor Console reported zero errors after building. All 21 loot, stall and configuration checks passed again.
- `Builds/M52/Siege-20261118` ran for 1,254.21 wall seconds. Six bots started each of three rounds. Team B killed the King and triggered a validated lobby reset in the first two. Their Playing durations were 185.80 and 248.80 simulation seconds. The third attempt reached the 780-second wall timeout after 211 simulation seconds. The CLI exited `1` and retained all three reports.
- The three-attempt run recorded 93 kills, including 15 PvP kills, 39 bot deaths, four gates destroyed, two Kings killed, 25 camps cleared, 40 item units generated and claimed, and a maximum level of 3. It detected 13 stuck episodes and recorded zero exceptions, network errors, invalid inventories, duplicate loot claims or dropped error logs.
- `Builds/M52/Repeat-20261118` passed with exit `0` after 876.37 wall seconds. It started and completed two full 3v3 rounds, with two validated resets and zero failures. Team B won both after 549.33 and 302.59 simulation seconds in Playing. The series recorded 112 kills (20 PvP), 46 bot deaths, three gates, two Kings, 34 camps, 54 item units rolled and claimed, and a peak level of 4. It reported 26 stuck episodes and zero exceptions, network errors, invalid inventories, duplicate loot claims or dropped error logs. The per-actor and per-team breakdown is in `analysis.json`.
- Before adding the soak-only siege role, bots attacked defenses but could spend long periods chasing guards and opposing players. Once the opening farm phase ends, one bot per team now targets the King after the gate falls while the other bots clear guards and fight the enemy team. All attacks, movement, rewards and victories still go through normal gameplay. Some long stalls near fortress approaches remain visible in the report.
- A comparative steering experiment validated each candidate step against the NavMesh, collision and spawn boundary before selection. The run recorded new stalls at NavMesh corners and slower camp progression. The experiment was reverted and its player stopped after 460 seconds of recorded wall time (`Builds/M52/Navigation-20261118`). The reports preserve the recurring approach coordinates for further investigation.
- `Builds/M52/Timeout-20261118` deliberately capped two rounds at six wall seconds. Both were recorded as failed in 12.81 wall seconds overall. After each timeout there were zero avatars and reconnect reservations. The second round started automatically in a fresh session. Exit `1` was expected.
- `Builds/M52/LobbyPeer-36eb64d3` passed the separate two-process `-hodrLobbyCheck` fixture on host and client. It verified the ready gate, match start and replicated return to lobby. The fixture causes a King death to exercise the transition. The older `-hodrLifecycle` fixture joined two peers but timed out awaiting match start in the ready lobby (`Builds/LifecycleAcceptance-666b0bfb`). Full remote soak coverage is still open.

Reproduce the three-attempt run from a fresh build, or rerun with a larger wall limit when investigating stalls:

```powershell
Tests/Standalone/RunMatchSimulation.ps1 -Matches 3 -Seconds 2400 -MatchTimeout 780 -Output Builds/M52/Soak-New
Tests/Standalone/RunMatchSimulation.ps1 -Matches 2 -Seconds 2460 -MatchTimeout 1200 -Output Builds/M52/Repeat-New
```

The reported wall and simulation durations use different clocks. The wall timeout still applies if the player advances fewer simulation ticks during a period. Inventory provenance coverage is limited to tracked chest batches and structural inventory checks.
