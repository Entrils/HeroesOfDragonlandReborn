# Milestones 70–82

This handoff was reconstructed from the tracked history through `5ef7922` (`m82`), the serialized game configuration, and the acceptance harnesses. The interval contains the M70 snapshot `b8ed7c2`, the model pack `1f87734`, and the combined M82 snapshot `5ef7922`. M71 and M72 are identified by Editor commands and runtime probes. The Ranked harness names M81 in its default executable path. The combined snapshot leaves the individual M73–M80 boundaries unverified, so its features are documented by system within that range.

| Milestone | Scope |
| --- | --- |
| [M70](Milestone70.md) | Online and Battlefield baseline, potion controls, existing release and playtest evidence |
| [M71](Milestone71.md) | Hunting Bow, aiming, projectile simulation, and replication |
| [M72](Milestone72.md) | Bear Trap, Slow Trap, Magic Mine, and inventory transactions |
| [M73–M80](Milestones73-80.md) | Skill tree, build presets, character customization, cosmetics, combat credit, death recap, spectator view, and match results |
| [Model-pack integration](Milestones73-80.md#model-pack-integration) | Separate art drop, gameplay bindings, and verification |
| [M81](Milestone81.md) | Ranked 3v3 queue and local rating |
| [M82](Milestone82.md) | Platform interface, local achievements, and Steam integration contract |

## Verification

The M70 snapshot contains earlier test records linked from its page. This reconstruction checked the source, configuration and available harnesses. No fresh Unity Console, Player build, or multiplayer log was available for M71–M82 on this machine. The commands in the milestone pages describe what their harnesses exercise and where they put logs.

Build matching Windows x64 players in Unity 6000.6.0f1 using **Dragonland > Demo > Build Windows Development** for the acceptance commands or **Build Windows Release** for a player-controlled smoke test. The menu commands build Battlefield and Training Grounds, write `build-result.txt`, and place the players under `Builds/Demo-<version>/<configuration>/`. Run the PowerShell harnesses from the project root and pass the actual executable path. For a machine without Unity, copy the full player directory including `Dragonland_Data` and runtime DLLs. See the [build entry points](../Assets/Game/World/Editor/DemoWindowsBuild.cs) and [release checks](DemoReleaseChecklist.md).
