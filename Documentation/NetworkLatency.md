# Milestone 86 — Network latency and lag compensation

Photon Fusion input ticks, input redundancy and snapshot interpolation remain the transport and timing foundation. The server accepts or rejects every action, advances the authoritative timeline, performs collision queries, and mutates resources and damage. Clients supply requests and aim intent.

## Input changes

- A human dodge now uses the same monotonic press-counter pattern as light attack, heavy attack, and parry. A short tap can survive between input ticks. Holding Shift no longer starts a second roll after recovery. Dodge still has its existing stamina, cancel, recovery, and invulnerability checks.
- A missing input tick is distinguishable from explicit neutral or suppressed input. The server holds movement, block, and facing for at most 50 ms of consecutive missing ticks. At 5 m/s, this bounds extra unobstructed movement to 0.25 m before modifiers. Sweeps, spawn constraints, and resource checks still apply. Missing samples cannot start an action or change projectile aim. After the grace period, movement and guard stop, and an unfinished bow draw is cancelled. A released projectile or accepted ability keeps advancing on the server.
- After 200 ms of consecutive missing input, the first recovered sample establishes a new baseline for one-shot counters. This may drop a tap made during a sustained outage. It prevents a delayed dodge, parry, or strike from firing when input returns. Movement and block can resume with that sample. Explicit UI or focus suppression is immediate and overrides held input.

## Where lag compensation is used

Fusion's input and snapshot buffering are used for all player traffic. **No gameplay system uses Fusion Hitbox rewind in M86.** The existing rewind buffer remains disabled. The project does not have a hitscan weapon. Rewinding just a target collider for the current attacks would put contact geometry and the defender's invulnerability, guard, parry, or moving cover on different timelines.

| System | Authority and timing decision |
| --- | --- |
| Melee | The accepted windup opens the existing authoritative active window. The server checks live overlap, obstruction, team eligibility, and one hit per target per swing. |
| Dodge | The server consumes the input edge and owns start tick, swept displacement, stamina, recovery, and invulnerability. The window is not backdated. |
| Parry and block | The server consumes the parry edge and resolves direction and windows at the authoritative contact. Brief missing samples can preserve already-held guard. |
| Bow and magic projectiles | The server sweeps each projectile step through live geometry. Travel time, moving targets, and cover resolve together. Bow aim and the optional lock hint are inputs. |
| Movement and correction | Server movement is swept and constrained. Clients render interpolated snapshots. There is no client movement prediction or reconciliation loop in M86. |
| Target lock | Selection and camera are local. Facing is bounded before use. Lock selection does not establish hit eligibility. |
| Ability casting | The existing owning-player request checks the session, skill, resources, cooldown, and cast legality. Server cast, interruption, release, and impact have a single timeline. |

A future short-range rewind experiment should first define how historical dodge, parry, and moving blockers are reconciled, and measure the defender-side tradeoff against this baseline.

## Test matrix

The transport profiles are opt-in for Editor and development players using Fusion Debug DLLs. The runner copies its config, so production settings retain their existing values.

| `-Latency` | Fusion transport settings on both peers |
| --- | --- |
| `30` | 30 ms nominal added RTT |
| `80` | 80 ms nominal added RTT |
| `150` | 150 ms nominal added RTT |
| `jitter` | 80 ms nominal baseline with variable additional packet delay up to 40 ms in the config |
| `burst` | 80 ms baseline with a repeating short high-loss portion of a four-second sine period |

Photon's documented split applies when both peers use the same profile. Measured RTT also includes underlying connection latency and scheduling. Burst loss is probabilistic. The deterministic input check separately exercises a continuous missing-input interval.

Build a Windows Development player with Fusion Debug DLLs. From the repository root:

```powershell
foreach ($profile in '30','80','150','jitter','burst') {
    ./Tests/Standalone/RunPlaytestFeature.ps1 -Feature Latency -Latency $profile -Executable Builds/M86/Dragonland.exe
}
```

The latency probe checks remote movement, dodge, authoritative melee, and rejected client damage mutation. The same runner also accepts `-Feature Defense`, `-Feature Bow`, and `-Feature Magic` with any profile. The Defense probe reacts to confirmed attack onset. At higher RTT that confirmation may arrive after a legal reactive parry is possible, so distinguish that timing limitation from a failed server-side parry. In a hand-played pass, watch movement stops and wall contact, target loss and switching, both sides of the parry window, projectile flight around cover, and cast interruption before release.

Run `NetworkLatencyValidation.Run()` in Unity for deterministic input delivery checks with all five profile shapes. The editor test does not measure transport RTT or replace a real host/client pass.

## Verification results

The Unity 6000.6.0f1 Windows Development build completed with zero errors. Both Fusion peers passed remote movement, dodge, authoritative melee, and the replica damage check for each configuration.

| Profile | Observed RTT across the two peers at completion | Local report folder |
| --- | --- | --- |
| 30 ms | 66?67 ms | `Builds/Playtest-Latency-effdf0a6a8f7` |
| 80 ms | 83?118 ms | `Builds/Playtest-Latency-008f4ec1c353` |
| 150 ms | 184?198 ms | `Builds/Playtest-Latency-c85b9df5a68e` |
| Jitter | 101?133 ms | `Builds/Playtest-Latency-4776c85c4bd7` |
| Burst loss | 116?134 ms | `Builds/Playtest-Latency-2d5d8af5c627` |

The defense probe passed at the 30 ms setting, including successful parries on both sides and replica mutation rejection. Bow and magic passed at the 150 ms setting. Play Mode checks covered lock-on selection and switching, attack windows, block, parry and fire-projectile contact. The five deterministic input checks passed, including stale-press rejection and acceptance of new presses after the blackout.

The RTT numbers are individual samples at completion. The headless checks establish correctness for those scenarios. They do not measure player reaction limits, jitter distribution, or the exact loss percentage in the burst run.

References: [Photon Fusion network simulation](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/simulating-network-conditions), [Photon Fusion lag compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation), [Fusion simulation and snapshots](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/network-simulation-loop).
