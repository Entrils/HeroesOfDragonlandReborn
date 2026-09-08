# Milestone 10: reusable neutral enemy AI

SampleScene contains one Test Bandit at (10, 0.08, 8), with a reusable prefab at
Assets/Game/AI/Prefabs/TestBandit.prefab. Approach within 7 metres to trigger its
alert and chase. Its label shows its state and turns red with WINDUP! before a hit.
Use the existing melee, dodge and middle-mouse lock-on controls to fight it.

## Components and configuration

EnemyBrain owns Idle, Patrol, Alert, Chase, Attack, Return and Dead transitions.
EnemyPerception selects the nearest living, visible EnemyThreatTarget within the
detection radius and spawn leash. The player has this opt-in marker; other enemies
and target dummies do not. EnemyNavigation handles complete NavMesh paths, speed,
stopping and facing. EnemyMeleeAttack handles windup, cooldown and one damage
application per swing. EnemyDebugVisual is a separate test presentation component.

EnemyDefinition is a ScriptableObject created through Create > Heroes of Dragonland
> Enemy Definition. It configures stats, sight aggression, detection, leash, patrol,
idle/alert/lost-target timing, movement speeds, attack range, damage, windup and
cooldown. CharacterStatsConfig supplies health using the existing CharacterStats.
ConfigureForSpawn initializes those resources once on enemy creation.

Included profiles:

| Profile | Health | Chase speed | Attack range | Damage | Cooldown |
| --- | ---: | ---: | ---: | ---: | ---: |
| Wolf | 60 | 4.5 | 1.4 | 8 | 1.0 |
| Bear | 180 | 2.8 | 2.0 | 20 | 1.8 |
| Bandit | 85 | 3.5 | 1.7 | 12 | 1.2 |

Distances are Unity units; timing is seconds. Only the Bandit has a test instance
and placeholder visual. For a Knight or future melee monster, duplicate a profile
and prefab, assign the new definition on EnemyBrain, replace the visual, and adjust
the collider/NavMeshAgent dimensions to the creature. No enemy-type switch or new
brain subclass is required. Turn off Aggro On Sight for passive-until-attacked
behavior. The included profiles enable proximity aggression.

## Behavior and integration

Idle waits, then Patrol visits deterministic points around spawn. Detection or
damage from a living marked actor triggers Alert, then Chase. Both the enemy and
its target must remain within the spawn leash. Lost visibility times out after
the configured interval. A dead, disabled or destroyed target is released. Return
clears aggression, cancels swings and goes home before resuming Idle; incoming
damage still reduces health but does not interrupt Return. Returning does not heal.

Attacks stop movement, wait for the windup and recheck target life, range, line of
sight and a 60-degree cone relative to the committed attack direction. A hit uses
the existing IDamageable/DamageReceiver, including dodge invulnerability. Cooldown
is the interval between attack starts and cannot be shorter than the windup.
Existing player attacks can damage the enemy through its DamageReceiver.

Death cancels attacks, disables navigation and colliders, drops the placeholder
body, and invalidates the existing Targetable so TargetLock releases the enemy.
An enemy that dies while its brain is disabled enters Dead on re-enable. Respawn
requires a new enemy instance; this milestone does not implement revival.

EnemyBrain.SetAuthority pauses/resumes autonomous decisions and cancels movement
and pending attacks when disabled. This is a local simulation hook, not network
authentication or replication. No camp, spawning wave, loot-on-death or network
transport logic is introduced.

## Navigation and validation

Arena Navigation holds a NavMeshSurface with the baked TestArenaNavMesh asset.
Agents require placement on this mesh; no direct-transform movement fallback is
used. Incomplete/unreachable chase paths trigger Return. If a return route becomes
unavailable, the enemy stays in Return and retries rather than teleporting.
Rebake the surface after changing arena geometry.

Run Tests/EditorCommands/EnemyValidation.cs via Unity MCP in a fresh Play Mode
session, followed by EnemyEdgeValidation.cs. The main test moves/disables the
player and pauses the scene bandit; exit Play Mode afterwards to restore the scene.
It uses game-time deadlines and enables background execution so losing Editor
focus does not produce a wall-clock timeout. Temporary fixtures are cleaned up.

Both validation scripts passed in Unity Play Mode; the final Console check had
zero errors and warnings. Coverage includes actual NavMesh chase/return, all states, profile health,
detection, leash, windup/cooldown, damage, range/obstruction rejection, dodge,
retaliation, neutral mode, lost/destroyed/dead targets, disable behavior, death
and existing lock-on integration. Physical keyboard/gamepad feel and visual
balancing remain a manual playtest.

## Files

Added: Assets/Game/AI (seven components, EnemyDefinition and TestBandit prefab),
Assets/Game/Data/Enemies (three definitions, three stats profiles and baked mesh),
Assets/Art/Materials/TestBandit.mat, the two enemy validation scripts and this file.
Unity assets retain their generated metadata.

Modified for this milestone: Assets/Game/Core/CharacterStats.cs (spawn profile
initialization) and Assets/Scenes/SampleScene.unity (bandit, player threat marker,
navigation surface). Other working-tree changes belong to preceding milestones.
No pre-existing assets were deleted and no commit was made.
