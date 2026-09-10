# Milestone 14 — teams and fortress guards

## Implemented code

TeamIdentity identifies Team A / Team B and Player / NPC. Unassigned is the neutral
state for existing camps and non-team actors, not a third combat team. Every player
or NPC spawned for team combat must receive an assigned TeamIdentity. FortressGuard
requires that component and refuses unassigned targets or an unassigned guard.

DamageReceiver now checks team rules before applying damage, death, or DamageTaken
events. Friendly fire is disabled by default. The attacker's Allow Friendly Fire
Inspector field (or TryConfigure argument) explicitly permits allied damage.
A child weapon source resolves its identity from its owning parent. Pass the actual
attacker as IDamageable.TakeDamage's source; null remains environmental damage.
Disabling TeamIdentity does not remove allegiance or bypass damage protection.
CharacterStats.ApplyDamage remains the lower-level, source-free resource operation;
combat must use IDamageable.TakeDamage rather than call it directly.

Player lock-on excludes protected allies and releases a target that becomes allied.
Neutral enemies retain their existing sight/retaliation behavior. Team-aware AI ignores
allies even when friendly fire is enabled, so a guard never retaliates against allies.

FortressGuard is a small IEnemyTargetPolicy component composed with EnemyBrain,
EnemyPerception, EnemyNavigation, EnemyMeleeAttack, DamageReceiver and CharacterStats.
It only accepts a living EnemyThreatTarget whose TeamIdentity is an opposing-team
Player. Even an NPC carrying a threat marker is excluded. The policy is checked during
acquisition, retaliation, target retention and attack resolution. A team change during
windup therefore prevents the pending hit.

EnemyBrain continues to own patrol, alert/chase/attack, line-of-sight loss timeout,
leash, return, authority and death. Guard area is anchored at its spawn position.
EnemyDefinition configures patrol radius, detection radius, leash distance, movement,
attack timing and stats. The test guard uses patrol 2, detection 9, leash 11; other
settings reuse Bandit. No King or alternate combat/state-machine implementation.

## Test scene — generation pending Unity MCP

Unity MCP was unavailable in this session. No scene/prefab/ScriptableObject assets
were created or edited, and no Unity Console/Hierarchy/Play Mode checks were possible.
The scene builder is implemented but has NOT been executed. Milestone scene delivery
and live verification are still pending.

Run Tests/EditorCommands/SetupFortressTest.cs through Unity MCP in Edit Mode. It calls
FortressTestSceneBuilder.Build to create:

- Assets/Scenes/FortressGuardTest.unity, separate from SampleScene.
- Two colored, labeled fortress courtyards and one team-assigned guard per courtyard.
- A Team A player using existing movement, camera, lock-on, melee, dodge and resources.
- Baked navigation and a guard definition/materials in Assets/Game/Data/FortressTest.

The builder refuses to overwrite existing output, loads the new scene additively,
saves it, then closes it and restores the previous active scene. It does not close or
save the user's existing scenes. Open FortressGuardTest alone for Play Mode, avoiding
duplicate cameras/players from simultaneously loaded scenes.

Player TeamDebug context menus switch allegiance in Play Mode. Test Team A versus
Team B and reverse them. Existing controls: WASD, mouse look, left-click attack,
middle-click lock, Q/E switch, configured dodge binding, Escape releases cursor.

Run TeamGuardValidation.cs through Unity MCP in Play Mode in FortressGuardTest. Its
temporary fixtures test allied damage/events, friendly-fire opt-in, parent weapon
sources, ignored allied/player/NPC/unassigned targets, opposing-player detection and
real attack damage, changing teams in windup, wall occlusion and loss timeout,
return path requests, patrol transitions, authority and death. It restores no player
state because it only modifies its own temporary actors; fixtures are cleaned up.

Then observe actual NavMesh movement: guards patrol, pursue an intruding opposing
player, attack, lose sight behind a wall or leash beyond the courtyard, return home
and resume patrol. Check both teams, friendly fire off/on, guard death and Console.
Run existing EnemyValidation/EnemyEdgeValidation in SampleScene as regressions.

## Checks completed

All runtime C#, all Game editor C#, and both new command scripts compile against the
installed Unity 6 / project package assemblies using Unity's Roslyn compiler. Script
commands were compiled with a minimal IRunCommand/ExecutionResult contract stub;
that checks C# correctness, not execution in Unity MCP. No compiler errors; Unity's
normal serialized-field CS0649 warnings were suppressed. git diff --check passed.
Behavioral tests and Console checks have NOT run.

## Files for this milestone

Modified:
- Assets/Game/Combat/DamageReceiver.cs
- Assets/Game/AI/EnemyPerception.cs
- Assets/Game/AI/EnemyBrain.cs
- Assets/Game/Player/TargetLock.cs

Added:
- Assets/Game/Core/TeamIdentity.cs
- Assets/Game/AI/IEnemyTargetPolicy.cs
- Assets/Game/AI/FortressGuard.cs
- Assets/Game/UI/TeamDebug.cs
- Assets/Game/World/Editor/FortressTestSceneBuilder.cs
- Tests/EditorCommands/SetupFortressTest.cs
- Tests/EditorCommands/TeamGuardValidation.cs
- Documentation/Milestone14.md
- Metadata for the new Unity scripts/editor folder.

No deleted files, commits, serialized scene edits or generated project-file edits.
Earlier Milestone 13 working-tree changes were preserved.
