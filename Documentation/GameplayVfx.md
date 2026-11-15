# Milestone 59 — Combat and Gameplay VFX

## Status and visual language

The initial language is **compact elemental glyphs + sparse bursts + separate audience markers**. Ivory contact sparks, a steel shield mark, a gold parry star and a split guard symbol distinguish outcomes by shape. Fire keeps a warm core; healing uses a cross; shield uses an open ward; Mana uses a narrow rune. Gameplay rewards use brass/bone icons rather than attack-shaped ground rings.

This follows [ArtDirection.md](ArtDirection.md), the shared biome palettes and the existing character/equipment pipeline. No exposure flash, full-screen tint, camera shake, smoke wall, opaque shield sphere or decorative damage circle is added. Existing boss attack telegraphs remain authoritative presentation and are **outside this cosmetic pool**.

**Delivered on this computer:** the 17-effect authoring manifest, reusable Unity prefab/catalog builder, bounded pool, outcome/event adapters, a static SVG design sheet and tests. The pure C# ownership/budget/sequence policy compiled with the installed Windows .NET Framework compiler and **passed 24 checks**. The design-sheet generator ran successfully and its SVG parsed as valid XML.

**Pending in Unity:** compilation/weaving, generation of actual `.prefab`/`.mat`/catalog assets, prefab/scene installation and saving, Console checks, native pool Play validation, real remote delivery, pose/overlap readability and six-player profiling. No Unity Editor/MCP is available on this machine; the gameplay scene and serialized prefabs are unchanged here. The SVG is a design reference, not a Unity render, particle simulation or proof of combat readability. M59 is not complete until that handoff passes.

AGENTS.md and the relevant combat, ability, camp, progression, match and presentation systems were inspected. DESIGN.md remains absent. The repeated milestone text is treated as one implementation with one set of effects.

## Initial effects and confirmed triggers

| Effect | Shape / base color | Confirmed source | Extra network traffic |
| --- | --- | --- | --- |
| Melee hit | Small ivory spark | New DamageReceiver accepted-melee outcome | One compact server outcome RPC |
| Blocked hit | Steel shield notch | Accepted block, including zero damage | Same outcome relay; never inferred from health loss |
| Parry | Gold four-point star | Successful `ParryDefense.TryResolve` outcome | Same outcome relay; no effect for an unsuccessful attempt |
| Guard break | Broken shield | Existing player GuardBreakSequence; NPC server defense counter | None for players; NPC counter change uses outcome RPC |
| Heavy attack | Short warm slash | Existing accepted AttackSequence/HeavyAttack/AttackBusy and render age | None |
| Player death | Small ash strokes/burst | Existing replicated Dead transition | None |
| Fire projectile | Warm bolt and two sparse core-radius wire rings | Existing AbilityCaster Released/Impact cues | Reuses existing NetworkAbilities cue RPCs |
| Healing | Muted green cross | Existing ability Impact at the actual healing target position | Reuses existing cue RPC |
| Magic shield | Open cyan ward, no filled bubble | Existing replicated ShieldRemaining | None added; supports late observation of an active shield |
| Mana cast | Narrow blue rune near casting hand | Existing CastRemaining and interruption/release cues | None added |
| Level up | Upward ivory/brass chevron | Existing public progression Level/SessionId/Revision | None |
| Item pickup | Small brass diamond, local owner only | Accepted existing chest OpenResolved response | None added; no inference from inventory quantity changes |
| Chest opening | Brass open-lid glyph | Existing IsOpen false→true | None |
| Camp cleared | Bone three-notch confirmation | Existing CompletionCount increase | None |
| Boss death | Ancient crown | Replicated Dead transition on a boss actor | None |
| King death | Split royal crown | Replicated Dead transition on NetworkKingObjective | None |
| Victory | Small gold laurel at the winning local avatar | Existing match FinishCount/MatchEnded/Winner | None; existing match HUD remains the primary result display |

Ordinary NPC deaths reuse the small actor-death burst; boss and King deaths get their own shapes. No additional enemy ability, spell, damage, reward, death or victory rule is implemented. Mana cast is a cosmetic casting indicator, not an extra ability or resource operation.

Pickup feedback is connected to the project's current confirmed chest-loot flow. It does not fire on equip, unequip, inventory snapshot application, a rejected chest request or a mere interaction-button press. Future non-chest pickups should call the same local VfxPool effect only after their own accepted pickup event.

## Friendly, enemy and neutral readability

- **Friendly:** a smooth/open cyan-green marker above the effect body.
- **Hostile/opponent:** a jagged orange marker with outward teeth.
- **Neutral:** a bone diamond.

The elemental body remains recognizable in every relation. Friendly fire still looks like fire, but its audience marker differs from an enemy projectile. Source allegiance is resolved against the current Runner's local player, not a global static team. Unknown damaging sources are treated as hostile; non-damaging unknown sources are neutral. The accepted melee relay also carries the attacker's friendly-fire flag so an allied damaging source does not receive a reassuring friendly marker when that rule permits harm.

Status/support markers identify affiliation; they are not damage-area boundaries. An opponent's healing cross is still visibly healing, not a red ground attack. Team A/Team B identity remains on the character/heraldry/HUD, independent of VFX. Do not paint every combat particle blue/red or rely on slight hue differences.

Glyphs are camera-facing where appropriate, with thin dark backing and a limited bright core. Projectile body rings remain oriented in 3D; their scale is derived from the actual AbilityDefinition projectile radius using the prefab's authored reference radius. No collider or damage volume is created by the VFX.

## Keeping 3v3 fights readable

The initial factory uses one non-looping ParticleSystem per effect, at most **16 particles**, a 0.28s particle lifetime, approximately 0.035m size and 0.2 alpha. A sparse contour glyph can outlive the initial burst. Default one-shot lifetimes range from 0.24 to 1.5 seconds; active shield/cast/projectile leases use the confirmed/current timing with a 30s safety ceiling.

Particles have no collision, lighting, trails, shadows or post-processing. Billboard particle screen size is capped at 4%. Native materials share URP Particles/Unlit with alpha blending and **ZWrite off**. Particle queue 2950 and glyph queue 2960 are below the existing default-transparent warning lines (3000). Effects still respect scene depth; no through-wall/X-ray rendering is introduced. Queue choice alone is not proof of readability: verify actual overlap, depth and camera angles in URP.

Place contact/status glyphs around the body/casting hand/chest, not as decorative filled footprints on combat ground. Camp/objective feedback is raised above the ground. Do not expand a hit burst into an apparent weapon reach/area. Body effects, cosmetic death glyphs and audience markers must never erase the boss cleave/sweep/stomp perimeter or a player's target/health information.

No boss-telegraph code, danger geometry, hit timing, cast timing, ability range, shield duration or target selection rule is changed. Existing attack warnings and HUD remain readable even when cosmetic effects are dropped.

## Pooling, priorities and resource ownership

`VfxPool` is scoped to the scene's MultiplayerBootstrap and found through the active Runner. There is no process-wide singleton mixing separate sessions. The baseline is **32 active leases**, configurable down to 16. Per-kind caps are 1–8; current manifest maxima total **85 retained instances** if every kind reaches its individual historical peak. The pool does not keep allocating indefinitely after warm-up.

Priority order:

| Priority | Use | Saturation behavior |
| --- | --- | --- |
| 0 | Level up, pickup, chest/camp celebration, victory flourish | Dropped first; gameplay/HUD stays intact |
| 1 | Ordinary hit/block/heavy/death feedback | Can replace lower-priority cosmetics |
| 2 | Parry, guard break, healing/casting, boss/King death | Can replace lower-priority feedback/cosmetics |
| 3 | Active fire projectile and magic shield | Protected from lower-priority eviction |

The oldest lowest-priority eligible lease is retired first. Equal/higher-priority leases are not overwritten, and per-kind caps remain enforced. In extreme saturation a request can still be dropped; VFX must never be the only source of hazard/health/objective information. Current six-player defaults fit up to eight projectile and six shield instances within the 32-slot budget. Record `Dropped` locally during stress review; it is not replicated.

Each lease includes a slot generation. An expired/interrupted handle cannot move or stop a newer effect that reused the same pool slot/Unity object. Return immediately clears particles and deactivates the root. Pool disable invalidates all active leases; follow-target disappearance/hiding returns persistent effects; externally disabled instances return themselves. Destroyed idle objects are discarded/replaced within the creation cap.

Persistent effects stay parented to the pool, following a target transform without becoming children of a despawned network avatar. Actor disable, death, round change and reconnect suspension stop shield/cast/projectile visuals. Resume reseeds transition baselines so old deaths, guard breaks and level-ups are not replayed. A legitimately active replicated shield/cast can be reconstructed after observation resumes.

The pool does not render/allocate native effect instances in batch mode by default. Dedicated/headless authority still sends confirmed outcome cues to real clients. The isolated Play validation explicitly enables batch rendering so native lifecycle behavior can be checked in an automated Editor session.

## Networking and authoritative outcomes

`DamageReceiver` gains an isolated `ImpactResolved` observation event. Existing policy, invulnerability, parry, stamina, defense, damage and poise execution order is preserved. A successful parry reports Parried before returning zero damage. An accepted blocked impact reports Blocked even when its damage is zero. An unblocked positive melee hit reports MeleeHit. Generic fire damage does not also pretend to be a melee hit; fire Impact already supplies its effect.

Event-listener exceptions are caught so a VFX observer cannot change the returned damage result. No client hit report, client-supplied damage or new damage authority is accepted.

`NetworkImpactVfx` subscribes only on the server/state authority and broadcasts a small confirmed outcome RPC in forward simulation. Its per-object sequence suppresses duplicate/out-of-order delivery. NPC guard breaks observe the actual server defense counter after the tick, including a guard drop during recovery immunity; player guard breaks reuse their existing replicated counter instead. Particle state, particle positions and random seeds are not networked.

Everything else reuses existing public state/events. `VfxSequence` seeds its first sample without historical playback, ignores repeated/stale counters and is explicitly reset on round/lifecycle changes. Coalesced snapshot counters emit one bounded confirmation burst rather than replaying an entire backlog. The outcome RPC is transient: joining after an old hit does not reconstruct that hit. Live current shield/cast state is handled separately.

The legacy AbilityPresentation component remains enabled for its optional Animator trigger hook, but setup enables `externalVfx` so it does not also instantiate old particle prefabs, fire lines or shield rings. This avoids duplicate magic visuals without removing existing animation triggers.

## Fire projectile limitations to verify

The current ability protocol already broadcasts release origin/direction and impact position; it does not provide a stable projectile ID or continuously replicated projectile position. The visual moves locally using the configured speed and stops on an authoritative impact cue, expiry, actor death or suspension. Heal uses the server's impact target position, not an assumed caster/self position.

On fire impact, only the **nearest matching local flight** is retired, rather than deleting every projectile from that caster as the legacy presenter did. This is still a matching approximation. Current base fire data is 20m range, 15m/s speed, 0.2m radius and 4s cooldown, normally leaving one flight per caster at a time. If future modifiers/data allow overlapping flights from one caster, exact association under latency may require a gameplay projectile ID. No unsupported claim of perfect projectile interpolation/identity is made here.

Impact spark location is the defender's visual chest or a body-height fallback, because the existing damage API does not report an exact contact point. It does not imply a precision hit-location system. Confirm visual travel, wall impacts, simultaneous casts, range expiry and delayed cues with real clients before production approval.

## Source, design sheet and Unity setup

Authoring data: `ArtSource/Vfx/VfxLanguage.json`. The same 17 entries supply effect identities, geometry shape keys, colors, lifetimes, priorities and burst/cap values for the Unity factory and static design sheet. `Documentation/VfxLanguage.svg` is a readable **design-only** reference; it is not a runtime screenshot. The fire glyph is projected for the sheet; Unity adds actual 3D core-radius rings.

In Unity 6000.6.0f1, through MCP:

1. Import the new scripts/metadata, inspect Console and fix compilation/weaving errors first. Existing `.meta` files and gameplay assets are preserved. Ensure the existing shared M56 soft-mote texture imports; it is reused rather than generating another redundant particle texture.
2. Invoke `HeroesOfDragonland.Art.Editor.GameplayVfxSetup.Build()`. It creates 17 reusable inactive PooledVfx prefabs, two shared unlit materials and GameplayVfxCatalog under `Assets/Art/Vfx/`, retaining already authored versions on rerun. It adds appropriate presentation/outcome adapters to network actor/camp/chest/match prefabs under Assets/Game/Data and sets the legacy ability presenter to external VFX mode.
3. The new NetworkImpactVfx is an additional NetworkBehaviour/RPC component, although it adds no Networked particle fields. Rebuild/bake/reimport Fusion prefab data and build **both peers from matching content**. The setup rebuilds the prefab table, but verify the actual baked scene/prefab behavior list in Unity before multiplayer testing; old players/builds are not claimed compatible with the new RPC layout.
4. With the intended battlefield scene active, invoke `GameplayVfxSetup.InstallLoadedScene()`. It creates/assigns the bootstrap-scoped pool and adds adapters to scene NetworkObjects such as the camps. It does not move scene objects, change camp data or silently save the scene. Inspect Hierarchy/references, save modified scenes/assets through MCP and repeat scene setup for any other test scene that needs VFX.
5. Run `GameplayVfxSetup.Validate()` and inspect Console. It checks the complete catalog, bounded systems, no visual collider/NetworkObject, safe material queue/depth and per-kind caps. Runtime catalog validation also rejects arbitrary gameplay/scripts, nested owners, physics/light modules and unsafe particle lifecycles before the pool is enabled.

The asset factory uses native LineRenderer/ParticleSystem geometry; no Blender import, custom shader, second art pipeline or networked particle prefab is necessary. The source/manifest and setup are ready, but none of these actual Unity outputs have been created on this machine.

## Validation record and required tests

**Passed here:** direct compilation/execution of `VfxBudget.cs + Tests/Standalone/VfxPolicyValidation.cs` with the Windows .NET Framework compiler: 24 checks covering per-kind/global limits, priority eviction, rejected requests, stale/double release, ownership conservation, late-join seeding, stale/repeated sequences, coalescing and explicit lifecycle reset. This uses the same pure policy source as the runtime pool; no Unity/network implementation is mocked or claimed exercised.

The reproducible Windows runner is `Tests/Standalone/RunVfxPolicy.ps1`. The direct compiler/executable commands passed; this runner script itself was not executed because local ExecutionPolicy blocks `.ps1` by default. The separate design-sheet generator was explicitly allowed for a process-scoped execution-policy override, then passed 17-entry budget/schema checks and SVG/XML parsing. No persistent execution-policy setting was changed.

**Prepared, not run:** in Play Mode invoke `GameplayVfxValidation.Start()` through MCP and read `Status`. Its real Unity fixture tests native lifetime/return, stale lease safety after actual instance reuse, hidden follow cancellation, 16-slot saturation, priority replacement, cosmetic dropping, pool disable/reenable, cleared particle buffers and all 17 prefab types. It does not claim to exercise a real attack or network connection.

**Required gameplay/host-client tests:**

- Strike an opponent/NPC; verify one confirmed hit, a zero-damage block, a successful parry (no effect for a failed attempt), stamina/guard break including immunity recovery, and accepted/rejected heavy attacks. Check remote observers, not just the server/owner. Damage, costs, timing and interruptions must match the existing rules.
- Fire, hit an enemy/wall, expire at range, heal an injured ally/self, start/end/break a shield, complete/interrupt a Mana cast, die and disconnect during casting/projectile travel. Check radius, travel/impact association, no stale shields/flights and late observation of an active shield.
- Gain a level, successfully loot a chest, reject duplicate/full-inventory/out-of-range opens, clear/reopen a recurring camp, kill the boss/King, finish/return/restart a match, reconnect and join late. No stale celebration/history should replay; only the winning local team gets the victory flourish.
- Overlap effects from six players and neutral enemies against each biome floor. Run all three existing boss attack shapes while filling the cosmetic pool. Confirm telegraph boundaries, targeting, silhouettes, chest prompts and both team identities remain legible in color/grayscale and near camera walls.
- Measure median/p95 CPU/GPU frame time, active/retained pool counts, Dropped, transparent overdraw and actual network traffic. Confirm no per-particle replication/new cosmetic RPC stream and no headless particle allocation. Performance numbers are currently unmeasured.

Record Console results, prefab/bake/build checks, Play Status, both peers' scenario observations and visual captures here before marking M59 complete. A passed pure policy test or static SVG is not a substitute for these checks.

## Changes

Modified: `Assets/Game/Combat/DamageReceiver.cs` adds accepted-outcome observation only; `Assets/Game/Abilities/AbilityPresentation.cs` preserves animation triggers while allowing external pooled visuals.

New runtime components under `Assets/Game/Presentation/Vfx/`: VfxBudget/VfxSequence, VfxCatalog, VfxPool, PooledVfx/VfxLease, VfxAudience, NetworkImpactVfx, ActorVfxPresentation and WorldVfxPresentation. These separate resource policy, rendering, audience rules and event adapters rather than extending PlayerController or a gameplay manager.

New authoring/validation: GameplayVfxSetup.cs and GameplayVfxValidation.cs under Assets/Game/Art/Editor; ArtSource/Vfx/VfxLanguage.json; Tools/Vfx/Preview-VfxLanguage.ps1; Tests/Standalone/VfxPolicyValidation.cs and RunVfxPolicy.ps1; this document and VfxLanguage.svg.

No particle prefab is a NetworkObject, no collider/gameplay component is spawned by VFX, and no attack/health/loot/layout rule is redesigned. No assets deleted or serialized YAML edited; no commits or pushes. New Unity metadata, generated art assets and scene/prefab adapter assignments remain later MCP integration changes.
