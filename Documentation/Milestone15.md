# Milestone 15 — King objective and match victory

## Delivered code and remaining Editor work

King objectives, match state, damage gating, winner presentation, battlefield builder
and validation commands are implemented. Nineteen standalone match-state assertions
passed. Runtime and Editor C# and the new Unity command scripts compile against the
installed Unity 6 / project assemblies.

Unity MCP remains unavailable in this session. The test battlefield has NOT been
created, and live Hierarchy, Console and Play Mode verification have NOT run. Scene
creation and the end-to-end battlefield test remain pending; the builder and commands
below are ready for that connection. No serialized Unity YAML was edited manually.

## King and damage

KingObjective composes TeamIdentity, CharacterStats, DamageReceiver and Targetable.
Assign a CharacterStatsConfig to its Configuration field; the battlefield builder
creates a 1000-health KingStats asset. The King remains stationary with a physical
collider. It has no EnemyBrain or movement logic, allowing separate King AI later.

Every King must be an NPC on its assigned team. Both Kings must be distinct, active,
alive, correctly configured and not owned by another match before a match starts.
Each match binds exactly its Team A and Team B King; unbound Kings cannot take damage.

DamageReceiver now evaluates all attached IDamagePolicy components after ordinary
team checks and before resource changes. KingObjective's policy requires an enemy
team source, the original team identity, an active King/manager, and Playing state.
Friendly damage is rejected even when the attacker enables Allow Friendly Fire.
Null/environmental and unassigned sources are rejected for Kings. Child weapon
sources resolve TeamIdentity through the parent, as in Milestone 14.

Combat must use IDamageable.TakeDamage with the attacker source. CharacterStats'
source-free ApplyDamage remains a low-level resource method, not a combat bypass API.
King defeat is latched and its Defeated event fires once; resetting resources does
not reset the objective or match. Do not reassign King teams during a match; changed
identity fails closed and the bound team remains authoritative for the result.

## Match contract

MatchManager references Team A King and Team B King in the Inspector. It starts in
Waiting. Start Automatically defaults to true and starts in Start (after actor Awake);
otherwise call TryStartMatch(out reason). Invalid roster/start requests leave it Waiting.

The pure MatchStateMachine owns the Waiting -> Playing -> Finished transitions:

- Team A King defeated: Team B wins.
- Team B King defeated: Team A wins.
- Before victory Winner is TeamId.Unassigned.
- The first valid defeat wins. Finished is terminal; duplicate defeats, another King
  death and repeated starts cannot change the winner or start another match.

MatchManager publishes StateChanged(MatchState) and MatchEnded(TeamId winner).
Winner and Finished are committed before observers are called, so both objective
damage gates close before presentation/reentrant callbacks. Every MatchEnded observer
is called once; a throwing observer is logged without suppressing later observers.
The manager catches an already-dead King before accepting reentrant damage to the
other King, including callbacks registered before the King's own death listener.

CharacterStats now dispatches Died before lethal HealthChanged, with isolated death
listeners. This makes objective resolution precede health/UI observers' subsequent
combat actions. Death subscriptions persist across King/manager disable so already
committed deaths are not lost to deactivate-on-death behavior. Disabling a manager
blocks King damage; re-enabling does not reset the result.

MatchStatusDisplay is separate presentation: a small on-screen Waiting/objective/winner
message and a Console report on MatchEnded. Kings stop accepting objective damage
outside Playing. World simulation and ordinary combat are not globally paused.
Restart the scene/create a new match and actors for another round. Existing
ProgressionSession remains the XP/attribute reset boundary; this milestone adds no
networking or progression persistence.

## Battlefield creation and live checks (pending)

1. Run Tests/EditorCommands/SetupKingBattlefield.cs through Unity MCP in Edit Mode.
   It uses KingBattlefieldBuilder and the shared FortressTestSceneBuilder geometry,
   player, guard and NavMesh construction. It does not depend on Milestone 14's scene
   having already been generated.
2. Output: Assets/Scenes/KingBattlefieldTest.unity and assets under
   Assets/Game/Data/KingBattlefieldTest (team materials, crown material, King stats,
   guard definition and baked NavMesh). Two colored fortress areas, two guards, two
   crowned team Kings, one Team A player, MatchManager and winner display.
3. The builder refuses to overwrite existing output, saves/closes the new additive
   scene and restores the previous active scene. Open KingBattlefieldTest alone.
4. Enter Play Mode. Confirm Playing, one assigned King/guard per team, 1000 King health,
   assigned match references, player movement/lock-on/melee and clear Console.
5. Attack Team B King as the Team A player, or select that King and run
   KingObjectiveDebug > Match/Apply Debug Damage To King four times (250 per call).
   This uses the actual player's identity and DamageReceiver, not a forced winner.
   Expect Team A wins, one event, and all subsequent King damage blocked.
6. Restart Play Mode, use player TeamDebug > Teams/Join Team B and kill Team A King.
   Expect Team B wins. Friendly attacks must always do zero King damage, including
   with the player's Allow Friendly Fire enabled.
7. Run KingMatchValidation.cs in Play Mode for isolated integration fixtures covering
   both winners, invalid rosters, high health, targetability, waiting/finished and
   disabled gates, strict friendly fire, enemy child sources, nonlethal/lethal damage,
   early/reentrant death callbacks and one-shot events. Fixtures clean themselves up.
8. Run KingBattlefieldValidation.cs in a fresh playing battlefield for the actual
   scene roster/profile checks and victory through real King DamageReceiver components.
   It finishes the match; reload and switch player team to verify the opposite winner.
   Observe winner UI, guard behavior, King collision/lock-on and final Console.

## Checks completed

- Tests/Standalone/MatchStateValidation.cs: executed against the actual pure
  MatchStateMachine and TeamId sources using .NET 8; PASS, 19 assertions.
- All Assets/Game runtime and Editor scripts: compiled with Unity's Roslyn compiler
  and installed Unity/package references. No errors. Serialized-field CS0649 warnings
  suppressed, matching Unity's Inspector-assigned fields.
- SetupKingBattlefield, KingMatchValidation and KingBattlefieldValidation: compiled
  against those assemblies plus a minimal IRunCommand/ExecutionResult contract stub.
  Compilation does not claim execution under Unity MCP.
- git diff --check passed. Live integration and scene checks remain unverified.

## Files for this milestone

Modified:
- Assets/Game/Core/TeamIdentity.cs (enums extracted without changing numeric values)
- Assets/Game/Core/CharacterStats.cs (death notification order/exception isolation)
- Assets/Game/Combat/DamageReceiver.cs (composable damage policies)
- Assets/Game/World/Editor/FortressTestSceneBuilder.cs (reuse for battlefield variants)

Added:
- Assets/Game/Core/TeamId.cs
- Assets/Game/Core/MatchStateMachine.cs
- Assets/Game/Core/MatchManager.cs
- Assets/Game/Combat/IDamagePolicy.cs
- Assets/Game/World/KingObjective.cs
- Assets/Game/World/Editor/KingBattlefieldBuilder.cs
- Assets/Game/UI/MatchStatusDisplay.cs
- Assets/Game/UI/KingObjectiveDebug.cs
- Tests/Standalone/MatchStateValidation.cs
- Tests/EditorCommands/SetupKingBattlefield.cs
- Tests/EditorCommands/KingMatchValidation.cs
- Tests/EditorCommands/KingBattlefieldValidation.cs
- This document and metadata for new Unity scripts.

No files deleted, commits made, packages installed or generated project files edited.
Earlier Milestone 13/14 changes remain in the working tree.
