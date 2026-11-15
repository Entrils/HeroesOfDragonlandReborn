# Milestone 45 — Initial magic

The battlefield and fortress player prefabs expose three spells through **K → Skills → Learn / Use**. Unlocks consume the existing session skill points (one at session start, more from levels). Existing Power Strike, Guard and Melee Training retain slots 0–2; magic occupies slots 3–5. New spells require level 1 and one point each. No permanent unlocks or separate magic progression currency were added.

| Spell | Mana | Cooldown | Cast | Range | Effect |
| --- | ---: | ---: | ---: | ---: | --- |
| Fire Projectile | 20 | 4s | 0.6s | 20m | Straight projectile, speed 15m/s, radius 0.2m, 30 damage before existing defenses |
| Short-range Healing Spell | 25 | 7s | 1s | 5m | Restore 35 HP to nearest injured ally in a forward 120° cone, falling back to self |
| Defensive Magic Shield | 25 | 10s | 0.4s | Self | Absorb 40 damage for up to 6 seconds |

Values are editable ScriptableObjects under `Assets/Game/Data/Abilities/`. Each spell has an `AbilityDefinition` plus a `SkillDefinition` unlock referencing it. Mana/cooldown in the skill UI come from the ability asset, avoiding duplicated tuning values.

## Cast and effect rules

- Accepted casts pay mana once and begin cooldown immediately. Interrupted casts consume both. Invalid/unlearned/stale-session/insufficient-mana/busy requests pay nothing.
- Defaults interrupt on movement beyond 0.1m from cast origin, actual health loss, block/parry/stagger, and dodge. Each category is configurable using interruption flags. Death, disable, session replacement, and leaving active network gameplay always cancel. Ordinary melee and other skill activations cannot start while casting. Movement/defense/dodge remain available as deliberate cancel actions.
- Fire commits server facing when casting begins. Swept sphere queries prevent tunneling at ordinary spawn positions, ignore the caster and allies, stop at solid geometry, and hit once. No homing, client hit reports, or area damage. Death and session changes clear outstanding projectiles.
- Healing selects its recipient on the server at acceptance and validates the same recipient again during casting. It requires a living self/ally in the same runner, range, and line of sight. It does not revive or switch targets after an interruption.
- Shields absorb damage after existing equipment mitigation, before HP removal. Overflow reaches HP; full absorption causes no health-loss interrupt. Capacity depletion, expiry, death, disable, and session replacement clear protection. Shields replace rather than stack.
- Client replicas cannot apply damage, heal, pay mana, cast, or modify cooldowns. Existing damage policies explicitly authorize magic only while the admitted server caster resolves an actual projectile hit; this includes enemies, hostile fortress guards, players, and hostile king objectives.

## Components and presentation

- `AbilityDefinition`: shared spell configuration, target mode, interruption flags, animation trigger, and cast/release/impact prefab hooks.
- `AbilityCaster`: cast state, target validation, projectiles, healing, shield capacity, and presentation events. Learning and cooldowns remain in `SessionSkills`.
- `NetworkAbilities`: server simulation, replicated cast/shield state, and server-to-client presentation cues. Uses the existing `NetworkSessionSkills` owning-player request and session validation path.
- `AbilityPresentation`: optional animator/VFX consumption, a basic moving fire streak, and a cyan shield ring. These visuals have no colliders or gameplay authority. `CastMagic` triggers only if an animator actually provides that trigger. Dedicated spell clips and authored particle prefabs remain art hooks, with no new animation assets fabricated.
- `MagicSetup`: idempotent Unity Editor installer that preserves existing skill slots and edits/saves prefabs through Unity APIs. The earlier session-skills installer also preserves the six-slot layout and invokes magic setup.

## Validation

`Heroes of Dragonland → Magic → Validate In Play Mode` exercises 66 asset/gameplay assertions: valid prefab references and definitions, learning and XP, stale/unlearned/duplicate requests, payment, cast time, cooldown, projectile collision and friendly exclusion, obstruction, movement/damage/death interruption, healing targets/range/line of sight, shield depletion/overflow/expiry, presentation events, and complete session reset.

`MagicNetworkProbe` is opt-in development code installed only with `-hodrMagicCheck`. Launch the development player as host with `-hodrBots 4`, wait for host creation, then join a client with the same `-hodrRoom`. Both use `-hodrMagicCheck`. The fixture uses normal owning-player activation RPCs and checks replicated casts, shield absorption, fire damage in both directions, healing, and direct replica mutation rejection. It disables autonomous attack fixtures and supplies XP only in this explicit acceptance mode.

Completed verification: all 66 Play Mode assertions passed. A Windows development build succeeded with zero errors. Both real host and client logged `[MagicNetwork] PASS` in `Builds/MagicAcceptance-f479df2b/host-check.log` and `client-check.log`; acceptance players were stopped afterward. Final source compilation and asset saves succeeded. The build reported existing package/obsolete-API/compute-shader warnings and a missing optional Pipeline runtime configuration; no compiler errors. Unity-generated prefab blank-name whitespace is retained as emitted by the Editor.

Manual review: check the fire streak and shield ring in a visible match, tune the three ability assets for game feel, and connect dedicated spell animation clips/particle prefabs when available. The current generic animator trigger and VFX references are deliberately optional.

No scene content was added or removed. Modified systems: skills/definition/UI and their installer, melee cast exclusion, shared HP shield absorption, player/enemy/king magic damage authorization, and the battlefield/fortress player prefabs. New files: the `Abilities` runtime/editor folder, six spell/unlock assets and their Unity metadata, and this document. No existing assets were deleted.
