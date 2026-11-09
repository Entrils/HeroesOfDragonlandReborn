# Multiplayer Milestone 8 — fortress objectives

Open `Assets/Scenes/MultiplayerFortressTest.unity` and use the existing Host/Client panel. Blue Team A and red Team B each have a fortress guard and a stationary 1,000-health King on opposite sides of the arena. The neutral camp and M7 chest/player composition remain available. Movement, melee and dodge controls are unchanged. The fortress overlay shows both replicated King health values and the winning team.

## Server-owned guards and Kings

`NetworkFortressSpawner` spawns one server-owned `NetworkMatch`. It creates the two King and two guard prefabs with no player input authority. The prefab references and positions are serialized configuration. Guard prefabs reuse the existing network Knight/EnemyDefinition, NavMesh, perception, attack and health components. `NetworkFortressGuard` adds a strict opposing-player-team policy to targeting and incoming damage; the player damage policy also checks guard allegiance. Friendlies cannot become guard targets or receive guard damage, including during an attack windup. Client NavMesh/AI components remain disabled, and movement, attack and death presentation use server snapshots.

`NetworkKingObjective` composes the existing CharacterStats, NetworkCharacterStats, DamageReceiver, TeamIdentity and Targetable components. It accepts only a live, registered same-runner opposing player's server-resolved melee attack while its owning match is Playing. Neutral, friendly, unregistered, client-direct and fabricated hits fail. There is no client damage, death, team-change or match-finish RPC. This prototype's guards target players; Kings are stationary objectives rather than combat AI.

## One-time match result and shutdown

`NetworkMatch` reuses the pure `MatchStateMachine`. Only the server can finish the match, and only after one of its own Kings is dead. The other team wins. `State`, `MatchEnded`, `Winner`, `FinishCount` and King IDs replicate publicly. The terminal transition commits before notifying observers, so concurrent or reentrant attempts cannot select a second winner. The first lethal server decision wins; no draw rule is introduced. Late clients read the final snapshot without replaying gameplay rewards.

The shared `NetworkMatch.AllowsGameplay` gate checks both match state and authoritative King deaths. That second check blocks reentrant combat/rewards even before the King's death callback commits Finished. It is enforced at these existing boundaries:

- CharacterStats authority: damage, healing, regeneration, stamina/mana use and resets.
- Player melee/dodge intent and hit resolution; pending melee is cancelled and presentation stops.
- Enemy AI/attack authority; guard and neutral-enemy paths stop and pending attacks cancel.
- Camp completion/chest creation, chest interaction and inventory/equipment mutations.
- XP, levels and attribute progression through the existing progression authority adapter.

Health snapshot publishing uses raw server ownership rather than the gameplay gate: the lethal King health change must still replicate after the death callback closes gameplay. Fresh player session initialization is allowed during spawn, including late join, while subsequent XP and attribute changes remain blocked. Player movement and disconnect are still available after match end.

The gate is active in scenes with `NetworkFortressSpawner`; absent that component, earlier multiplayer slices retain their previous behavior. A fortress scene with no valid match stays closed to gameplay. Offline MatchManager/KingObjective and their rules are unchanged. Host migration, rematches and a draw policy are outside this milestone.

## Validation tools

- `Tests/EditorCommands/NetworkFortressPrefabValidation.cs` checks saved King/guard/match composition, Fusion bake, identities, health profiles, art, references, NavMesh and scene integration.
- `Tests/Standalone/RunMultiplayerFortress.ps1 -AttackingTeam A|B` launches host + client, drives the selected owner through production movement/melee to kill the opposing guard and King, checks shutdown, then joins a late client.
- `Tests/Standalone/VerifyMultiplayerFortress.py <root> A|B` verifies complete peer logs, exact damage totals, server-only decisions, terminal state/event counts and post-end gates.
- `MultiplayerFortressProbe` is opt-in with `-hodrFortress A|B` in Editor/development builds. It has no active production behavior without that argument.

## Completion verification (2026-09-14)

- Resumed and audited the saved implementation; no additional runtime changes were required.
- Unity MCP: 43 saved fortress prefab/reference checks passed.
- Fresh standalone host/client/late-client runs passed for both attacking teams. `python Tests/Standalone/VerifyMultiplayerFortress.py Builds/MultiplayerFortressFinal A B` passed all 136 checks. Logs are under `Builds/MultiplayerFortressFinal/A` and `B`.
- Both offline King and guard regressions passed in Editor Play Mode. The final Console inspection reported zero errors and zero warnings; Editor returned to Edit Mode.
- The standalone build reports `Succeeded errors=0`; its runtime sources predate the build. Existing build warnings remain (Photon/development directives and shader/package warnings).
- The acceptance launcher now invokes the complete log verifier before reporting success, and the verifier's default output root matches the launcher.
- Manual visual review of fortress UI and normal two-person play remains useful; automated runs use batch mode.

## Changed files

Modified for M8: `Assets/Game/AI/NetworkEnemy.cs`, `Assets/Game/Combat/MeleeAttack.cs`, `Assets/Game/Combat/NetworkPlayerCombat.cs`, `Assets/Game/Core/MultiplayerValidationProbe.cs`, `Assets/Game/Core/NetworkCharacterStats.cs`, `Assets/Game/Inventory/NetworkInventory.cs`, `Assets/Game/Player/NetworkPlayerDodge.cs`, `Assets/Game/Progression/NetworkSessionProgression.cs`, `Assets/Game/UI/NetworkActionPresentation.cs`, `Assets/Game/UI/NetworkEnemyPresentation.cs`, `Assets/Game/World/Camps/NetworkCamp.cs`, `Assets/Game/World/NetworkLootChest.cs`.

New runtime: `NetworkMatch.cs`, `NetworkFortressSpawner.cs`, `NetworkKingObjective.cs`, `NetworkFortressGuard.cs`, `NetworkFortressPresentation.cs`. New tooling: `MultiplayerFortressProbe.cs`, `MultiplayerFortressSceneBuilder.cs`, `NetworkFortressPrefabValidation.cs`, `RunMultiplayerFortress.ps1`, `VerifyMultiplayerFortress.py` and this document.

New assets: `Assets/Scenes/MultiplayerFortressTest.unity`, `Assets/Game/Data/MultiplayerFortress/` (match, two guard/two King prefabs, King stats), `Assets/Art/Materials/MultiplayerFortress/`, and Unity-generated metadata. Scene/prefabs/assets were authored and saved through Unity MCP. Existing M7 working-tree changes were preserved. No assets deleted and no automatic commit.
