# Demo release checklist

## Windows candidate

- Version 0.2.0, Windows x64, Unity 6000.6.0f1.
- In Edit Mode, use Dragonland > Demo > Build Windows Development or Build Windows Release. The same entry points are available to Unity batch mode as `HeroesOfDragonland.World.Editor.DemoWindowsBuild.Development` and `HeroesOfDragonland.World.Editor.DemoWindowsBuild.Release`.
- Both configurations start on Dragonland Battlefield and include Training Grounds. Development enables the Unity profiler and gameplay acceptance fixtures. Distribute the Release configuration only.
- Copy the entire Release player directory together, including Dragonland_Data and runtime DLLs. Exclude any Unity backup or debug-symbol directory marked as unsuitable for shipping.
- Ship only after the checks below pass on the exact release package. Record the build report, hardware, Windows version, player logs and any failed steps.

## Playtest instructions

1. Extract the package on a Windows PC without Unity and run Dragonland.exe. Confirm the main menu renders. Change volume, sensitivity and render scale in Settings. Restart the game and verify the values persist.
2. Start Training Grounds offline. Follow the prompts through movement, targeting, defense, skills, loot, the camp and the King. Confirm victory returns to the main menu. Replay and use Esc to check pause and skip.
3. Run Quick Play with six players using the same build. Confirm both teams show three players and all six must ready up before the host can start. Confirm a new search cannot join the active match.
4. Repeat with Private Match. Share the host code and region and try lower-case entry. Check invalid and missing codes, a full lobby, cancellation and return to the menu.
5. Complete a match by defeating the enemy King. Check all six clients agree on the winner and return to the lobby. Start a second match, then leave the lobby and quit from the main menu.
6. During play, interrupt a client's connection and confirm it can reclaim its slot while the host and reservation survive. Repeat around death and a chest claim. End a separate run by closing the host and confirm surviving clients can return to the menu and start a new session.

## Quality gates

| Area | Pass condition | Existing evidence |
| --- | --- | --- |
| Crashes, Console errors and broken references | No crashes, exceptions, missing scripts, broken bindings or scene-load failures through the release flow | Earlier builds exercised both scenes and the menu separately. Review the M68 Editor log and each release player log. |
| Network and reconnect | Both matchmaking paths work, retry exits cleanly, reconnect retains the correct slot and state | M66 online and Alpha resilience fixtures covered selected paths. Repeat over WAN on M68. |
| Stuck AI | Neutral enemies pursue, recover and return without indefinite stalls. Capture map position and target for each stall. | Alpha route recovery reduced stalls. Fortress approaches still need player coverage. |
| Respawns | Each player returns to the correct team spawn, alive and movable, with correct protection and inventory | Earlier team spawn, respawn and resilience checks. Repeat for both teams and after reconnect. |
| Loot duplication | Concurrent claims, full bags and reconnect never duplicate a reward or consume a failed claim | Earlier lifecycle and boss fixtures. Repeat in both rounds and on reconnect. |
| Progression | XP and upgrades come from valid server events, cannot be granted twice, and reset when required | Earlier session skills and lifecycle fixtures. Profile totals are local and should increment once per result. |
| King objective | Only a valid enemy King death ends the round, once. Verify friendly hits, pre-match hits, late hits and second-round reset. | Earlier fortress and lifecycle fixtures. Verify all peers agree on results. |
| Performance | Meet the budgets on the defined six-player test | See [Performance budget](PerformanceBudget.md). |

Earlier evidence is indexed in [Alpha known issues](KnownIssues.md), [First-time onboarding](FirstTimeOnboarding.md) and [Online matchmaking](OnlineMatchmaking.md). Development automation covers selected authority and lifecycle cases. Release sign-off requires a player-controlled pass using the executable alone.

## Release notes

Version 0.2.0 is the first public demo candidate. It includes offline Training Grounds and the 3v3 Battlefield with Quick Play and private room codes. Players can learn combat, equip loot, contest neutral camps and breach the enemy fortress to defeat its King. The demo includes a local player profile and configurable volume, sensitivity and render scale. See [Demo known issues](DemoKnownIssues.md) for session limitations and outstanding verification.

## M68 source review

- Connection handling distinguishes missing, full and closed sessions. Active reconnect depends on a server-side reservation and readmission. Pre-match disconnects return players to a fresh lobby search or code entry.
- Respawn waits for a valid pose from the player's team spawn service. If the service cannot find one, the avatar remains in the respawn state and retries. Exercise obstructed spawn positions during the release test.
- Network chest claims require server and inventory authority, a living claimant and interaction reach. The server keeps one loot roll across capacity failures and commits the claim before notifying inventory observers.
- Player XP derives from server-observed death sequences and uses a repeat-victim cooldown. The King checks the match phase, attacker admission and opposing team. The match finishes only for a dead King owned by the current playing round.
- These are source-review findings. The release gates require runtime confirmation, including navigation recovery, reference binding, connection recovery and duplicate-reward attempts.

## Candidate verification

- Windows Release and Development both built with zero errors. Unity reported 600 Release warnings and 780 Development warnings, including obsolete API and shader-variant warnings.
- Unity MCP reported zero missing scripts and zero dangling serialized MonoBehaviour object references in both included scenes.
- The staged Release executable displayed the main menu and opened Settings on DX11. Settings persistence and the full six-human flow remain open for release sign-off.
- The Development standalone tutorial passed all 18 lessons, offline King victory and return to the main menu.
- The Development two-round lifecycle fixture passed on host and client. It covered replicated winners, return to lobby, round reset, progression freeze and duplicate chest claim rejection. Logs are in `Builds/LifecycleAcceptance-d0594741`.
- The Development Photon fixture passed Quick Play creation and joining, active-match exclusion, private code entry, lobby disconnect, unavailable and invalid code handling, and full-lobby rejection. Logs are in `Builds/OnlineAcceptance-ff71343d`.
- The Development reconnect fixture passed temporary interruption, repeated reconnect, a real stalled client, disconnect during death and chest claim, and cleanup after disconnect at results. Logs are in `Builds/Playtest-Resilience-03c2eec3`.
- The legacy reconnect harness expected `ReconnectExpired` after losing a host in the lobby. The current build reported `LobbyDisconnected` instead, matching the lobby recovery design. The harness did not reach its dedicated-server loss case. Active-match host loss still needs a separate check.
