# Session skills

The fortress test scene has exactly three single-rank skills. Open **Skills [K]** to learn or activate them. Attribute requirements use innate base plus session allocations, excluding equipment bonuses. Requirements are checked when learning and using a skill; the passive applies only while its requirements remain satisfied.

| Skill | Kind | Requirements | Skill points | Mana | Cooldown | Effect |
| --- | --- | --- | --- | --- | --- | --- |
| Power Strike | Active melee | Level 1, Strength 10 | 1 | 20 | 5 seconds | A normal melee swing with twice the final melee damage; costs no Stamina. |
| Guard | Active defensive | Level 2, Mind 10 | 1 | 25 | 10 seconds | Invulnerability for 1.5 seconds through the existing damage system. |
| Melee Training | Passive | Level 2, Vitality 12 | 1 | 0 | None | Adds 5 melee damage before the active strike multiplier. |

Characters start each session with one skill point and receive one additional point for each level gained. These points are separate from progression points spent on attributes. Skills have no ranks, respec, skill tree, or persistent save data.

## Components and authority

- `SkillDefinition`: ScriptableObject configuration for kind/effect, level/stat prerequisites, point cost, Mana cost, cooldown, and effect magnitude. The three authored assets live in `Assets/Game/Data/Skills/`.
- `SessionSkills`: session-owned learning, skill points, validation, cooldowns, defense duration, and passive calculation. It observes `SessionProgression.SessionStarted` and `Changed`. A new session ID clears learned skills, buffs and cooldowns and restores the initial point; repeating the same ID does not refund points.
- `NetworkSessionSkills`: Fusion authority and transport adapter. Only the server controlling the registered player object mutates skills. Owner RPCs carry only skill index, learn/use intent and session ID. The server checks ownership, session, playing phase, request rate, prerequisites, learned state, life/action state, Mana and cooldown. It replicates learned state, points and timers; clients hydrate presentation state without executing effects.
- `SessionSkillsPanel`: local-player UI requesting actions through `NetworkSessionSkills`. Opening it uses `LocalGameplayInputGate`; it never pauses the server or changes skill state directly.

Power Strike reuses `MeleeAttack` and `NetworkPlayerCombat`, including server attack timing, hit geometry, obstruction, hit deduplication, team rules, and existing damage policy. No client target or damage report is accepted. Guard implements `IInvulnerabilityProvider`. The passive is evaluated for authoritative melee hits without stacking mutable stat modifiers.

Death ends Guard immediately. Learned skills and cooldowns survive an ordinary respawn. A new round recreates players with a new progression session ID, resetting everything in the skill system. Cooldowns tick on the server simulation clock during the playing phase, including while dead; clients display replicated values.

`Dragonland/Install Session Skills` installs the definitions, the actual `NetworkFortressPlayer` spawn prefab, and the UI in `MultiplayerFortressTest`. The installer preserves valid existing skill asset tuning and verifies that the scene references the expected player prefab. Fusion's prefab table is rebuilt and modified assets and scene are saved.

## Validation

- `Tests/EditorCommands/SessionSkillsValidation.cs`: run through Unity MCP in Play Mode. Covers initial/per-level points, invalid indices, unlearned and duplicate requests, level/stat requirements, separate attribute investment, Mana-only strike cost, insufficient Mana without effects, cooldown rejection, passive non-stacking, defense expiration, invalid time, stale session rejection, and complete/idempotent session resets.
- `Tests/EditorCommands/SessionSkillsPrefabValidation.cs`: run through Unity MCP in Edit Mode. Checks the actual spawn prefab, required components, exactly three valid definitions and UI references/theme. Read Console separately with the MCP `Unity_GetConsoleLogs` tool.
- `Tests/Standalone/RunSessionSkills.ps1`: launches a development host and client with `-hodrSkills`. The opt-in probe verifies owner RPCs, inability of replicas to mutate gameplay, replicated Mana/cooldowns/Guard, authoritative defense, real active and normal melee hit damage with the passive, and recreation/reset of both player sessions.

The probe's XP grants, attribute allocations, placement and world AI isolation are test fixtures enabled only by `-hodrSkills` in Editor/development builds. They are not part of ordinary gameplay.

Verified on 2026-11-07:

- Play Mode rule validation and saved prefab/scene validation passed through Unity MCP.
- Development Windows build: `Builds/SessionSkills/DragonlandSkills.exe`, succeeded with zero errors and 31 warnings (existing project warnings plus the development-only probe directive warning).
- Host/client acceptance passed: `Builds/SkillsAcceptance-895acb47/`. Real skill damage was 60 and ordinary melee with the passive was 30; both actors reset successfully into a new session.
- Inventory/equipment UI regression passed against the same build: `Builds/InventoryUiAcceptance-2c66f94f/`.
- Final MCP `Unity_GetConsoleLogs` query for errors returned zero entries. Editor was left in Edit Mode.

Manual verification remaining: visual layout and mouse/keyboard operation of the Skills panel at the tester's display resolution. The automated checks cover the saved UI wiring and authoritative gameplay behavior.
