# Milestone 71 — Hunting Bow

## Gameplay and configuration

Hunting Bow is a Main Hand item in the low-camp loot pool. Its weapon definition requires 11 Dexterity. Hold secondary mouse to aim and press Attack to start the draw. The shot releases automatically after the draw. The default profile has a 0.65 s draw, a 0.28 s release period, 34 units/s projectile speed and up to 6° of aim assistance against a valid, visible target. The weapon has 26 base damage, a stamina cost of 16 and a range of 24 units. The Basic Arrow is the default ammunition definition. Inventory-backed ammunition is an optional extension through `IArrowSupply`.

The same shot logic is used offline and under network state authority. The server resolves the first solid collision from the shooter, applies damage through the normal damage receiver, and publishes the visual shot state and impact. A wall can stop a shot even when the camera can see the target. Equipment changes and interruptions can cancel the draw. The client presents the bow and arrow through the existing character presentation.

Implementation and tuning are in [BowShot](../Assets/Game/Combat/BowShot.cs), [NetworkPlayerCombat](../Assets/Game/Combat/NetworkPlayerCombat.cs), [BowPresentation](../Assets/Game/Presentation/BowPresentation.cs), [HuntingBow](../Assets/Game/Data/Weapons/HuntingBow.asset), [HuntingBowProfile](../Assets/Game/Data/Weapons/HuntingBowProfile.asset), and [BowMilestoneSetup](../Assets/Game/Art/Editor/BowMilestoneSetup.cs).

## Verification

```powershell
./Tests/Standalone/RunPlaytestFeature.ps1 -Feature Bow -Executable $player
```

Set `$player` to the Development executable path. The [bow probe](../Assets/Game/Combat/BowNetworkProbe.cs) exercises the Dexterity gate, assisted and free shots, a server-side obstruction, draw and release states, and projectile and impact replication on host and client. Logs go to `Builds/Playtest-Bow-*/`. Use a rendered pass to check aim, hand placement, and the draw and release animations.
