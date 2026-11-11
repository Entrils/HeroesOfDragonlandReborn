# Milestone 44 — advanced melee defense

Parry, block, guard break and poise resolve on the existing stats/combat authority. Animator state and animation events never authorize defense, damage, stamina payments or interruption. The existing M42 moves and M43 input-buffer/cancel rules remain in use.

## Components and data

- `CombatDefense`: existing block costs/chip damage, guard exhaustion, stagger timers, protection interval and cancellation coordination. Existing block costs, stagger durations and protection settings are preserved. The previous continuous stagger-buildup decay is replaced by the stats-configured poise regeneration delay/rate.
- `ParryDefense`: independent parry attempt, active window, recovery, cooldown, committed facing and success counter. `ParryDefinition` provides reusable settings.
- `CharacterPoise`: current/max poise and delayed regeneration, advanced by the same authoritative clock as defense. No independent frame loop.
- `IMeleeImpactSource`: live player/enemy hit-resolution context and interruption callback, shared by `MeleeAttack` and `EnemyMeleeAttack`.

`CharacterStatsConfig.poise` replaces the serialized `staggerResistance` name using `FormerlySerializedAs`, preserving existing values. `StaggerResistance` remains a compatibility getter. Stats assets now expose maximum poise, regeneration delay and regeneration per second. Missing stats data uses poise 50, delay 2 seconds and regeneration 20/second. Invalid numeric stats fall back safely.

M42 move/profile and enemy `staggerPower` values now mean **poise damage**. Their serialized field names and compatibility getters are retained so existing authored moves, enemy data and tools keep their values; `PoiseDamage` exposes the current terminology. Weapon moves retain per-step poise damage, including their existing heavy values. Unblocked applied melee hits consume poise; blocked/parried/invalid/invulnerable hits do not. Existing armor and HP damage rules are unchanged.

## Parry

Press **C**. F remains chest interaction. Offline input and network input use the same `ParryDefense.TryStart` acceptance rules. Multiplayer transmits a monotonic parry press counter alongside the M43 attack counters; it does not send a claimed success, window timestamp or target. Held/replayed input cannot restart the attempt, and rejected/dead/paused/suppressed input is consumed rather than deferred.

Default asset: `Assets/Game/Data/DefaultParry.asset`.

| Setting | Default | Meaning |
| --- | --- | --- |
| Startup | 0.04 s | No parry protection yet |
| Active window | 0.18 s | Accepted elapsed interval `[startup, startup + window)` |
| Recovery | 0.35 s | Committed recovery after the window, including after a successful parry |
| Cooldown | 0.8 s | Measured from attempt start; must cover the entire attempt |
| Stamina cost | 15 | Paid once when the server accepts the attempt |
| Frontal arc | 120° | Centered on accepted, committed facing |
| Attacker stagger | 0.65 s | Subject to the attacker's existing stagger protection |
| Movement multiplier | 0.3 | Uses the existing authoritative movement/collision motor |

A parry can start from idle or guard, but cannot cancel an attack, dodge or stagger. Starting from guard releases guard before paying parry stamina. During startup/window/recovery, attacks, skills, block and dodge are prohibited. Network equipment changes are also rejected by the existing equipment action policy. Facing remains committed until recovery ends. Cooldown alone does not prohibit ordinary movement/attacks after recovery.

Damage resolution order is: alive/team/damage-policy validation → stats authority and finite damage → invulnerability → live melee context and frontal parry → existing block → HP damage → poise damage/events. A bare source identity or busy windup cannot fabricate a successful parry. Geometry and obstruction must already have accepted the hit, and both existing network damage policies and stats authority must permit it. There is no client-side rewind or animation-based extension of the window.

Success prevents this hit's HP/poise damage and consumes the window immediately, allowing at most one success per attempt. It cancels the attacker's committed swing and buffered continuation. Player overlap loops and enemy area-attack loops stop resolving further victims after cancellation. Recovery and cooldown remain; a second incoming attack can still hit the defender after the successful window is consumed. A valid hit outside the active window or outside the frontal cone is resolved normally.

The attacker receives a configurable stagger if its protection timer permits. An attacker already protected from stagger still loses the parried swing, but the parry cannot renew or extend its stun/protection timers. This preserves the successful interruption without creating an indefinite stun loop.

## Block and guard break

Existing block reduction, stamina-per-damage, upkeep cost and movement behavior remain configurable on `CombatDefense`. The existing block coverage is preserved; only parry adds a frontal-cone rule.

If stamina reaches zero while blocking, guard drops, `GuardBreakSequence` increments once and the configured `guardBreakDuration` (default 1 second) is applied, subject to stagger protection. Both impact/upkeep costs and other authoritative stamina spending trigger the same exhaustion handler. The hit which exhausts guard retains the previous block chip-damage behavior; later hits are unblocked.

Guard must be released before it can be raised again. Restoring stamina while continuing to hold block does not bypass this latch. Exhaustion during stagger protection still drops guard and requires release, but cannot extend the protected character's stagger.

## Poise and stagger-lock prevention

Poise starts full. Each valid unblocked melee hit subtracts the accepted move's poise damage and restarts the configured regeneration delay. Partial poise remains unchanged during that delay, then regenerates at the configured rate up to maximum. A tick crossing the delay boundary regenerates only for the portion after the delay.

At zero poise, an allowed poise break applies the existing configurable stagger duration (default 0.45 seconds), interrupts committed attacks/buffers/dodge/parry/navigation and restores poise to full. This break reset is intentional: the character does not emerge from a stagger with an empty pool that another tiny hit can immediately break.

Each accepted stagger reserves its entire duration **plus** the configurable post-stagger protection interval (default 1 second, minimum 0.1). Hits cannot extend an active stagger; poise damage is ignored during protection, preventing hidden buildup for an immediate rebreak. Guard breaks and parry retaliation use the same protection timer. After the stagger itself ends, the protected character can move, dodge and act. Protection does **not** prevent HP damage or make the character invulnerable.

Death/respawn/disable and existing match reset paths clear defense/parry state. Spawn/resource resets restore poise. Regeneration, windows and immunity use simulation time; pause/no-authority does not advance a competing local defense clock.

## Multiplayer and presentation

`NetworkPlayerCombat` publishes accepted parry attempt/start tick, busy/active flags, success sequence, guard-break/stagger sequences/reason and poise/regen/stagger/protection values. Existing network enemies also publish poise and stagger timing snapshots. Clients use snapshots for presentation and debugging and cannot mutate live parry/poise/stagger. Other stat-authority actors such as kings have authoritative poise; inspect their server instance for detailed defense values if no dedicated public defense snapshot exists.

`NetworkCharacterVisual` reuses the M41 upper-body Block pose during parry startup/window/recovery. The attacker uses the existing Stagger state. These are placeholder presentation choices; no new Animator graph or animation-authoritative hit window was introduced. Bespoke parry clips/VFX can replace this presentation independently.

Matching M44 host/client builds and content are required: network input and public defense snapshot layouts changed. Players, enemies and other saved combatants receive poise components; all four network player prefabs receive valid parry references through Unity Editor tooling. Existing metadata and art assets are preserved.

## Debugging tools

Open **Heroes of Dragonland → Combat → Timing Debugger**, then select a gameplay actor or its visual child. The window shows block costs, configured stagger/guard-break/protection duration, live poise and regen countdown, parry active interval/recovery/cooldown, attack hit-window/swing/recovery values, accepted attack identity/tick and buffer/dodge-cancel eligibility. Runtime timelines are read-only. Replica inspection reads public snapshots and labels content previews; it does not run local defense simulation.

Enable **Trace selected events to Console** to log attack starts/ends, parry attempts/successes and interruptions with simulation tick/time values. Select `DefaultParry.asset`, a stats config or a combatant's `CombatDefense` in the Inspector to tune their normal authoring fields. No debug button bypasses gameplay authority or directly grants a hit/parry.

Run **Heroes of Dragonland → Combat → Build Advanced Defense** through Unity MCP in Edit Mode to create/link missing data/components, migrate stats serialization, save prefabs/scenes/assets and rebuild Fusion's prefab table. Existing assigned parry assets and block configuration remain intact.

## Verification

`AdvancedDefenseValidation.ValidateAssets()` checks the shared parry data, all four player references and saved poise combatants. `Start()` in Play Mode uses isolated actual melee overlap/hit-window callbacks to check successful interruption, one success per attempt, startup/end/rear misses, cost/cooldown, attack-cancel denial, block chip/impact/upkeep, guard-break latch/duration, partial-tick poise regeneration, exact threshold, no active stun extension, free control intervals under sustained poise damage, protected-attacker parry behavior, fabricated-context rejection and team-policy precedence. Existing M42 geometry/clip/cost and M43 combo fixtures provide regression coverage.

Launch matching development host/client players with `-hodrDefenseCheck`, the same `-hodrRoom` and normal `-hodrMode`; host also uses `-hodrBots 4`. The opt-in network fixture equips real swords through normal server inventory/equipment and supplies ordinary attack/parry/block input. It tests parries in both directions, remote guard pose/attacker interruption, guard break, poise break/regen and direct replica-mutation rejection. Stage setup resets test actors and heals HP; autonomous damage sources are disabled to isolate timing. Attack/parry stamina still pays through normal gameplay. Both processes must report `[DefenseNetwork] PASS`. The fixture is absent in normal sessions and its implementation is excluded from release builds.

## Files

Verified on 2026-11-11: the isolated M44 Play Mode fixture passed, as did the M42 weapon/hit-window/clip regression and M43 finite-chain/input-buffer regression. Development player build succeeded with zero errors. Both real processes reported `[DefenseNetwork] PASS` in `Builds/DefenseAcceptance-2bceba26/host.log` and `client.log`. The final test uses an ordinary parry press reacting to the accepted incoming remote strike in one direction, avoiding an assumption of symmetric input transit times; gameplay window/authority rules were not changed. Asset validation covers all four player prefabs and 18 saved poise combatants. Timing Debugger was opened successfully; assets were saved and the active scene remained clean in Edit Mode. Existing package/deprecation warnings remain. Bespoke parry art, network-latency feel and final timing/balance still require director playtesting.

New runtime: `Combat/ParryDefinition.cs`, `ParryDefense.cs`, `CharacterPoise.cs`, `IMeleeImpactSource.cs` and opt-in `AdvancedDefenseNetworkProbe.cs`. New Editor tools: `Combat/Editor/AdvancedDefenseSetup.cs`, `AdvancedDefenseValidation.cs`, `CombatTimingDebugWindow.cs`. New data: `Data/DefaultParry.asset` and Unity metadata.

Updated: `CombatDefense`, `DamageReceiver`, player/enemy melee hit resolvers, `NetworkPlayerCombat`, `NetworkEnemy`, stats/config, weapon/profile poise aliases, input capture/counters, player facing/control and input suppression, remote character guard presentation and control help. Existing defense prefabs now contain poise, player prefabs also contain parry; stats assets retain migrated maximum-poise values and gain regeneration settings. No assets deleted; no automatic commit.
