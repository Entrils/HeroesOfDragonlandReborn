# Milestone 42 — weapon movesets

Milestone 43 replaces the original wrapping/held-repeat sequence policy below with finite buffered transitions and explicit recovery/cancel rules. See [AttackChaining.md](AttackChaining.md) for the current runtime contract. M42 move profiles, costs, geometry and presentation mapping remain in use.

Milestone 44 interprets the existing per-move stagger power as poise damage; serialized move values are preserved. See [AdvancedMeleeDefense.md](AdvancedMeleeDefense.md) for parry, block, guard break and delayed poise regeneration.

`WeaponDefinition.Moveset` selects reusable `WeaponMoveset` data. Sword, Greatsword, Spear and Staff assets are linked under `Assets/Game/Data/Weapons/`. Runtime combat never branches on `WeaponArchetype`; the archetype is an authoring label. Existing weapons without a moveset retain the previous attack-set behavior, and unarmed actors retain their serialized melee fallback.

## Authoring

Movesets live in `Assets/Game/Data/Weapons/Movesets/`. Each contains 1–16 ordered light moves, one heavy move, a fallback `WeaponAttackAnimationSet` and a combo reset delay. The supplied Sword/Spear/Staff sequences have three steps; Greatsword has two. Each move defines:

| Field | Meaning |
| --- | --- |
| Profile | Reusable `MeleeAttackDefinition`: swing duration, recovery, normalized active window and hitbox width/height |
| Animation | Optional Humanoid clip; falls back to the moveset animation set, then the shared character clip |
| Attack speed | Positive timing divisor: swing, recovery and hit-window times are divided by this value |
| Stamina cost | Absolute cost paid once when the server accepts this move |
| Range | Absolute forward reach; hitbox extends from origin to this distance |
| Movement multiplier | Fraction of normal player movement permitted during swing and recovery, applied by the existing swept server motor |
| Stagger power | Absolute stagger buildup applied by the existing damage/defense pipeline |
| Damage multiplier | Multiplies weapon base damage after existing equipment/skill modifiers |

For moveset attacks, heavy timing/cost/damage/stagger multipliers on the legacy profile are **not** applied again. Heavy is an independent move with its own explicit values. `WeaponDefinition` still owns base damage, requirements and weapon art; its speed/cost/range getters expose the first light move for existing UI previews. Legacy serialized speed/cost/range values serve weapons without movesets.

The supplied profiles preserve each archetype's existing base reach, speed and cost. Later light steps have modestly longer timing and increased damage/stagger; heavy moves retain the previous heavy balance and reduce permitted movement. These are starting balance values, editable per move. Staff currently uses melee attacks; spellcasting/projectiles are separate gameplay systems.

Clips are the existing placeholder Humanoid LightAttack, alternate Attack and HeavyAttack art. This milestone provides weapon/step-specific animation assignment and timing, not four newly authored art sets. Replace any move's clip in its moveset without extending the Animator graph. A null move clip uses the moveset's animation set. Clips remain in-place, presentation-only and subject to the M41 safe-event rules.

## Accepted attack lifecycle

`MeleeAttack` resolves current equipped data only when accepting an attack, then commits the move/profile, weapon, timing, damage, geometry and facing. Busy requests, insufficient stamina, stat requirements, dodge, death and stagger use the existing legality/authority gates. Failed requests do not advance the sequence or pay a partial cost.

The next successful light advances/wraps the sequence only after full recovery. Combo timeout counts simulation seconds while idle after recovery; it does not expire during a slow swing. Heavy, skill attacks, cancellation, death, component disable and main-hand equipment changes reset the combo. There is no client combo counter, input buffering or animation-event-driven attack chaining. Held attack retains the existing repeat-after-recovery behavior.

Equipment is already server validated and prohibited while an attack/dodge is busy. Switching weapons automatically selects the new configuration for the next attack. Unarmed fallback and existing skills still use `MeleeAttack`; skill strikes select the first light and reset chaining.

## Multiplayer and presentation

`NetworkPlayerCombat` publishes the accepted `AttackWeaponId` (catalog item GUID, not archetype), `AttackMoveIndex`, heavy flag, sequence, start tick and swing duration. Both ordinary and skill attacks publish this identity. Only admitted server players/bots can accept attacks. Clients send ordinary attack/equip intentions; no RPC accepts a client moveset, move index, hit window, cost or damage value.

`NetworkCharacterVisual` resolves the accepted catalog item during a swing and uses the equipped item at rest. This avoids choosing a remote attack clip from a differently timed equipment snapshot. Each visual owns an `AnimatorOverrideController` over the unchanged M41 controller, overriding only LightAttack/HeavyAttack. Overrides change only when clips change and are released with the visual. ActionSpeed uses the chosen clip's length and the accepted swing duration; late observers retain M41's accepted phase offset. No new Animator parameters, graphs, hitboxes or animation-authoritative gameplay were added.

Damage and stagger still resolve on server ticks through existing overlap/obstruction/team/defense logic. The active window is profile time divided by move speed; a long tick crossing the window still samples once, and a target can be hit only once per accepted swing. Animation events never authorize hits, resources, movement or action completion. All peers must use matching M42 builds/content because Fusion's combat snapshot layout changed.

## Tools and verification

In Edit Mode, run **Heroes of Dragonland → Combat → Build Weapon Movesets** through Unity MCP. It creates missing movesets/profiles, links the four weapons and fills missing fallback clips; existing authored movesets/profiles are preserved. Assets are saved through Unity APIs; existing assets and metadata are retained.

`WeaponMovesetValidation.ValidateAssets()` checks four mappings, complete sequences, Humanoid clips and the M41 architecture. `Start()` in Play Mode creates temporary isolated fixtures and checks all four equips; ordered/wrapping/reset sequences; rejected stamina/busy requests; exact costs, timing, movement and stagger values; independent heavies; hit-window timing, target deduplication and range; actual per-step/weapon Animator overrides; and default clip restoration on unequip. Fixtures are removed afterward.

An explicitly launched development host/client pair can use `-hodrMovesetCheck` (host also `-hodrBots 4`) with matching `-hodrRoom`. This opt-in probe arranges separated server actors, grants test progression/innate attributes and replenishes test resources, equips real catalog items through existing inventory/equipment authority, and supplies ordinary light/heavy input. Both processes must observe `[MovesetNetwork] PASS` for every real remote weapon/step/heavy, correct accepted durations and clip overrides. The client also verifies direct replica combat/equipment mutations are rejected. The fixture is not installed in ordinary games, and its implementation is excluded from release builds.

Verified on 2026-11-11: asset validation and the Play Mode fixture passed; development player build succeeded with zero errors; both real host/client processes reported `[MovesetNetwork] PASS` in `Builds/MovesetAcceptance-691bf8bb/host.log` and `client-retry.log`. The first client launch preceded room creation and was retried after the host connected. The final Unity Console error query returned zero entries; Editor was restored to Edit Mode with a clean, saved active scene. Existing Unity/package deprecation and shader warnings remain. Weapon animation art and final balance still require director playtesting.

## Files

New: `Combat/WeaponMoveset.cs`, `Art/Editor/WeaponMovesetSetup.cs`, `Art/Editor/WeaponMovesetValidation.cs`, `Presentation/WeaponMovesetNetworkProbe.cs`, four movesets and fifteen per-move profiles under `Data/Weapons/Movesets/`, plus Unity metadata and this document.

Updated: `MeleeAttack`, `NetworkPlayerCombat`, `WeaponDefinition`, `CharacterVisual`, `NetworkCharacterVisual`, four weapon assets and four fallback animation-set assets. No assets deleted. Existing equipment policies, authoritative motor, damage/defense systems and Animator graph are reused.
