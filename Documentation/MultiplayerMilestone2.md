# Multiplayer Milestone 2 — authoritative stats and damage

## Implementation

Fusion Host/Server owns player Health, death, Stamina and Mana. The existing `CharacterStats`, `ResourcePool`, `MeleeAttack` and `DamageReceiver` remain the gameplay implementation; network components adapt their state and events rather than implement another stats system.

- `NetworkCharacterStats` implements `ICharacterStatsAuthority`. Public gameplay mutation methods fail closed on clients and before network spawn. The server publishes Health, maximum Health and death. Replica hydration does not fire gameplay death/reward events. Dead avatars remain spawned and stop moving/attacking.
- `NetworkPlayerCombat` consumes owner tick input containing attack intent only. The server checks admission/ownership, alive state, attack timing, stamina, overlap geometry, obstruction and one hit per target per swing using the existing melee implementation. No RPC accepts client-selected damage, target, team, resource value or cost. Damage policies require an admitted, living attacker resolving a server swing in the same runner.
- Friendly fire is disabled for this milestone. The server rejects same-team, unassigned-team, self and invalid-source damage. Team identity originates from the server roster.
- `NetworkPlayerResources` publishes Stamina/Mana only to the owning client. Costs, regeneration, delays and maximums are authoritative. Invalid/nonfinite costs and damage are rejected. A client cannot refill resources, heal or resurrect through Stats APIs. Mana spending is validated by Stats; this milestone adds no spell or artificial mana-spending action.
- Regeneration runs on server simulation ticks; melee resolves after player movement ticks. Walking remains independent of resource acknowledgements and costs no stamina. The existing server motor/interpolated rendering is retained; client movement prediction and high-latency responsiveness testing remain outside this implementation. Future sprint/dodge/spell intent must be charged by the server before its gameplay effect.

This trusts the Host as server. A modified client can falsify its own display, but cannot publish authoritative state. Inventory and AI are not networked.

## Validation, 2026-09-09

The continuation inspected the active saved `SampleScene` through Unity MCP (Edit Mode, no compilation in progress). The isolated multiplayer scene remains `Assets/Scenes/MultiplayerFoundationTest.unity`.

`NetworkStatsValidation.cs` executed through MCP: **18 checks passed**, including offline compatibility, resource delay/spend rules, invalid values, one-shot death, blocked dead actions and fail-closed network mutations.

Saved prefab inspection through MCP passed: all three network adapters, existing Stats/melee/damage components, assigned Stats/attack definitions, enabled hit collider and disabled deactivate-on-death. A transient MCP connection failure required retry; the successful inspection did not mutate the prefab.

The existing Windows development assembly is dated 12:12:13 local time, newer than all gameplay sources reviewed. The build invocation recorded **Succeeded, errors=0, warnings=8**; the separate build-result file reports one warning, so this is not described as a warning-free build. No gameplay source was changed during this continuation.

Real independent Photon peers are exercised by `Tests/Standalone/RunMultiplayerCombat.ps1`; log assertions are in `Tests/Standalone/VerifyMultiplayerCombat.py`. Logs are stored under ignored `Builds/MultiplayerCombatValidation/`. Host-to-client and friendly-fire records were retained from the preceding run; missing scenarios were completed in this continuation.

**116 multiplayer log checks passed across five scenarios:** Host attacks Client, Client attacks Host, repeated allied attacks, graceful disconnect during combat, and killed client process during combat. Both attack directions reached replicated zero Health/death on both peers. Friendly attacks left every player at full Health while still charging attack stamina. Client heal/reset/damage/resource mutation attempts were blocked, including resurrection after death. Disconnect cases released the departed player slot and the host continued simulation. The graceful-exit verifier uses the probe's damage-received event because immediate departure can precede the next half-second HP sample.

Unity Console inspected through MCP: **0 errors, 14 warnings**, primarily Fusion/Unity API deprecations plus an MCP executable-certificate warning. These warnings are not suppressed.

Manual checks still needed: keyboard/mouse focus, visual HP/death presentation and movement feel with latency. Headless multi-process simulation does not establish those visual results. Host migration, respawn and networked inventory/AI are not added.

## Files in this milestone

New gameplay files: `Core/ICharacterStatsAuthority.cs`, `Core/NetworkCharacterStats.cs`, `Core/NetworkPlayerResources.cs`, `Core/MultiplayerCombatProbe.cs`, `Combat/IDamagePolicy.cs`, `Combat/NetworkPlayerCombat.cs` (under `Assets/Game/`, with metadata).

Integrated files: `Core/CharacterStats.cs`, `Core/ResourcePool.cs`, `Combat/MeleeAttack.cs`, `Combat/DamageReceiver.cs`, `Core/MultiplayerBootstrap.cs`, `Player/NetworkPlayerInput.cs`, `Player/NetworkTestPlayer.cs`, `UI/NetworkPlayerPresentation.cs` and `UI/MultiplayerTestPanel.cs`. The multiplayer prefab/test scene and `NetworkMelee.asset` supply components/configuration. Earlier unrelated working-tree changes are preserved.

Validation files: `Tests/EditorCommands/NetworkStatsValidation.cs`, `Tests/EditorCommands/SetupMultiplayerCombat.cs`, `Tests/Standalone/RunMultiplayerCombat.ps1`, `Tests/Standalone/VerifyMultiplayerCombat.py`.

This continuation adds this report, corrects the graceful-disconnect assertion in `VerifyMultiplayerCombat.py`, and improves the ignored `Builds/unity_mcp.py` helper to discover tools before invocation and expose protocol errors. No files were deleted and no commit was made.
