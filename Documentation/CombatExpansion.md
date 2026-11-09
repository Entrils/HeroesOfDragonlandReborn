# Combat expansion

## Controls

- Light attack: existing left mouse / Space multiplayer input and existing offline Attack action.
- Heavy attack: R (held repeats only after recovery in multiplayer).
- Block: hold right mouse. Release and press again after a guard break.
- Existing dodge: unchanged. Blocking and stagger prevent starting a dodge or attack.
- No parry implemented.

## Rules and tuning

MeleeAttackDefinition preserves the light attack values and adds heavy multipliers: damage 1.8, stamina 1.6, attack and recovery time 1.6, stagger 3. Light stagger defaults to 20. EnemyDefinition adds enemy stagger power. CharacterStatsConfig adds stagger resistance (default 50).

CombatDefense is a separate component required by DamageReceiver. Block is omnidirectional, reduces incoming damage to 25%, drains 10 stamina/second and 1 stamina per incoming damage point. The hit that exhausts stamina retains block reduction; exhaustion drops guard and requires release before blocking again. Guard break lasts 1 second. Normal stagger lasts 0.45 seconds. Stagger buildup decays at 20/second. Reaching resistance interrupts attacks, dodge, and enemy navigation. Stagger cannot be extended, and its recovery is followed by 1 second of immunity. Exhaustion during immunity still drops guard, without chaining another stun. Defense tuning is serialized on CombatDefense.

## Multiplayer architecture

Clients submit light/heavy/block intent only. Existing server admission, match, team, obstruction, hit deduplication, health and resource policies remain in place. DamageReceiver rejects unauthorized and invulnerable hits before charging block stamina or applying stagger. Defense uses the existing stats simulation clock; replicas do not run defense outcomes. NetworkPlayerCombat publishes heavy attack type, blocking, stagger and guard break sequence. NetworkEnemy publishes stagger. Existing owner-only stamina replication is retained. Presentation reads accepted network snapshots and heavy attack timing.

Saved combat prefabs and SampleScene were updated through Unity MCP. Existing .meta files were preserved. No assets deleted and no commits created.

## Validation

- 25 passing Play Mode checks in ExpandedCombatValidation.cs: light preservation, heavy windup/cost/damage, hit deduplication, block damage and stamina, guard break, release requirement, resistance, interruption, immunity, and rejected non-authoritative mutations.
- Windows development build succeeded with zero errors. Existing Pipeline configuration, deprecated API and shader warnings remain.
- Separate heavy and block host/client scenarios passed 34 checks with VerifyExpandedCombat.py. Both peers observe damage, stagger, guard break and death. Client mutation attempts are rejected.
- Existing client-to-host light attack regression passed 23 checks, including damage, resource privacy, replicated death and rejected client mutations.
- Final Unity Console inspection: zero errors.
- Manual verification: game feel, keyboard/mouse bindings, enemy and player visual feedback in a normal match.

## Modified files

- `Assets/Game/AI/EnemyBrain.cs`
- `Assets/Game/AI/EnemyDefinition.cs`
- `Assets/Game/AI/EnemyMeleeAttack.cs`
- `Assets/Game/AI/NetworkEnemy.cs`
- `Assets/Game/AI/Prefabs/TestBandit.prefab`
- `Assets/Game/Combat/DamageReceiver.cs`
- `Assets/Game/Combat/MeleeAttack.cs`
- `Assets/Game/Combat/MeleeAttackDefinition.cs`
- `Assets/Game/Combat/NetworkPlayerCombat.cs`
- `Assets/Game/Core/CharacterStats.cs`
- `Assets/Game/Core/CharacterStatsConfig.cs`
- `Assets/Game/Core/MultiplayerBootstrap.cs`
- `Assets/Game/Core/MultiplayerCombatProbe.cs`
- `Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab`
- `Assets/Game/Data/Battlefield/HighBear.prefab`
- `Assets/Game/Data/Battlefield/HighKnight.prefab`
- `Assets/Game/Data/Battlefield/LowWolf.prefab`
- `Assets/Game/Data/Battlefield/MediumBear.prefab`
- `Assets/Game/Data/Battlefield/MediumWolf.prefab`
- `Assets/Game/Data/MultiplayerCamps/NetworkCampPlayer.prefab`
- `Assets/Game/Data/MultiplayerFortress/NetworkFortressPlayer.prefab`
- `Assets/Game/Data/MultiplayerFortress/TeamAGuard.prefab`
- `Assets/Game/Data/MultiplayerFortress/TeamAKing.prefab`
- `Assets/Game/Data/MultiplayerFortress/TeamBGuard.prefab`
- `Assets/Game/Data/MultiplayerFortress/TeamBKing.prefab`
- `Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab`
- `Assets/Game/Data/MultiplayerPve/NetworkBear.prefab`
- `Assets/Game/Data/MultiplayerPve/NetworkKnight.prefab`
- `Assets/Game/Data/MultiplayerPve/NetworkWolf.prefab`
- `Assets/Game/Player/LocalGameplayInputGate.cs`
- `Assets/Game/Player/NetworkPlayerDodge.cs`
- `Assets/Game/Player/NetworkPlayerInput.cs`
- `Assets/Game/Player/PlayerController.cs`
- `Assets/Game/Player/PlayerDodge.cs`
- `Assets/Game/UI/NetworkActionPresentation.cs`
- `Assets/Game/UI/NetworkPlayerPresentation.cs`
- `Assets/Scenes/SampleScene.unity`
- `Tests/Standalone/RunMultiplayerCombat.ps1`

## New files

- `Assets/Game/Combat/CombatDefense.cs`
- `Assets/Game/Combat/CombatDefense.cs.meta`
- `Tests/EditorCommands/ExpandedCombatValidation.cs`
- `Tests/EditorCommands/SetupExpandedCombat.cs`
- `Tests/Standalone/VerifyExpandedCombat.py`
- `Documentation/CombatExpansion.md`

## Deleted files

None.
