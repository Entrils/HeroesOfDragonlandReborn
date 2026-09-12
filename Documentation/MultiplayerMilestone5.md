# Multiplayer Milestone 5 — authoritative PvE AI

## Server simulation

`NetworkEnemy` adapts the existing `EnemyBrain`, `EnemyNavigation`, `EnemyPerception`, `EnemyMeleeAttack`, `EnemyDefinition` and `CharacterStats`. It implements `IEnemyAuthority`: a valid Host/Server-owned NetworkObject with **no input authority** is required. Offline actors without the adapter retain their current behaviour and manual test controls. Setting the old `EnemyBrain.SetAuthority(true)` flag cannot enable client authority on a network enemy.

The network brain skips its ordinary `Update`; the adapter ticks it once per Fusion server tick in `IAfterTick`, after player movement. NavMeshAgent integrates its internal position on Unity's server frame clock with transform position/rotation updates disabled. The adapter copies `agent.nextPosition` and facing to the authoritative Transform in `FixedUpdateNetwork`, then NetworkTransform replicates it. Allowing the agent to write Transform directly between ticks caused Fusion restoration to erase most movement during validation; this explicit handoff fixes that conflict. This is a server-only NavMesh integration, not deterministic rollback/prediction. It uses the server's current transforms and physics visibility at impact; there is no client hit report, client AI RPC or lag compensation.

On replicas, EnemyBrain, EnemyNavigation, EnemyPerception, EnemyMeleeAttack and NavMeshAgent are disabled. The agent is also saved disabled on the prefab to prevent movement before authority callbacks. Direct client calls into Tick, TryStart, MoveTo, Face and perception fail closed through the authority adapter, even if a local script re-enables the behaviour. Clients neither select targets nor spend AI cooldowns, apply damage, regenerate enemy stats or decide death.

`EnemyThreatTarget.NavigationPosition` provides an authored feet/navigation position. Existing offline actors default to their root position; center-rooted network players use a `(0,-1,0)` offset. This keeps attack range, navigation stopping distance and leash tests consistent without changing the existing player transform convention.

## Targets, combat and death

The server's target policy accepts only living player objects registered to a connection in the same runner. Neutral enemies can target either player team; they do not acquire other neutral enemies, unrelated offline actors or objects in another runner. Detection, visibility, retaliation, chase, leash/return and target invalidation reuse existing AI logic. Disconnect/despawn and player death invalidate the server target and cause return behaviour.

Enemy damage is accepted by `NetworkPlayerCombat` only during a living server enemy's actual `EnemyMeleeAttack` damage call in the same runner. The attack checks cooldown, current range, committed facing and obstruction; it resolves once per swing. Existing server dodge invulnerability remains in CharacterStats. Player damage to enemies is accepted only during an admitted server player swing, using existing melee geometry and duplicate-target prevention. Arbitrary direct `DamageReceiver.TakeDamage` calls outside those contexts are rejected.

`NetworkCharacterStats` replicates public enemy health/max health/death. Death cancels attacks, stops the agent, clears the target, disables colliders and publishes Dead on the server. Replicas apply corpse presentation and collision changes from that state; they do not invoke authoritative Died/reward logic. Corpses remain network objects for this slice, so late joiners see the final health/state without replaying death or previous attacks. Respawn, corpse cleanup policy and network camp rewards are future gameplay work.

## Data and scene integration

`NetworkEnemySpawner` subscribes to `MultiplayerBootstrap.SessionStarted` and validates all authored NavMesh spawn points before calling server `Runner.Spawn`. Its prefab/position entries are configurable in the Inspector; no enemy-type switch or client spawn command exists. Runner shutdown owns despawn, and a new session spawns fresh enemies. The existing offline Camp/loot/progression systems are not silently converted to network rewards by this change.

The new `Assets/Scenes/MultiplayerPveTest.unity` is based on the multiplayer foundation arena and adds a saved NavMesh plus three server-spawned enemies. The original foundation scene and SampleScene remain intact.

- Wolf uses the existing Wolf definition and 60-health profile.
- Bear uses the existing Bear definition and 180-health profile.
- Knight uses a new definition copied from the existing Bandit melee-humanoid configuration and shares its 85-health profile. No separate Knight definition/model existed in the inspected project.

All three network prefabs reuse the existing placeholder body. This milestone adds working authority/presentation support, not final wolf/bear/knight art. Future enemy variants can use another EnemyDefinition and a prefab with the same component contract; new behaviour such as ranged attacks will require its corresponding server-authoritative attack component.

## Replication and six-player scope

Only root transform, public Stats, FSM state, target NetworkId, spawn index, attack sequence and start/end ticks/busy state are replicated. NavMesh paths, perception results, timers and decision internals remain on the server. No per-frame animation RPC or per-bone synchronization is used. Unchanged Networked properties remain unchanged in Fusion's state delta; scale/parent synchronization is disabled on these root prefabs.

Target acquisition is limited to 5 Hz for network enemies; navigation paths already refresh at 4 Hz. Current-target legality and hit visibility still run at the simulation/impact tick. A few visible enemies are replicated to all six players, including late joiners. No spatial partitioning, large-enemy pooling or hundreds-of-enemies infrastructure is introduced.

`NetworkEnemyPresentation` reads the accepted state and renders a simple windup/death pose using Fusion render time. Remote transforms and action progress share the remote render timeline. End ticks cut off cancelled attacks, and old snapshots do not replay a completed windup. Presentation never calls enemy decision, attack or damage methods.

## Validation

Acceptance pending standalone runtime verification. Editor/offline checks completed so far:

- `PveOfflineValidation.cs`: 37 checks across Wolf, Bear and melee humanoid, including target acquisition, timing/cooldown, current hit range, wall obstruction, target death and one-time enemy death. Test objects are deactivated before deferred Play Mode destruction so one fixture cannot obstruct the next.
- `PvePrefabValidation.cs`: three prefabs, Stats/definition references, disabled agents/offline presentation, visual references, Fusion bake and player navigation point passed.

`RunMultiplayerPve.ps1 -Scenario combat` starts Host plus five Clients. The development probe drives owner movement/attack input against all three enemy profiles, observes real damage and death, verifies client mutation guards, then replaces a disconnected peer with a late observer of the corpses. `-Scenario disconnect` tests an acquired client leaving and the enemy returning home. `VerifyMultiplayerPve.py` validates the resulting logs. The probe is dormant outside development `-hodrPve` automation and does not grant client authority.

Build: `Builds/MultiplayerPve/DragonlandPve.exe`. Logs: `Builds/MultiplayerPveValidation/`. Headless presentation checks validate state, not the quality of final animation/art. Deliberate packet-loss/high-latency play, dedicated-server deployment and final animation review remain manual/future checks.

## Files

Modified: `EnemyBrain.cs`, `EnemyNavigation.cs`, `EnemyPerception.cs`, `EnemyMeleeAttack.cs`, `EnemyThreatTarget.cs`, `NetworkPlayerCombat.cs`, `MultiplayerBootstrap.cs`, `MultiplayerValidationProbe.cs`, and the existing network player prefab (adds the target/navigation point).

New runtime: `IEnemyAuthority.cs`, `NetworkEnemy.cs`, `NetworkEnemySpawner.cs`, `NetworkEnemyPresentation.cs`. New development/editor tooling: `MultiplayerPveProbe.cs`, `MultiplayerPveSceneBuilder.cs`, `SetupMultiplayerPve.cs`, `PveOfflineValidation.cs`, `PvePrefabValidation.cs`, `RunMultiplayerPve.ps1`, `VerifyMultiplayerPve.py`.

New assets: `Assets/Game/Data/MultiplayerPve/` (three network prefabs, Knight definition, NavMesh) and `Assets/Scenes/MultiplayerPveTest.unity`, with Unity-generated metadata. No assets deleted. No commit. Existing Milestone 4 working-tree changes are preserved.
