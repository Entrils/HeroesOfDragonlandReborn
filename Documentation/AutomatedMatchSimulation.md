# M52 automated match simulation

Implementation and live verification in progress, 2026-11-17. M52 is explicitly authorized by the director; the older M54–M62 deferral no longer applies to M52. M53 remains deferred. No production art/layout/balance changes or new NetworkBehaviours are included.

## Operation

Development/Editor only. `AutomatedMatchSimulation.StartRun(new MatchSimulationOptions { ... })` requires Play Mode and an idle configured battlefield bootstrap. It installs a runtime-only component, starts a local Fusion Single runner, excludes its local connection from avatar/roster admission, requests six existing server-owned developer bots and starts the balanced 3v3 lobby. Ordinary lobby still requires humans and ordinary developer controls still cap at five bots. Release builds contain none of the simulation code.

Successful rounds return through the existing King/Finished/ReturningToLobby/reset pipeline and automatically start again. Timeouts are failures: the driver disconnects/cleans the failed session and starts another attempt, without inventing a King death or counting this as a successful round reset. `StopRun()` cancels and disconnects. The wall-clock limit also records an unfinished round as failed/interrupted. Default limits are three attempts, 1800 wall seconds, 900 seconds per round, 30 seconds per stuck episode, seed 52. Unity random state is restored afterward; production chest rolls retain GUID-based RNG and full determinism is not claimed.

After building the development player:

```powershell
Tests/Standalone/RunMatchSimulation.ps1 -Matches 3 -Seconds 1800 -MatchTimeout 900
```

Raw development-player arguments: `-hodrSoak -hodrSoakMatches 3 -hodrSoakSeconds 1800 -hodrSoakTimeout 900 -hodrSoakSeed 52 -hodrSoakOutput <fresh-directory>`. Do not combine with ordinary `-hodrMode`; the soak driver owns startup. The wrapper launches a hidden child, waits for the flushed stop summary and stops only that child. Local Single runs do not test remote-client transport/replication.

## Diagnostics and definitions

Reports are incremental `events.jsonl`, `summary.json` and `summary.txt`, under the configured fresh output directory or Application.persistentDataPath/MatchSimulation. Each record has UTC time, elapsed wall seconds, round and actor/team/position/goal when applicable. Round summaries and cumulative summaries distinguish started/completed/failed attempts and successful natural resets. Actor samples record health and level; kill/death/level events permit per-actor and per-team aggregation. Kills count credited lethal player attacks on players/NPCs/objectives; deaths count bot deaths. Camp counts use authoritative Cleared events. Items count units actually rolled and actually claimed, including failed-capacity rolls before any claim; empty rolls count zero.

Existing items are fungible definition-ID stacks, not uniquely identified item instances. A developer-only GUID identifies each real chest reward batch; generation and claim counts are deduplicated and quantity-checked. This detects repeated/inflated claims of an instrumented batch, not arbitrary creation by unrelated systems or a complete per-item provenance audit. Legitimate equipment transfers and death/respawn are not treated as new loot. Inventory checks validate capacity, resolved definitions, positive stack amounts, stack limits and equipment-slot compatibility.

Stuck AI means a live, playing bot wants to travel, is not busy attacking, and remains within one metre of its progress anchor for the configured threshold. One event is emitted per episode; progress or legitimate idle/death resets tracking. Unreachable paths still count as inability to progress. This is a diagnostic heuristic, not proof of a pathfinding defect. Exceptions/errors and network failure messages are collected through a bounded background-thread queue, truncated to 8 KiB per message and drained on the main thread. Overflow is explicitly reported. Subscriptions and round tracking are released at boundaries and shutdown.

## Real evidence so far

- Preflight: main at 7ed0bde, only the previously prepared M52 prompt untracked. Saved DragonlandBattlefield in Edit Mode, no dirty scene, Console errors zero. DESIGN.md absent.
- Initial six-bot natural run: `Builds/M52/Smoke/`, 155.75 wall seconds; 1 started, 0 completed, 1 explicitly interrupted, 9 camps cleared, 10 item units generated/claimed, highest level 2, 8 bot deaths, 4 PvP kills (initial collector counted only PvP), 8 stuck episodes, 0 reported exceptions/network errors/invalid inventories/duplicate batch claims. Stopped cleanly to apply observed fixes. No natural victory or reset claimed.
- 21 deterministic loot-ledger/stall/configuration checks passed through Unity MCP (`Builds/M52/invariant-checks.txt`).
- Demonstrated navigation issue: bots stalled near ridge/tower corners despite complete NavMesh paths. The old loop required reaching intermediate corners within 2 cm and checked 80 cm ahead. Corner tolerance now advances only with a clear next NavMesh segment; probe/obstacle lookahead shortened while normal authoritative capsule sweep remains unchanged.
- Existing bot brain never targeted closed gates. Added the real enemy gate as a siege goal before guards/King. All damage still uses ordinary melee/team/authority policies. Soak-only loadout decisions equip legal loot upgrades and spend earned progression points through existing APIs.

## Current checkpoint

Compilation clean after the above changes. A fresh development build and further live runs are underway; record their outcomes here before claiming acceptance. Natural multi-round victory/reset, long soak stability, remote host/client coverage, inventory conservation outside instrumented chest batches and exhaustive reset-leak verification are not yet passed.

Changed source: DeveloperBotService/Brain/Navigation, MultiplayerBootstrap, NetworkMatch, LootChest. New runtime development modules: AutomatedMatchSimulation, MatchSimulationReport, MatchSimulationDiagnostics, MatchSimulationInvariants, MatchSimulationBotLoadout; new Editor MatchSimulationValidation and standalone RunMatchSimulation.ps1. Unity generated metadata preserved. No deleted assets, manual YAML edits, automatic commit or push.
