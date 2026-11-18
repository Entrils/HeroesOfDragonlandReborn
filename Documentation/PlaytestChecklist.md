# Playtest Alpha checklist

Windows x64 Development Build. Extract `Builds/HeroesOfDragonland-PlaytestAlpha-Windows.zip` and launch `Dragonland.exe`, or launch it directly in `Builds/PlaytestAlpha`. Keep the executable, `Dragonland_Data`, `MonoBleedingEdge` and its sibling DLLs together. Use the same build for every participant.

## Session setup

- [ ] In the main menu, open Settings. Check Master, Music and Effects volume, mouse sensitivity, borderless fullscreen, VSync and 75% render scale. Close settings, restart the application and verify the values persisted.
- [ ] The host enters a unique lobby name and selects **Create lobby**. Each tester enters the same name and selects **Join lobby**. Record host/client role, build date and map when reporting an issue.
- [ ] Check alternating team assignment and three slots per side. Set Ready on every human. For fewer than six participants, enable **Allow short match** before readying up, or fill the open slots with bots. Both teams need avatars. The host starts when the lobby is ready.
- [ ] Check a wrong lobby name, leaving and rejoining, disconnect/reconnect during a match, and host departure. Verify the displayed state on both computers. Quit from the menu after testing.

## Gameplay and authority

- [ ] Move with WASD, look with the mouse, and exit each fortress through a flank exit while the main gate is closed. Check collision, remote motion and respawn. Esc releases the cursor for menu controls. Enter resumes capture.
- [ ] Use left click or Space for a light attack, R for heavy, Shift for dodge, right click for block and C for parry. Compare hit, stamina, stagger and recovery on host and client. Include an attempted hit through cover and an ally standing in reach.
- [ ] Use middle mouse to acquire or release lock-on. Use Q/E to switch targets. Check camera and strafe direction, target loss on death or range, and acquisition around walls.
- [ ] Open the Skills panel with K. Learn and activate an available skill. Check resource cost, cooldown, target feedback and the effect from the other peer.
- [ ] Clear an ordered set of camps, record XP and open a reward chest with F. Have two players try the same chest and verify a single valid claim. Open inventory with I, equip and remove loot, check stats and compare the visible weapon or armor on both peers. Allocate an earned progression point.
- [ ] Fight the central boss after it appears. Observe targeting, attacks, phase change and reward. Verify the server owns enemy decisions, damage, loot and XP while remote clients display the outcomes.
- [ ] Destroy a fortress gate, fight its guards and defeat the enemy King. Check one victory and the same winner on both peers. After return to the lobby, begin another match and verify fresh health, progression, inventory, camps, gates and Kings.

## Performance and diagnostics

- [ ] Enable **Record playtest diagnostics** in Settings or launch with `-hodrPlaytestLog`. Samples are written locally to Unity's Player.log every ten seconds. `-logFile <path>` selects an explicit local log. `-hodrDebugUi` restores legacy developer panels when needed by staff.
- [ ] Record graphics mode, resolution, hardware, host/client role and participant count. Capture samples in the menu, in traversal, in crowded combat and during fortress siege. Compare frame time and main-thread time, GPU time, GC bytes/frame, enemy/bot marker time, Fusion bytes/sec and RTT. `n/a` means the platform or frame did not supply a sample.
- [ ] Note visible hitches and their timestamps. Capture representative host and client Unity Profiler traces if performance is a concern. A headless run covers simulation and transport and provides no GPU measurements.
- [ ] Save separate host/client Player.log files. Default path is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\HeroesOfDragonlandReborn\Player.log`. Include reproduction steps, expected/actual result, and an approximate timestamp.

## Automated checks

```powershell
Tests/Standalone/RunBattlefield.ps1 -Executable Builds/PlaytestAlpha/Dragonland.exe
Tests/Standalone/RunSessionSkills.ps1 -Executable Builds/PlaytestAlpha/Dragonland.exe
Tests/Standalone/RunMatchLifecycle.ps1 -Executable Builds/PlaytestAlpha/Dragonland.exe
Tests/Standalone/RunPlaytestFeature.ps1 -Feature Defense
Tests/Standalone/RunPlaytestFeature.ps1 -Feature Boss
Tests/Standalone/RunPlaytestFeature.ps1 -Feature Fortress
```

These development fixtures use `-hodrPlaytestReady` and separate named sessions. Lobby automation drives the normal ready/start flow; individual feature probes may use controlled setup to reach their assertions. See [KnownIssues.md](KnownIssues.md) for evidence and unverified coverage.

The profiler samples report mean frame cadence, maximum frame interval and mean recorder values over the window. Main-thread time can include frame pacing. AI markers cover the managed enemy and developer-bot decision ticks. They exclude rendering, physics and NavMesh worker work. Fusion transport samples and RTT are diagnostic estimates for the current process.
