# Milestone 72 — Deployable traps

Assign a trap from inventory to a quick slot, aim the camera at nearby ground, and press **G** with the slot selected. The server validates ownership, the inventory revision, and placement. It checks range, usable ground, clearance and spacing. A successful placement spends one item. A rejected placement keeps the item and reports the outcome to the requester.

| Item | Damage | Movement effect | Effect radius |
| --- | ---: | --- | ---: |
| Bear Trap | 30 | Stops movement for 0.95 s | 0.85 units |
| Slow Trap | 0 | Movement at 45% for 3.5 s | 1.05 units |
| Magic Mine | 36 | Area damage | 2.7 units |

The tracked definitions share a 5-unit placement range, 2-unit minimum spacing, 0.65 s arming time, and 45 s lifetime. Friendly fire is disabled for all three. The installer adds them to the item catalog and low-camp loot, and registers the prefabs with Fusion. The network object owns trigger and effect timing. It despawns after triggering or expiring, and round reset removes any remaining traps.

See [deployable definitions](../Assets/Game/Data/Deployables), [placement checks](../Assets/Game/World/DeployablePlacement.cs), [inventory transaction](../Assets/Game/Inventory/NetworkInventory.Deployables.cs), [trigger and cleanup](../Assets/Game/World/NetworkDeployable.cs), and [Editor setup](../Assets/Game/Art/Editor/DeployableMilestoneSetup.cs).

## Verification

```powershell
./Tests/Standalone/RunPlaytestFeature.ps1 -Feature Traps -Executable $player
```

The [four-peer fixture](../Assets/Game/World/DeployableNetworkProbe.cs) checks replicated placement and effects, item consumption and rejection, round cleanup, friendly-fire protection, spacing, and movement recovery. Logs go to `Builds/Playtest-Traps-*/`. Check feedback and readability in a rendered client by placing each item once on valid ground and trying an invalid placement.
