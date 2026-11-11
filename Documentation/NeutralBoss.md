# Neutral Stone Warden

The center contested camp now spawns one boss using the existing NetworkCamp, NetworkEnemy, EnemyBrain, Targetable, DamageReceiver and loot chest lifecycle. The previous BossBear assets are retained but no longer spawned by this camp.

The boss has 1,800 HP, 250 stagger resistance, 2.8m/s chase speed, 10m detection, and a 14m leash. It is visibly larger than ordinary enemies. It has no team allegiance or fortress guard component. The existing server targeting policy accepts living, admitted players from either team in the same runner. Sight acquires a nearby player; taking damage changes aggro to the attacker. Aggro changes during a windup affect the next action, while the current danger area remains fixed. Death, lost target, return, stagger and disabled simulation still cancel attacks.

| Attack | Danger area | Windup | Damage | Recovery |
| --- | --- | --- | --- | --- |
| Cleave | 70-degree sector, 4.5m | 1.1s | 34 | 1.4s |
| Sweep | 180-degree sector, 5.5m | 1.5s | 28 | 1.4s |
| Stomp | Full circle, 5m | 1.8s | 40 | 1.4s |

Attacks cycle in that order. A bright orange-to-red ground outline and attack label show the committed area during windup. The server replicates attack index, origin, forward direction and start/end ticks. Presentation derives its timing from the runner render clock. Only the existing server enemy authority resolves damage, at impact, once per eligible player. Every player inside the area can be hit, regardless of team. Range, direction, line of sight, invulnerability and damage policies are checked at impact; moving out of the warning avoids damage. Attacks do not move the boss or spawn projectiles.

EnemyAttackSet is optional configurable data consumed by EnemyMeleeAttack. Ordinary enemies with no attack set retain their original single-target behavior. No separate boss AI or camp controller is added.

The camp opens at 2:30 in the map center, awards 300 XP through the existing nearby living-player rule, and spawns its existing 3–4 drop Epic/Legendary chest after boss death. XP is proximity-based and can reward nearby opponents too. Chest collection remains contested under the existing first-collection rules. No arena lock, team ownership, PvP restriction or separate encounter mode is introduced. The 210s respawn timer starts on clear.

Configure through **Heroes of Dragonland > Battlefield > Configure Neutral Boss**, or reapply the camp tier setup. Assets and prefabs are saved through Unity Editor APIs and preserve their GUIDs.

`Tests/EditorCommands/NeutralBossValidation.cs` invokes Play Mode tests of the shared combat code using an offline fixture. It checks saved boss configuration, neutrality, aggro changes across teams, committed telegraphs, all three AoEs, early damage prevention, evasion, obstruction, cooldowns, cancellation, ordinary enemy attacks, authority-gated brain ticking, death and unchanged PvP team rules. CampDifficultyValidation checks camp references, NavMesh anchors and reward rolls. A two-client multiplayer playtest is still required to assess warning readability under latency and PvPvE balance; the fixture does not simulate remote clients.
