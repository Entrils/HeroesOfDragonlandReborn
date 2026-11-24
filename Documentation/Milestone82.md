# Milestone 82 — Platform and achievement foundation

The platform layer exposes identity, private-room invitations, presence, and achievements behind `IGamePlatform`. The shipped configuration uses the local provider and local profile storage. Players can share a private-room code through the existing Private Match flow. A Steam adapter can install a provider through the contract documented in [SteamIntegration.md](SteamIntegration.md).

Completed match receipts feed six achievement rules covering a first victory, an enemy player kill, ten camp clears, a neutral boss kill, an enemy King kill, and winning without dying. The local profile persists the unlocks and deduplicates match receipts. The session bridge resubmits saved unlocks when a provider is installed and retries submissions it has yet to acknowledge.

See [platform interface](../Assets/Game/Core/GamePlatform.cs), [session bridge](../Assets/Game/Core/PlatformSessionBridge.cs), and [achievement rules](../Assets/Game/Core/AchievementRules.cs).

## Verification

```powershell
./Tests/Standalone/RunPlatformAchievements.ps1 -Executable $player
```

The development fixture submits synthetic match receipts, relaunches an isolated profile, and checks all six local achievements and duplicate protection. The [provider validation](../Tests/EditorCommands/PlatformFoundationValidation.cs) exercises the adapter contract with a test provider. Live Steam invites, presence and achievement delivery require a Steam provider and configured test app.
