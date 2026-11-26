# Neutral Stone Warden

## Character art

The Warden wears a shattered basalt crown and a deeply cut funerary mask. Pale cheekstones and narrow mineral eyes frame the dark jaw. Ribbed buttresses behind the shoulders give the upper body a distinct outline during a windup. Interlocking bronze chains fasten the dragon seal to the chest. The crown raises the top of the resting mesh from 3.56 m to 3.71 m. [Full figure](../ArtSource/Characters/SK_StoneWarden/StoneWarden.png) · [Mask and seal](../ArtSource/Characters/SK_StoneWarden/StoneWardenDetail.png) · [Rear silhouette](../ArtSource/Characters/SK_StoneWarden/StoneWardenBack.png)

Blender 5.3 evaluates the editable master at 927,000 triangles. The playable FBX contains 120,508 triangles and one skinned mesh. The exporter reduces the surface before baking a 2048² base color, tangent normal and metallic/smoothness atlas. The six review renders use the game geometry and baked maps. [Cleave](../ArtSource/Characters/SK_StoneWarden/StoneWardenCleave.png) · [Sweep](../ArtSource/Characters/SK_StoneWarden/StoneWardenSweep.png) · [Stomp](../ArtSource/Characters/SK_StoneWarden/StoneWardenStomp.png)

Rebuild with `blender -b --python-exit-code 1 --python Tools/ArtPipeline/create_stone_warden.py` from the project root. The script writes the master and review images under `ArtSource/Characters/SK_StoneWarden/`, the playable `Assets/Art/Characters/Models/SK_StoneWarden.fbx` and the three `T_StoneWarden_*.png` maps under `Assets/Art/Textures/Characters/StoneWarden/`. The sweep review is new. The existing Battlefield BossKnight prefab references the model and material by their original GUIDs.

A Blender round trip matched the previous FBX's 22 bone names, parent relationships and rest matrices, mesh name and eight ordered material slots. Skin weights summed to one. The exported mesh deformed with the existing Idle, Cleave, Sweep and Stomp FBX clips at nine sampled frames each. Unity reimport, Console, runtime bounds and attack readability at the Battlefield camera distance remain to be checked on the Editor machine.

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
