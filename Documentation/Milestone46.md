# Milestone 46 - Distinct enemy archetypes

All enemies retain EnemyBrain, EnemyNavigation, EnemyPerception, EnemyMeleeAttack, CharacterStats, CombatDefense, and the existing authoritative NetworkEnemy adapter. EnemyCombatTactics supplies combat decisions; there are no separate per-enemy controllers. EnemyDefinition selects the archetype and exposes ranged distance, projectile speed and spell mana cost alongside existing movement/attack settings. Standard remains the default for unclassified existing content. Existing boss attack sets remain intact.

| Archetype | Mechanics |
| --- | --- |
| Wolf | 5.5 m/s chase, 0.1s alert, 0.18s bite windup, 0.85s interval; circles during bite recovery when close. |
| Bear | 2 m/s chase, increased health (at least 160, high tier 220, boss 400), at least 24 damage, 1.1s windup and 2.8s interval for ordinary attacks, 75 poise damage. Bosses preserve their authored attack-set timing and damage. |
| Knight | 3.2 m/s chase, 0.45s weapon strike, 1.6s interval; guards for 1.2s in each 3.2s cycle at close range, faces the target, then drops guard to attack. Uses existing stamina consumption, mitigation, guard break and stagger. Knight stats now provide at least 100 stamina with regeneration. |
| Archer | Holds a distance band around 8m, retreats below 5.6m, repositions sideways under pressure, fires committed arrows up to 12m at 18 m/s; 0.65s windup, 1.8s interval, 14 damage, 90 HP. If retreat paths fail, can shoot while cornered. |
| Mage | Ranged spell at 10 m/s, 1s windup, 2.2s interval, 28 damage, 75 HP. Each cast pays 30 of 90 mana; regenerates 8/s after a 2s delay. Below casting cost, repositions and waits until at least two casts are funded before resuming. |

Ranged attacks commit aim at acceptance, so moving during the telegraph can avoid them. Swept sphere queries prevent tunneling, solid geometry intercepts shots, and each projectile hits at most once. The shared attack remains busy through flight (one outstanding projectile per enemy). Cancel, death, disable, stagger, target loss, return and inactive match gameplay clear the attack. Interrupted spells retain their mana payment and cooldown. Ranged hits use the server's existing damage-policy gate without advertising themselves as parryable melee impacts. Current blocking mitigation also applies to ranged damage, consistent with DamageReceiver.

Navigation samples compatible NavMesh destinations and checks complete paths through the shared navigation component. Retreat/circle destinations respect the home leash; failed repositioning tries the opposite side. Existing targeting, team policies, visibility, leash/return and lost-target rules remain shared. Offline authority now also gates direct attack/navigation calls; network replicas continue to fail closed.

Archer and Mage are saved network prefabs cloned from the existing humanoid enemy prefab, with dedicated definitions and stats under `Assets/Game/Data/EnemyArchetypes/`. High battlefield camps now contain Knight, Archer, Mage, retaining their member count and rewards. Wolves and bears remain in lower camps and existing bosses. The Fusion prefab table is rebuilt by setup. No scene edits are required because camp definitions are referenced assets.

NetworkEnemy publishes blocking along with its existing attack origin, direction and timestamps. NetworkEnemyPresentation displays BLOCK, retains windup motion, and renders yellow arrow / magenta spell streaks from authoritative attack snapshots. These cues are visual only. Existing humanoid art is reused; dedicated bow, staff and spell animation assets remain art work.

Editor commands:

- Heroes of Dragonland > Enemies > Install Archetypes: reapply baseline tuning, save definitions/prefabs/camp references, rebuild network prefab registration. This intentionally reapplies baseline values; edit assets directly for subsequent balance changes.
- Heroes of Dragonland > Enemies > Validate In Play Mode: isolated temporary NavMesh and actors, cleaned in finally. 35 assertions cover spawn configuration, relative movement/health, telegraph timing, actual ranged impact, dodging committed aim, obstruction before/during flight, one-hit behavior, mana spending/rejection/regeneration, guard mitigation and attack windows, retreat paths, authority denial, wolf bite/circle behavior, bear poise break, mage recovery decisions and stagger cancellation.

Validation: all 35 assertions passed on final source in Play Mode. Unity compiled without source errors and returned to Edit Mode. Windows development build succeeded with zero errors (582 existing package/API/compute-shader warnings and the optional Pipeline runtime configuration warning). Build result is recorded in `Builds/Milestone46/build-result.txt`. Final asset/component/reference checks and Console inspection are performed through Unity MCP. A live host/client match and subjective balance/animation review remain manual verification; the automated fixture exercises offline simulation and does not claim a multiplayer acceptance run.

Modified runtime files: `Assets/Game/AI/EnemyBrain.cs`, `EnemyDefinition.cs`, `EnemyMeleeAttack.cs`, `IEnemyAuthority.cs`, `NetworkEnemy.cs`, and `Assets/Game/UI/NetworkEnemyPresentation.cs`.

Modified data: Wolf/Bear definitions in `Data/Enemies`; Knight in `Data/MultiplayerPve`; LowWolf, MediumWolf, MediumBear, HighBear, HighKnight, BossBear, BossKnight and HighCamp in `Data/Battlefield`; bear health and knight stamina stats (including the shared BanditStats used by the original Knight).

New files: `EnemyCombatTactics.cs`; `AI/Editor/EnemyArchetypeSetup.cs` and `EnemyArchetypeValidation.cs`; Archer/Mage definition, stats and network prefab assets in `Data/EnemyArchetypes`; this document, and Unity metadata. No existing assets were deleted. Pre-existing Milestone 45 working-tree changes are preserved. No commit was created.
