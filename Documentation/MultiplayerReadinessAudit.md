# Multiplayer readiness audit — Heroes of Dragonland Reborn

Audit date: 2026-09-08. Completion review: 2026-09-09. Target: 3v3, maximum six human participants, session-based PvPvE, server-authoritative combat and progression.

## Outcome and evidence limits

**The architecture is a workable local prototype, but it is not ready to expose gameplay methods as RPCs.** Keep the modular components; introduce explicit simulation, command-validation, identity and replication boundaries before networking.

Read [AGENTS.md](../AGENTS.md). **DESIGN.md was not found** in the project; the completion review repeated the project file search including hidden files outside generated directories. This report uses the stated target and checked-out code. Unresolved rules are identified below, not attributed to a missing design document. The missing document limits design reconciliation, but does not prevent completing this source-readiness audit.

Inspected the 67 C# files under Assets/Game, relevant content/prefab/scene configuration, Packages/manifest.json, ProjectVersion.txt, EditorBuildSettings.asset, milestone notes and validation commands. Unity version is **6000.6.0f1**. No gameplay networking package is installed; collab-proxy is version control, not multiplayer netcode.

The working tree contains Milestone 13–15 code, but only SampleScene.unity exists under Assets/Scenes and is enabled in EditorBuildSettings. FortressGuardTest and KingBattlefieldTest builder outputs are absent. Milestone 15 notes leave scene creation and live verification pending. Feature coverage and verified integration are different: this audit cannot certify the latest objectives as playtested. On completion review, Unity MCP was available: read-only hierarchy inspection confirmed SampleScene is open, saved, outside Play Mode and not compiling. Its gameplay components include one player, three target dummies, one test bandit, a loot chest, a camp and progression. No TeamIdentity, FortressGuard, KingObjective or MatchManager was present in that hierarchy. Console inspection returned zero errors and two tooling warnings (Unity AI/MCP); this is not a runtime, build, latency or penetration test.

**This task changes documentation only. No gameplay, scenes, assets, packages or behavior were changed.** Existing working-tree changes belong to earlier tasks.

## Classification and ownership model

These categories describe the **recommended multiplayer destination**, not implemented networking:

| Class | Meaning |
| --- | --- |
| 1. Client-only | Input, camera, UI and cosmetics. Reads approved state; cannot commit gameplay. |
| 2. Server-only | Rules, validation and bookkeeping. Publish results through other systems where needed. |
| 3. Server-authoritative with client presentation | Server commits state; clients render it. Owner prediction is provisional and reconciled. |
| 4. Replicated shared state | Durable facts visible to permitted peers, with the server as sole authoritative writer. This does not mean distributed authority. |

A participant owns an **input association and private views**, not the right to write health, items or progression. The dedicated server owns canonical state for all player avatars, NPCs, objectives and world objects. Library ownership/InputAuthority routes requests; application validation decides whether they are legal.

Use a stable authenticated participant ID distinct from connection ID and entity ID. Reconnection may replace the connection/avatar without replacing participant identity. Bind messages to a server-created match ID/epoch and actor references to entity ID plus incarnation. Unity object pointers, GetInstanceID and array positions are not network identity.

| System | Primary class | Split |
| --- | --- | --- |
| Input/cursor/camera | 1 | Input construction local; processing server-side. |
| Movement/jump | 3 | Owner prediction, server collision, remote interpolation. |
| Lock-on | 1 | Server validates resulting aim/facing intent. |
| Targetable/threat markers | 4 | Eligibility derives from server life/team state. |
| Resources/regen | 3 | Owner details; health visible as required. |
| Damage/invulnerability/policies | 2 | Results replicated separately. |
| Melee | 3 | Server attacks/hits, client presentation. |
| Dodge | 3 | Server displacement/cost/i-frames, owner prediction. |
| Definitions/catalogs | 4 | Immutable shared content/manifest; rule evaluation is class 2. |
| Inventory | 3 | Owner-private snapshots. |
| Equipment | 3 | Private transactions; public equipped appearance. |
| Attributes | 3 | Server computation; owner breakdown. |
| Interaction | 3 | Local prompts, server validation. |
| Loot rolling | 2 | Results published selectively. |
| Chests | 4 | Server open/claim state; private overflow. |
| Enemy AI/navigation | 2 | NPC pose/action representation is class 3. |
| Guards | 2 | Targeting policy; NPC presentation is class 3. |
| Camps | 4 | Spawn/completion/reward decisions are class 2. |
| XP/levels/point allocation | 3 | Server ledger; owner details. |
| Teams/roster | 4 | Server assignment. |
| Kings | 3 | Server objective, public health/defeat. |
| Match/session/result | 4 | Lifecycle and victory decisions are class 2. |
| Visuals/UI | 1 | Read initial state and deduplicate effects. |
| Debug mutation tools | 2 | Development-only trusted server commands. |

“RPC” below means an explicit request/reply where appropriate. Continuous gameplay input should use a ticked input stream, not one reliable RPC per rendered frame. Editor/build tooling is outside runtime ownership classes and is classified separately from gameplay.

## Detailed system audit

### 1. Input and third-person camera — class 1

Files: [PlayerController](../Assets/Game/Player/PlayerController.cs), [ThirdPersonCamera](../Assets/Game/Player/ThirdPersonCamera.cs).

- **Ownership:** only the local human owns an input/view rig. Remote avatars and headless servers need no cursor, camera or InputAction subscriptions.
- **Authority:** the client chooses inputs; the server applies gameplay intent.
- **Replicate:** no camera transform, cursor or raw pointer deltas. Submit normalized movement axes, bounded yaw/aim intent, button edges and sequence/tick.
- **RPCs:** no camera RPC; movement/action input stream, with interaction requests separately.
- **Never trust:** client position, grounded state, deltaTime, focus/cursor flags as proof of legality, or an actor ID other than the sender's assigned avatar.
- **Refactor:** Update directly calls movement/attack/dodge and Awake requires a camera. Split input sampling from simulation. Define neutral input after loss/disconnect. Separate OnDisable gameplay cancellation from local cursor/view cleanup.
- **Cheating:** time acceleration, forged input rate, NaN/infinite vectors, controlling another player.
- **Synchronization:** six avatars consuming the same keyboard, multiple cameras/listeners, missing-camera server errors, button edges lost between render and simulation ticks.

### 2. Movement and jumping — class 3

File: [PlayerMovement](../Assets/Game/Player/PlayerMovement.cs).

- **Ownership:** participant supplies input; server owns pose/motor state. Owner predicts; others interpolate.
- **Authority:** speed, acceleration, gravity, jump eligibility, slopes, collision and dodge override.
- **Replicate:** pose, correction velocity/grounded/mode, teleport/incarnation marker and last processed input sequence. Remotes need reduced pose/locomotion state.
- **RPCs:** input stream, never MoveTo(position) or authoritative client transform updates. Teleports are server commands.
- **Never trust:** position/velocity, grounded/collision flags, jump success, speed, client time step or arbitrary velocityOverride.
- **Refactor:** capture/restore private planarVelocity and verticalSpeed; step once per server tick; smooth a visual child, not collision root. CharacterController.Move/live raycasts are not automatically rollback-capable. Evaluate a prediction-aware KCC against current slopes/jump/dodge behavior. Reject nonfinite input and bound work per tick.
- **Cheating:** speed/fly/noclip, infinite jumps, teleports that also bypass loot reach and attack range.
- **Synchronization:** frame-rate dependence, corrections around slopes/steps, actor collision differences, two systems moving one root. Replaying without restoring motor state is insufficient.

### 3. Lock-on and target markers — classes 1 and 4

Files: [TargetLock](../Assets/Game/Player/TargetLock.cs), Targetable, EnemyThreatTarget.

- **Ownership:** reticle/selection belong to the viewer; server owns target existence/life/team.
- **Authority:** local camera scoring is presentation. Lock selection cannot authorize a hit.
- **Replicate:** entity identity, existence and approved life/team/eligibility. Local lock need not be broadcast; accepted action facing/target can be.
- **RPCs:** none for reticle changes. If lock-on drives simulation-facing assistance, send target ID as a hint and validate it server-side.
- **Never trust:** selected ID as proof of range/LOS/hostility, camera FOV as a security boundary or client life state.
- **Refactor:** network entity lookup instead of Component references; scope FindObjectsByType and physics to the match. Proxy Targetable reads replicated life. Handle respawn incarnations separately from Unity-null checks.
- **Cheating:** hidden-target selection and aim automation. Legal server geometry does not prove a human selected the target manually.
- **Synchronization:** stale targets after despawn/respawn, mismatched visibility/team, absent referenced entities when an action arrives.

### 4. Health, stamina, mana and regeneration — class 3

Files: [CharacterStats](../Assets/Game/Core/CharacterStats.cs), ResourcePool, CharacterStatsConfig.

- **Ownership:** actor resources, written by server.
- **Authority:** damage/heal, death, costs, regen timing, maxima and spawn restoration. Owner may display predicted resource costs.
- **Replicate:** relevant observers receive health/max/life; owner receives stamina/mana/maxima and correction timing. Enemy resource details only if approved UI needs them.
- **RPCs:** no SetHealth, Heal(amount), ResetToFull or TickRegeneration. A player asks to perform an action; server derives its cost.
- **Never trust:** balances, maxima, elapsed regen, healing, immunity or death claims.
- **Refactor:** remove replica regeneration, establish one clock and passive snapshot application that does not call ConfigureForSpawn/ResetToFull or gameplay Died handlers. Preserve capacity-change clamping without healing. Co-version resources with gear/attribute changes.
- **Cheating:** god mode, infinite resources, fast regen and resets used as revives.
- **Synchronization:** peers regenerating independently, Awake overwriting snapshot health, duplicate death effects, current/max values arriving separately. Died precedes lethal HealthChanged; preserve server ordering while separating replica UI callbacks.

### 5. Damage and invulnerability — class 2

Files: [DamageReceiver](../Assets/Game/Combat/DamageReceiver.cs), IDamageable, IDamagePolicy, IInvulnerabilityProvider.

- **Ownership:** server resolves attack context; receiver belongs to victim.
- **Authority:** only validated server simulation invokes damage/policies. Existing team checks alone do not establish authority.
- **Replicate:** resource/life result and optional hit sequence/attacker/effect data; not policy execution.
- **RPCs:** never TakeDamage(amount, source), ApplyDamage or a client-reported hit list. Only attack intent.
- **Never trust:** source GameObject, damage amount, hit result, immunity, friendly-fire override or client death events.
- **Refactor:** server-derived damage context containing attack ID/tick, authenticated attacker and target incarnation; a single trusted mutation gate. Keep source-free ApplyDamage internal to server rules. Normal actors permit environmental null sources; Kings reject them—retain that explicit distinction.
- **Cheating:** source spoofing, direct objective damage, policy bypass and damage replay.
- **Synchronization:** replay/rollback applying the same hit twice, duplicate death rewards, stale actor IDs. Deduplicate by attack and target incarnation; proxy presentation must not trigger server rewards.

### 6. Player melee — class 3

Files: [MeleeAttack](../Assets/Game/Combat/MeleeAttack.cs), MeleeAttackDefinition; MeleeSwingVisual is class 1.

- **Ownership:** participant input; server accepted attacks/hits.
- **Authority:** life, action conflicts, stamina, timing, facing, geometry and server equipment damage.
- **Replicate:** attack sequence/type, start tick, active/recovery phase, committed facing, cancellation and hit effects. HitTargets stays server-side.
- **RPCs:** attack button edge in ticked input preferred; otherwise deduplicated request. Accepted/rejected feedback is keyed to the action, not the durable state source.
- **Never trust:** active window, rate, damage, overlap/raycast results, definitions or unrestricted historical timestamps.
- **Refactor:** tick-driven action state replaces LateUpdate/elapsed; split prediction from irreversible damage/events. Existing OverlapBox/RaycastAll sample current physics; add a lag-query boundary later. Damage currently reads gear at ResolveHits: explicitly decide whether to preserve live gear evaluation or freeze at attack start; do not change it accidentally while porting.
- **Cheating:** fast attacks, enlarged hitboxes, fake hits/rewind times, duplicate requests, gear timing exploits.
- **Synchronization:** windows crossing different frames, replayed VFX, historical target pose tested against current dodge immunity or gear. Define and test one timing policy.

### 7. Dodge — class 3

Files: [PlayerDodge](../Assets/Game/Player/PlayerDodge.cs), DodgeDefinition.

- **Ownership:** player intent; server action/motor.
- **Authority:** grounded check, direction, distance, stamina, duration, recovery and i-frames.
- **Replicate:** sequence, direction, accepted start/end/recovery ticks, cancellation and corrected pose/resources. Remotes derive visual progress.
- **RPCs:** dodge edge/direction in input; never “invulnerable now” or a client displacement.
- **Never trust:** elapsed progress, arbitrary velocity override, no-cost cancellation or freely backdated immunity.
- **Refactor:** restore dodge state alongside motor/stamina; one coordinator selects dodge versus movement. Validate facing fallback, currently influenced by local lock-on. Record authoritative i-frame intervals for the chosen historical hit policy.
- **Cheating:** permanent immunity, extended rolls, cooldown bypass, retroactive dodge requests.
- **Synchronization:** predicted roll rejected, missed input edge, OnDisable cancelling the wrong role, hit and i-frame clocks disagreeing.

### 8. Definitions and catalogs — class 4 content, class 2 evaluation

Files: ItemDefinition, ItemCatalog, ItemTypes, EquipmentSlot, CharacterStatsConfig, MeleeAttackDefinition, DodgeDefinition, EnemyDefinition, CampDefinition, LevelProgressionDefinition, AttributeProgressionDefinition.

- **Ownership:** build content; server selects permitted definitions.
- **Authority:** server content canonical; client copies support tooltips/prediction.
- **Replicate:** definition IDs, selected config/version and content manifest hash. Ship immutable assets in builds; do not transmit ScriptableObjects/textures/prefab references/formulas every tick.
- **RPCs:** version handshake only; no content-upload/edit RPC.
- **Never trust:** damage, stack limits, requirements, XP curves, costs, profile values or a claimed hash as proof an executable is unmodified.
- **Refactor:** preserve GUID item IDs and numeric enums; add versioned registries for other definitions/prefabs. Freeze runtime rule data per match. Validate duplicate camp IDs: OnValidate only generates blank IDs, so duplicated scene objects can retain a shared ID.
- **Cheating:** modified asset values, invented item IDs. Hash checks detect compatibility errors, not all tampering.
- **Synchronization:** formula/collider/NavMesh/content mismatches. ItemIdentityImporter and ItemDefinitionValidation are editor/build-only: run in CI, no runtime replication/RPCs.

### 9. Inventory — class 3, owner-private

Files: [Inventory](../Assets/Game/Inventory/Inventory.cs), [InventoryState](../Assets/Game/Inventory/InventoryState.cs), InventorySnapshot/InventorySlot.

- **Ownership:** stable participant, not temporary connection or avatar object.
- **Authority:** server adds/removes/clears items and commits deposits/exchanges.
- **Replicate:** owner-only slots, schema, participant, match epoch, revision and transaction acknowledgements. Other players do not need backpack contents.
- **RPCs:** current supported intentions are equip/unequip and chest interaction. No generic AddItem, ClearInventory or snapshot-upload RPC. No new consumable/drop mechanic is implied.
- **Never trust:** ownership, quantities, slot contents, capacity, catalog, snapshots or hasAuthority.
- **Refactor:** bind authority to server role and keep replicas read-only. InventorySnapshot has no owner/epoch; TryApplySnapshot rejects equal revisions, including initial revision zero. Add explicit initial/reset hydration and per-session ordering. Use expected revision/item ID for slot requests. Serialize server transactions and queue observer mutations; per-component flags are not a network transaction protocol.
- **Cheating:** item creation, accessing another inventory, replayed rewards, stale-slot swaps and snapshot injection.
- **Synchronization:** old higher revisions poisoning new matches, reconnect losing state, partial inventory/equipment updates. Existing copied snapshots and staged InventoryState transactions are valuable foundations.

### 10. Equipment and requirements — class 3

Files: [Equipment](../Assets/Game/Equipment/Equipment.cs), IEquipmentRequirementSource, EquipmentSlot.

- **Ownership:** participant loadout; server owns the six slots/modifiers.
- **Authority:** sender, item possession, bounds, definitions, base-plus-allocated requirements, capacity and applicable action/match restrictions.
- **Replicate:** public equipped IDs/appearance and loadout revision; owner transaction/inventory updates and consistent derived resources.
- **RPCs:** Equip(slotIndex, expectedItemId, equipmentSlot, expectedRevision, requestId), Unequip(slot, expectedRevision, requestId). Derive participant from sender.
- **Never trust:** requirement satisfaction, final stats, equipment contents or return capacity.
- **Refactor:** add passive equipment snapshots; today there is only a local string array and Changed event. Preserve TryExchange atomicity and co-publish inventory/equipment/derived-state revisions. Decide equip while dead/attacking/Finished: CanChange currently checks enabled, inventory authority and transfer state, not those gameplay restrictions.
- **Cheating:** gear not owned, requirements bypass, duplication, mid-swing modifier abuse.
- **Synchronization:** item appearing in backpack and equipment, stats computed before loadout arrives, late join missing Changed history.

### 11. Session attributes — class 3

Files: [CharacterAttributes](../Assets/Game/Equipment/CharacterAttributes.cs), AttributeProgressionDefinition; allocations are stored in SessionProgression.

- **Ownership:** participant session build; server allocation and calculation.
- **Authority:** bases, five allocations, flat/percent modifiers, requirements and resource formulas.
- **Replicate:** owner base-profile ID, counts, unspent points, loadout/progression revisions and authoritative maxima/final values needed for correction. Do not create independent writable derived-stat sources.
- **RPCs:** IncreaseAttribute(stat, expectedRevision, requestId, matchEpoch), exactly one point per unique accepted request.
- **Never trust:** balance, base/final totals, modifiers, formula or client-selected session identity.
- **Refactor:** add passive allocation/progression hydration. Current TryIncreaseAttribute checks points but lacks request deduplication: retries can spend multiple valid points. Calculate after coherent gear/progression application; do not trigger authoritative resource mutation on replicas. Preserve gear exclusion from requirement values.
- **Cheating:** free stats, illegal enums, retry/double-spend abuse, modified configured bases.
- **Synchronization:** deducted points without allocation, mixed gear/allocation revisions, capacity clamp ordering, missed subscriptions while disabled and stale corrections after reset.

### 12. Interaction — class 3, class 1 prompts

Files: PlayerInteractor, IInteractable, InteractionReach.

- **Ownership:** local candidate/prompt; server operation on world object.
- **Authority:** authenticated player's live actor, reach/LOS from authoritative pose, object state and interaction rules.
- **Replicate:** changed world/private state and response code; not candidate lists or prompt text.
- **RPCs:** Interact(targetEntityId, requestId, epoch). Server resolves the sender's Inventory; do not accept the current TryInteract(Inventory, ...) argument from a client as ownership proof.
- **Never trust:** position, interaction point/range, visibility, nearest-object claim or another player's inventory reference.
- **Refactor:** local selection versus server dispatch. Keep chest's independent reach check, but use server physics. Add incarnation checks, bounded requests and rate limits.
- **Cheating:** through-wall/distant loot, interaction after death, targeting another player's belongings.
- **Synchronization:** despawn between request and execution, stale prompt, corrected movement changing reach, rejection leaving predicted items behind.

### 13. Loot rolling and chest claims — classes 2 and 4

Files: [LootTable](../Assets/Game/Items/LootTable.cs), [LootChest](../Assets/Game/World/LootChest.cs); LootChestVisual is class 1.

- **Ownership:** server world object; first eligible claimant owns existing overflow rights, not object authority.
- **Authority:** roll once, record claim/result, validate capacity, commit deposit/remainder together.
- **Replicate:** public chest ID/open/empty/revision; claimant visibility as design permits; pending rewards only to claimant. Never broadcast hidden loot or RNG seeds unnecessarily.
- **RPCs:** Interact only; no RollLoot(seed), GiveRewards or client acknowledgement that authorizes a grant.
- **Never trust:** random result/seed, claimant, remainder, unopened status or capacity.
- **Refactor:** claimant is an Inventory reference; replace with participant ID and explicit disconnect/expiry rules. Keep server randomness but record rolls so retries/recovery/resimulation cannot reroll. Serialize competing claims and publish coherent chest/inventory results; add late-join state application.
- **Cheating:** rerolls, double claims, stolen overflow, replayed deposits.
- **Synchronization:** open state ahead of inventory, reconnect creates a different Inventory and strands overflow, local predicted loot conflicts with rejection. processing prevents local reentrancy, not distributed races.

### 14. Enemy AI, perception, navigation and melee — class 2; actor presentation class 3

Files: EnemyBrain, EnemyPerception, EnemyNavigation, EnemyMeleeAttack, EnemyDefinition, EnemyThreatTarget; EnemyDebugVisual is class 1.

- **Ownership:** dedicated server owns NPCs; never delegate them to the nearest human.
- **Authority:** detection, aggro, leash, patrol, return, navigation, attack, death and physical blocking.
- **Replicate:** entity/definition/team, pose/locomotion, health/death, attack phase/start tick and target ID when needed for telegraphs. Internal paths, unseenTime and patrolIndex need not go to clients.
- **RPCs:** none for AI decisions. Player attacks enter common combat and can cause server retaliation.
- **Never trust:** NPC pose/path/target, attack timing, client kill claims or despawn as death.
- **Refactor:** server-only NavMeshAgent/brain and tick timers. Brain.Tick checks hasAuthority, but public EnemyMeleeAttack.TryStart/Tick have no independent authority boundary; CharacterStats still regenerates locally. Define trusted call ownership and configuration-before-network-spawn. Scope static physics queries by match/peer.
- **Cheating:** pacifying/moving NPCs, forged kills/sources, flooding requests to exhaust physics work.
- **Synchronization:** independent navigation on proxies, duplicate spawns, death/collider disagreement, target reference arriving late. Profile total NPCs: each CampDefinition allows up to 32 members, so six humans is not the total simulation budget.

### 15. Fortress guards — class 2 policy, class 3 representation

Files: FortressGuard, IEnemyTargetPolicy and shared enemy/team components.

- **Ownership:** server; guard home/assignment belong to team fortress.
- **Authority:** opposing-player-only selection; allies/NPCs ignored including retaliation. Friendly-fire permission does not change guard targeting.
- **Replicate:** identity, team, pose, health and action/return state through NPC snapshots.
- **RPCs:** no SetGuardTarget, ChangeGuardTeam or MoveGuard. Normal validated player attacks only.
- **Never trust:** team, player/NPC kind, home, eligibility or another player's identity.
- **Refactor:** assign roster/team before spawn; reuse policy, not a second network AI. Server checks policy both at acquisition and hit resolution; replicas must not choose replacements.
- **Cheating:** forged allegiance, pretending to be an NPC to evade guards, redirected aggro.
- **Synchronization:** team and target arriving separately, disconnect/respawn invalidating target, policy running on incomplete proxy state.

### 16. Camps and rewards — class 4 state, class 2 decisions

Files: Camp, CampDefinition, CampExperienceReward, CampExperienceSource; CampDebugVisual is class 1.

- **Ownership:** server world, stable unique camp ID plus match incarnation.
- **Authority:** members, spawns, counted deaths, one-time clear/chest and XP eligibility.
- **Replicate:** camp ID, member total/dead count, cleared/revision and reward chest ID. NPCs replicate independently; private delegates/implementation membership do not.
- **RPCs:** none for CompleteCamp, SpawnChest or AwardXP. Attacks/interactions are player intentions.
- **Never trust:** counts, claimed participation/last hit, clear events, reward size/recipients.
- **Refactor:** server spawn adapter replaces Instantiate; authority/configuration before callbacks. Preserve disabled/despawned != dead. Add unique camp validation and idempotent spawn/reward records. CampExperienceSource caches component recipients and SessionIds in Awake; replace with participant registry and a credit policy after session initialization.
- **Cheating:** fake clears, duplicate rewards, reward theft and repeated-spawn farming.
- **Synchronization:** clear before chest spawn, missed late-join events, lost references, mismatched Awake order. Current credit gives the full reward to every configured recipient, without team/proximity/contribution rules; it is not yet a 3v3 PvPvE credit specification.

### 17. XP, levels and session reset — class 3 state, class 2 grants

Files: SessionProgression, LevelProgressionDefinition, LevelUpEvent, ProgressionSession.

- **Ownership:** stable participant build; server ledger/curve/grants.
- **Authority:** reward sources/IDs, amounts, levels, awarded points and reset epoch.
- **Replicate:** owner XP remainder/threshold, level, points, allocations, session and revision. Public level separately if wanted. Reward-deduplication IDs remain private server data.
- **RPCs:** no AddExperience or BeginSession. Attribute spending is a request; XP arises from server events.
- **Never trust:** XP claims, recipients, reward ID, levels, points, resets or hasAuthority.
- **Refactor:** retain copied curves, overflow checks and rewardedIds; bind to server match ID. ProgressionSession.Awake invents a GUID and uses a serialized roster—replicas would create separate matches. Add initialization barrier and passive snapshots. Reconnect resumes existing participant state; Finished reward rules must be explicit.
- **Cheating:** arbitrary XP, replay with new reward IDs, reconnect/reset exploitation, grants repeated on replacement actors.
- **Synchronization:** per-peer GUIDs, revision zero reset while old messages are in flight, duplicated level-up effects and new attributes paired with old inventory. Event totals are historical; snapshots establish present state.

### 18. Teams and six-person roster — class 4

Files: TeamIdentity, TeamId/CombatantKind. A networking admission/roster system does not exist.

- **Ownership:** server owns team slots and participant/avatar mapping.
- **Authority:** assignment, player/NPC kind, friendly-fire policy, maximum six humans and three per side.
- **Replicate:** participant/entity IDs, team/kind, connection/readiness and approved rules. Never credentials.
- **RPCs:** authenticated join/ready/reconnect; optional Waiting team preference if approved. No direct TryConfigure passthrough.
- **Never trust:** requested identity/team, reserved slot, second connection ownership or friendly-fire toggle.
- **Refactor:** restrict TryConfigure to lifecycle/authority code; passive replica apply. Capacity is separate from connection count: dedicated server is not a human participant. Reject seventh player and duplicate occupancy.
- **Cheating:** impersonation, switching allegiance during attacks, spoofing NPC kind or friendly-fire rules.
- **Synchronization:** spawn before assignment, hit/team change race, prematurely reusing a disconnected player's reserved slot.

### 19. Kings — class 3

Files: KingObjective, CharacterStats, DamageReceiver, Targetable.

- **Ownership:** server owns one bound King per team.
- **Authority:** profile, registered team, strict damage policy, latched defeat and notification.
- **Replicate:** objective ID/team, health/max, defeat and binding. Late clients see defeat without replaying winning damage.
- **RPCs:** no KillKing/Defeated. Normal attack requests only; future King AI stays on server.
- **Never trust:** health/team, attacker, defeat or a client objective event.
- **Refactor:** server-only initialization/resource configuration/binding; passive proxy health/latch updates without server OnDied. Current King owner is a local MatchManager, not network authority. Keep strict friendly immunity and distinguish destruction from authoritative death.
- **Cheating:** policy bypass, spoofed sources, instant wins via resource methods, proxy death accepted as real.
- **Synchronization:** King before match binding, initial profile overwriting snapshots, duplicate death after reconnect, queued hit on survivor after Finished. Reentrant protection needs one server commit timeline.

### 20. Match state, result and lifecycle — class 4

Files: MatchManager, MatchStateMachine, ProgressionSession; MatchStatusDisplay is class 1.

- **Ownership:** server match epoch, roster, objective bindings and immutable result.
- **Authority:** Waiting/Playing/Finished, first valid King death, lifecycle. Public local APIs are not client permissions.
- **Replicate:** epoch, state, transition/result sequence, winner, start/end ticks, King IDs and roster/readiness. Add operational result reasons only after abort/forfeit rules are decided.
- **RPCs:** ready/leave/reconnect intentions. MatchEnded can trigger an effect, but replicated result is durable truth; a one-off broadcast is insufficient.
- **Never trust:** winner, defeated team, requested match ID/reset or client ready as proof the required scene/content loaded.
- **Refactor:** production server bootstrap replaces Start Automatically. Coordinate match and progression lifetimes without merging them into a giant manager. Finished currently gates only Kings: XP, loot, gear, attributes and ordinary combat may continue. Define action permissions. Choose same-tick death ordering or a tie rule; do not let packet arrival order become an accidental design.
- **Cheating:** forced results/resets, post-finish grants/actions, manipulated readiness, a player-host altering server state.
- **Synchronization:** result before referenced objects, old result in new epoch, reconnect missing MatchEnded, different local winners. Finished is terminal; a new round needs a complete new session/actor lifecycle.

### 21. Visuals, debug and authoring — class 1 views; class 2 development mutations

Files: MeleeSwingVisual, EnemyDebugVisual, CampDebugVisual, LootChestVisual, MatchStatusDisplay, InventoryDebug, EquipmentDebug, SessionProgressionDebug, TeamDebug, KingObjectiveDebug; World/Editor and Items/Editor tools. FortressTestSceneBuilder and KingBattlefieldBuilder are editor-only authoring tools: they create one local player and configured scene references, not a six-player runtime server bootstrap. Preserve them as local test builders and introduce a separate authoritative spawn path later.

- **Ownership:** each client owns rendering; trusted development server tools own authorized test mutations. Editor builders are authoring/CI, outside runtime categories.
- **Authority:** no mesh, label, UI or log determines health, rewards or result.
- **Replicate:** gameplay view data only; not label rotation, mesh transforms or GUI boxes. Reconstruct current state separately from transient effects.
- **RPCs:** ordinary UI uses validated requests. No production GrantXP/AddItem/JoinTeam/KingDamage debug endpoints.
- **Never trust:** Debug.isDebugBuild, hidden UI, private C# methods or context menus as security controls.
- **Refactor:** views read passive models instead of requiring active simulation. Remove camera/Input/OnGUI startup dependencies from headless builds. Strip mutation tools from production or use authenticated development-only administration. SessionProgressionDebug currently checks only Application.isPlaying, weaker than several other debug components.
- **Cheating:** accidental test-command exposure; private inventories/loot leaked through broad debug feeds. Client-only tampering should be cosmetic under server authority.
- **Synchronization:** duplicate effects on resimulation, missed events on subscription, Camera.main from another peer, visuals invoking spawn/reset. Authoring tools have no runtime RPC or replication need.

## Pre-networking refactor priorities

Recommendations only; not implemented by this audit.

| Priority | Code evidence | Preparation |
| --- | --- | --- |
| P0 | PlayerController.Update/MeleeAttack.LateUpdate/CharacterStats.Update simulate locally. | Separate input/simulation/views; one tick; passive proxies. |
| P0 | Partial writable hasAuthority flags; no authenticated combat/team/match boundary. | Trusted mutation APIs and sender-to-participant validation. |
| P0 | No six-player admission or identity registry. | Participant, entity/incarnation, epoch and roster contracts. |
| P0 | IDs/resources/spawns created in Awake/Start. | Ordered server bootstrap before simulation. |
| P0 | Motor/dodge private state cannot be restored; hits use current physics. | Capture/restore, prediction and timing/rewind policy. |
| P1 | Most systems expose only local events, not hydration. | Initial snapshots/deltas; never replay gameplay events to load replicas. |
| P1 | Inventory lacks owner/epoch; spend/equip lack request IDs. | Initial revision-zero handling, stale checks and bounded deduplication. |
| P1 | Match Finished/reset cover only some systems. | Complete lifecycle and action permission matrix. |
| P1 | Chest claimant/camp recipients are component references. | Stable participant claims and explicit PvE credit/reconnect rules. |
| P1 | Debug mutations and global camera/physics queries remain. | Production stripping, scoped views/physics and headless smoke test. |
| P2 | Sparse runtime definition identities and missing objective test scenes. | Content manifest/spawn registry and verified local battlefield. |

Retain InventoryState staging, GUID item IDs, interfaces, separate visuals, XP deduplication/overflow checks, configurable formulas and the pure MatchStateMachine. Do not turn every MonoBehaviour into a networking subclass or introduce one catch-all NetworkGameManager.

## Network contract and synchronization design

### Command boundary

All client requests go through a server adapter that derives participant identity from the authenticated sender. Validate current match epoch, controlled actor incarnation, connection/admission state, allowed match phase, actor life/action state, bounded payload, finite numeric input, enum/slot/ID validity, rate limits and relevant revisions. Client-supplied IDs are lookup hints, never authorization.

Use bounded per-participant request IDs for discrete transactions. A duplicate returns the recorded accepted/rejected result without applying again. Sequence continuous input independently; clamp acceptable past/future ticks and processing budget. Reliable delivery is not application-level exactly-once execution, and ordering across separate objects/channels cannot be assumed.

| Message | Direction/channel intent | Server-derived result |
| --- | --- | --- |
| Movement/look/jump/attack/dodge input | Client -> server ticked input; sequence and button-edge history | Pose/action/cost from authoritative simulation, input acknowledgement. |
| Equip/unequip | Client -> server reliable request + expected inventory/loadout revision | Atomic item transfer, requirement validation, coherent stats snapshot. |
| Interact with chest | Client -> server reliable request + object ID/incarnation | Reach/LOS/life check, first claim and deposit/remainder. |
| Increase attribute | Client -> server reliable idempotent request | One debit and one allocation, derived resources. |
| Join/ready/reconnect/leave | Authenticated session request, not gameplay ownership assertion | Admission, reserved participant/team and scene readiness. |
| Health/resources/pose/actions/world state | Server -> authorized recipients as snapshots/deltas | Client presentation/correction only. |
| Match result | Server -> all participants as durable state | Epoch, terminal state, winner, result sequence/end tick. |
| Hit/level-up/chest effects | Server -> appropriate recipients; transient events keyed by ID | Deduplicated effects, never the sole source of durable state. |

Do not expose RPCs for damage, healing amounts, loot generation, item grants, XP grants, team writes, camp completion, King death, arbitrary spawn/despawn or match result/reset. Future item-use actions should identify a held item/action, not request a resource amount.

### Replication visibility and cadence

These are starting design targets to measure, not proven performance limits.

| Data | Recipients | Suggested handling |
| --- | --- | --- |
| Roster/team/match epoch/result/Kings | All admitted participants | On change plus full hydration; coherent result record. |
| Player/NPC transforms and actions | Relevant observers | Start with 60 Hz simulation and approximately 20–30 Hz remote snapshots; profile/tune. |
| Owner motor/resource corrections | Owning participant | Stack prediction/correction stream with processed input tick. |
| Health/max/life | Relevant observers; Kings always shared | On change/coalesced updates, consistent with damage sequence. |
| Stamina/mana/XP/points/allocations/backpack | Owner | On change, bounded snapshots; private server ledger stays private. |
| Equipment appearance | Relevant observers | On change, linked loadout revision. |
| Camp clear/count and chest open state | Relevant observers or all six if deliberately public | On change plus full snapshot; reward contents private. |
| Pending chest rewards | Claimant | Private transaction snapshot. |
| Definitions | Build content + server-selected manifest | Handshake/configuration, not per-frame replication. |

With six humans, begin with simple relevance for public world facts and strict owner filtering for private state. Do not invent complex interest management before measuring NPC/bandwidth load. Conversely, hiding a rendered enemy is not information security if its position is still sent. Decide whether hidden NPC/player positions are legitimately shared before adding fog/stealth mechanics.

For prediction, retain all state needed to replay the owning motor/action/resource timeline. Keep irreversible server damage, XP, rolls, item transfers and match results out of client resimulation. Spawn/death/result events need stable IDs so reconciling does not replay rewards or effects.

### Melee and dodge timing

Adopt one server tick for movement, action eligibility, stamina, attack windows, dodge i-frames, death and objective finish. Render interpolates independently. Evaluate acceptance order for simultaneous attack/dodge inputs explicitly.

For later lag compensation, query bounded historical target hitboxes using the stack's validated input timing. Do not let a client choose unlimited rewind. Specify whether a historical hit tests historical i-frames and how attacker pose/facing and moving obstructions are evaluated. Do not combine a rewound target with arbitrarily current immunity/geometry. Pick and measure a maximum compensation window (for example, prototype 100–150 ms, then tune); this is a proposed test parameter, not an established game rule. Do not rewind committed loot, XP or match results.

For two King-lethal hits in one simulation tick, the current local rule is first committed death wins. Preserving it requires a stable server action ordering and recorded result tick/sequence. A draw rule would be a design change. Packet arrival order and Unity physics enumeration should not silently choose the rule.

### Session start, reconnect and end

Proposed lifecycle: authenticate -> reserve one of six participant slots (three per team) -> verify build/content -> load scene -> create server epoch and roster -> spawn configured actors/world -> hydrate clients -> start Playing at a server tick. Default production entry should wait for the approved start condition; a two-player debugging session can be a separate development mode.

Reconnect must authenticate the same participant and rebind to server-owned state: inventory, gear, XP, allocation, entity incarnation/pose, team and pending chest rights. It must not call the new-match XP reset path. Define grace duration and disconnected-avatar behavior before implementing it; remaining attackable during a grace period is a reasonable proposal to prevent disconnect invulnerability, not an existing rule.

At finish, commit the result once and enforce the chosen action policy before accepting more requests. Recommended competitive default: stop XP/loot/allocation/equipment and new combat commitments after Finished, while allowing view/chat/leave and finishing cosmetic effects. This is stricter than current behavior and requires design approval before implementation.

For a new match, clear server reward/claim/request ledgers; recreate or explicitly reset inventory, gear, allocations, XP, resources, action/motor state, NPCs, camps, chests, Kings and match bindings; increment epoch; drop old queued inputs/snapshots. Resetting ProgressionSession alone currently does not do this. A server crash is not a normal player reconnect; initially terminate the session cleanly rather than promise unimplemented crash recovery/host migration.

### Proposed phase permissions and commit ordering

This table is a proposed production policy for M16/M32, not a change to existing local behavior. All gameplay mutations must check the phase at execution, including queued server rewards and AI actions, not only when an RPC arrives.

| Operation | Waiting | Playing | Finished |
| --- | --- | --- | --- |
| Camera, menus, approved state/result viewing | Allowed | Allowed | Allowed |
| Ready and initial team assignment | Server admission rules | No reassignment; backfill only if approved | No assignment into the completed match |
| Movement, jump, attack and dodge simulation | No match simulation; separate warm-up only if designed | Live admitted actors under action rules | Freeze gameplay; cosmetic camera movement remains local |
| NPC decisions, damage and resource regeneration | Initialize only | Server simulation | Stop combat/resource mutation |
| Chest claim/deposit, equipment exchange, attribute spend | Initial server loadout only | Validate each request; death/equip timing still needs a rule | Reject new transactions |
| Camp clear, XP grants and reward spawning | No grants | Commit once from server evidence | Reject newly executed grants |
| Reconnect and snapshot hydration | Reserved participant rules | Resume existing state | Show the committed terminal snapshot/result |
| Reset/new match | Trusted bootstrap | No player reset command | New epoch through trusted lifecycle |

Proposed tick sequence: ingest bounded input -> validate/choose actions -> movement -> advance action windows -> resolve combat in stable order -> commit deaths/rewards/result -> publish coherent snapshots -> render effects on clients. Recheck the phase before each irreversible commit: a King death may finish the match during combat resolution. A transaction committed before the finish boundary remains committed; acknowledgements may arrive afterward. A queued transaction not yet committed must be rejected under the proposed freeze policy. Record the ordering explicitly; the current local event chain is not this new scheduler.

Inventory conservation must include chest remainder, backpack and equipped slots. A retry with the same request ID returns the same outcome; reject reuse of that ID with a different payload. Cache entries need a bounded lifetime, and requests older than the retained sequence window must be rejected rather than treated as new. A lost reply followed by reconnect must be resolved by snapshot/revision and transaction outcome, never by blindly repeating the grant.

## Recommended current networking stack

**Recommendation: Photon Fusion 2 in Dedicated Server mode, subject to an isolated Unity-version/latency compatibility gate.** This is a project-fit recommendation, not a measured benchmark or an instruction to install it now.

The official SDK page lists **Fusion 2.1.2 Stable, released August 13, 2026**. Its published Unity requirements list 2021.3.45, 2022.3.45, 6.0.x and 6.3.x, **not this project's 6000.6.0f1**. Test the exact project version, Input System, AI Navigation, build target and prediction controller before pinning dependencies. If support fails, choose a supported Editor version through a separate migration decision or reconsider the stack; do not silently downgrade the project. [Photon SDK downloads and requirements](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download).

Why Fusion fits this game: its client/server input model supports client prediction, rollback and resimulation, matching responsive third-person movement and dodge while the server owns combat. [Photon player input](https://doc.photonengine.com/fusion/v2/manual/input/player-input). Its lag-compensation API supports box/sphere overlaps as well as rays, which maps to this project's melee volumes; ordinary Unity physics queries do not automatically gain those features. [Photon lag compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation).

Use input authority only for the controlling human and state authority on the dedicated server for every gameplay object. Host Mode is useful for early developer smoke tests but does not protect against a cheating host and must not be the competitive production trust model. Dedicated mode runs the game simulation separately and needs both the Fusion service and separately hosted game-server compute; Photon Cloud is not automatically hosting this Unity simulation for you. [Photon dedicated-server topology](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/dedicated-server-overview).

Recommended stack layers:

- Fusion 2 client/server simulation and replication; consider its Simple/Advanced KCC only after measuring current motor compatibility. Changing controllers is a gameplay-feel risk, not a free networking checkbox. [Photon Simple KCC](https://doc.photonengine.com/fusion/v2/addons/simple-kcc).
- A Unity dedicated-server build, initially a local headless process plus standalone clients; later hosted per match. Keep render/input code out of its startup requirements. [Unity dedicated-server build](https://docs.unity3d.com/6000.0/Documentation/Manual/dedicated-server-build.html).
- Authenticated participant/session admission integrated with the selected platform/backend; Photon session connectivity alone does not enforce this game's 3v3 roster or progression rules.
- Separate server hosting/orchestration chosen after platform, region, expected concurrent sessions and budget are known. Six players per match is not the service's total concurrent-user requirement. Budget for both network service and compute/egress/operations.
- Avoid stacking Unity Relay/MPS onto Photon by default. Add a service only for a demonstrated need and supported integration, not duplicate session ownership.

**Current hosting caveat:** Unity states that its Multiplay Game Server Hosting was deprecated April 1, 2026 and new scaling/allocations are unavailable except the stated migration exception. Do not base a new deployment plan on older Multiplay tutorials; evaluate current third-party hosting separately. [Unity service notice](https://status.unity.com/info_notices/362941).

### Alternatives

| Stack | Fit and tradeoff | Decision |
| --- | --- | --- |
| Photon Fusion 2 dedicated | GameObject-friendly input prediction and historical hit queries; service plus separate hosting; exact 6000.6 compatibility not established. | Preferred technical fit for this action PvPvE prototype, gated by a spike and budget. |
| Netcode for GameObjects + Unity Transport | First-party GameObject path, suitable server-authoritative state/RPC plumbing; current documented anticipation is not a complete rollback/replay movement solution. | Best first-party fallback if that ecosystem is prioritized and prediction/lag-query engineering is explicitly budgeted. |
| FishNet dedicated | Prediction support in a GameObject stack; its documented ColliderRollback lag-compensation component is a Pro feature. | Strong alternative if Photon dependency/cost is undesirable; verify exact license, transport, Unity version and controller integration. |
| Mirror dedicated | Familiar GameObject server/Commands/state approach; its current prediction guide still labels that prediction work experimental. | Viable for state networking, less attractive here if the team must assemble responsive movement/rewind itself. |
| Netcode for Entities | ECS prediction framework. | Not the first migration choice: current code is MonoBehaviour/CharacterController/NavMesh-based, so ECS conversion adds substantial unrelated work for six humans. |

The NGO **2.13.2** documentation explicitly distinguishes anticipation from full client prediction/reconciliation. This limitation is verified against the current documented branch, not inferred from old tutorials. [Unity NGO client anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/client-anticipation.html).

FishNet documents prediction and the Pro status of ColliderRollback. [FishNet prediction](https://fish-networking.gitbook.io/docs/guides/features/prediction), [FishNet ColliderRollback](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/components/colliderrollback). Mirror documents server authority and qualifies its prediction implementation as experimental; that is a reason to test it, not a claim that all Mirror networking is experimental. [Mirror authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority), [Mirror prediction](https://mirror-networking.gitbook.io/docs/manual/general/client-side-prediction). Unity's Entities documentation describes its prediction model; the rewrite-cost assessment here comes from this repository's architecture. [Netcode for Entities prediction](https://docs.unity3d.com/Packages/com.unity.netcode@1.10/manual/prediction.html).

No stack validates this game's economy, team rules, participant identity or RPC payloads automatically. No authoritative stack eliminates bots, aim assistance, information leaks or server overload. Those require application policy and observability.

## Migration plan in small milestones

**Plan only. None of these implementation milestones are authorized by this audit request.** M16–M19 prepare the local project without networking. M20 onward require a later instruction to begin networking. Preserve the existing offline harness and configurable behavior throughout; resolve deliberate rule changes separately.

| Milestone | Bounded scope | Dependencies | Exit criteria |
| --- | --- | --- | --- |
| M16 — Verify baseline and decisions | Locate/reconcile DESIGN.md; complete pending local King/guard battlefield setup and tests through Unity MCP; record readiness, death/respawn, PvE credit and finish rules. | This audit | One complete offline vertical slice is verified; build scene list and test evidence exist; unresolved rules have owners. No netcode package installed. |
| M17 — Input/simulation/presentation separation | Split local input/camera/UI from player simulation; establish trusted command interfaces; make non-simulating views possible. Strip production debug mutation paths. | M16 | Existing local play feels unchanged; a camera-free simulated actor works; one actor's view cleanup cannot control another actor. |
| M18 — Simulation clock and recoverable action state | One fixed simulation driver for movement, resources, attack and dodge; capture/restore motor/action/resource state; separate irreversible effects. | M17 | Recorded local inputs replay on a fixed tick with agreed tolerances; no double ticking at 30/60/144 render FPS; restore tests cover mid-dodge and recovery. Physics determinism is not assumed. |
| M19 — Identity and snapshot contracts | Define participant/entity/incarnation/epoch IDs, read-only hydration, coherent transaction revisions, revision-zero initialization, request IDs and reset contract. | M17 | Offline serialization round trips; old epoch and wrong owner rejected; duplicate spend/equip applies once; hydration raises UI updates without damage/reward events. |
| M20 — Stack compatibility spike | In an isolated branch/test project, pin candidate SDK, run dedicated process + two clients, test exact Unity version, KCC, resimulation and one historical overlap. Compare fallback only if necessary. | M18–M19; later networking authorization | Recorded SDK/editor compatibility and latency results; documented license/hosting assumptions; chosen version/controller. No unsupported Editor downgrade or package upgrade hidden in the spike. |
| M21 — Session admission and player spawn | Authenticated server session bootstrap, six human slots, three per team, one epoch, configured network spawns and ready barrier. | M20 | Six clients admitted; seventh/duplicate identity rejected; two balanced teams; one input rig per client; reconnect token cannot claim another participant. Server is not counted as a human. |
| M22 — Predicted movement | Input stream, server motor, owner correction, remote interpolation, safe teleport/incarnation handling. | M18, M21 | Standalone clients move under delay/jitter; forged position/speed ignored; render FPS does not change speed; slopes/jumps and disconnect neutral input pass. |
| M23 — Resources and life replication | Server stats/regen/cost ledger; private stamina/mana and public health/life snapshots; initialization order. | M19, M22 | No resource divergence after reconciliation; direct client writes cosmetic only; late join receives damaged/dead actor correctly without full-health overwrite. |
| M24 — Server melee | Ticked attack requests, action validation, current-server-time hit geometry, authoritative damage and hit deduplication. | M22–M23 | One attack hits a victim at most once; walls/team/range/cost/recovery enforced; forged hit/damage RPC attempts have no route; six-peer results agree. |
| M25 — Predicted dodge | Restore/reconcile motor and dodge action; authoritative cost, recovery and i-frame intervals. | M22–M24 | Dodge spam cannot extend immunity; rejected rolls reconcile; same-tick attack/dodge order is defined; no duplicate stamina charge. |
| M26 — Combat latency policy | Introduce bounded historical hit queries for the approved melee/dodge policy; historical hitboxes and relevant immunity history. | M24–M25 | Test at representative RTT/loss, behind-wall escapes and i-frame boundaries; forged old/future ticks rejected; no unbounded rewind or duplicated damage. Feel acceptance by game director. |
| M27 — Inventory and equipment networking | Owner-only snapshots, idempotent request handling, server requirement checks and coherent exchange/derived-stat updates. | M19, M21, M23 | Two concurrent stale slot requests cannot duplicate items; full-inventory swap behavior preserved; another client cannot read/write private backpack; owner reconnect snapshot ready. |
| M28 — Chest transactions | Server-only rolling, stable participant claim, concurrent request handling, overflow/retry and private rewards. | M27 | Two players opening simultaneously produce one roll/claim; retries cannot grant twice; overflow remains conserved; out-of-range/dead player rejected. |
| M29 — NPCs and guards | Server AI/NavMesh and spawns; proxy pose/action/death presentation; guard team policy. | M21, M24–M26 | No proxy runs AI; all clients see consistent attack/return/death; allies ignored; both team guards and neutral enemies behave correctly. |
| M30 — Camps and XP | Server member registry, confirmed-death clear, one reward chest and approved participant credit; progression snapshots/ledger. | M28–M29 | Contested camp follows recorded credit rule; dead count and rewards agree; duplicate death/clear cannot repeat XP/chest; disconnected recipient handling explicit. |
| M31 — Attribute spending | Idempotent spend command and coherent allocation/points/gear-derived maxima. | M23, M27, M30 | Same request spends once; insufficient/stale/foreign requests fail; resources/requirements update together; per-match allocation reset verified. |
| M32 — King victory and complete finish gate | Bind two server Kings; durable result; approved simultaneous-hit order; freeze prohibited post-finish actions. | M21, M24–M31 | Both victory paths; one result for simultaneous lethal attempts; all clients including late/reconnecting peers see winner; no forbidden post-finish XP/loot/spend/King hit. |
| M33 — Reconnect, teardown and second match | Participant rebind, claim restoration, snapshot hydration, grace/abandon policy, full session cleanup and epoch fencing. | M28–M32 | Disconnect during loot/dodge/victory recovers as specified; replayed prior-match requests rejected; second match starts clean with no retained XP/items/actions or duplicated NPCs. |
| M34 — Dedicated deployment and six-player hardening | Chosen hosting allocation/ready/shutdown flow, auth/build checks, rate limits, telemetry, real multi-process soak and NPC load profiling. | M33 | Six human clients plus representative PvE load meet agreed latency/frame/bandwidth budgets; malicious inputs rejected; server crash ends cleanly; no UI dependencies or production debug commands. |

A milestone is complete only with its acceptance evidence. Do not finish M21 by testing only host mode, M22 by synchronizing client-owned transforms, or M32 by sending an unpersisted winner RPC. Start with local dedicated-server processes before paying for production orchestration.

M18 prepares a transport-neutral simulation seam, not a custom general-purpose rollback engine. M20 chooses the SDK/controller-specific state representation before M22 implements prediction. If later networking authorization permits an earlier isolated compatibility probe, perform it before committing to a controller rewrite; do not install a speculative SDK into this prototype during M16–M19. Keep M34 operational work small by deploying the first local dedicated executable at M20 and repeating headless checks as each system migrates.

## Required verification matrix

These tests are future acceptance work; they were not executed by this audit.

| Area | Cases |
| --- | --- |
| Topology | Dedicated executable + 2, then 6 independent client processes; Editor multi-peer only as supplementary coverage. |
| Adversarial input | NaN/infinite vectors, oversized payloads, invalid enums/IDs, future/stale ticks, spoofed actor IDs, rate floods, wrong epochs, duplicate/reordered requests. |
| Movement | 30/60/144 render FPS; slopes, stairs, jumps, actor collisions, corrections, no input, teleport/respawn incarnation. |
| Network conditions | Start with 0/50/100/150/250 ms RTT, 0/1/3/5% loss, jitter and short outages. Label one-way versus RTT correctly. Chosen ship region/RTT target remains a design decision. |
| Melee/dodge | Edge-of-range hits, walls/moving blockers, attack/cooldown spam, same-tick dodge, i-frame boundaries, multi-collider victim, duplicate hit, target death/despawn mid-swing. |
| Resources | Cost rejection, regeneration delays, health/max ordering, death once, capacity changes, no replica healing, snapshot during attack. |
| Economy | Competing chest claims, full inventory overflow, stale equip slot, duplicate spend, corrupted snapshot, owner-only visibility, reconnect before response. |
| PvPvE | All camp members active, two teams contest one camp, duplicate kills/clears, spawn before snapshot, target switch/loss, friendly-fire/guard rules. |
| Objectives | Both winners, two lethal actions in one tick, late result delivery, destroyed versus dead King, post-finish requests, reconnect into Finished. |
| Lifecycle | Seventh join, duplicate identity, unbalanced team request, readiness timeout, disconnect in Waiting/Playing/Finished, second match and stale old messages. |
| Dedicated build | No input/camera/AudioListener/OnGUI dependency, correct static geometry/NavMesh/content, bounded GC/physics allocations and graceful empty-session shutdown. |
| Security/operations | Auth ticket reuse, participant hijack, content mismatch, debug-command stripping, bounded request caches, malformed data and resource exhaustion. |

Record correction frequency/magnitude, input age/rejection counts, action acceptance and hit tick, duplicate-request outcomes, snapshot bytes, private-state recipients, server tick cost/GC, NPC count, match finish sequence and reward/item conservation. Do not log credentials. Agree numerical performance budgets after the compatibility spike and representative NPC profiling; this audit has no bandwidth or CPU measurements.

## Design decisions still needed

Missing DESIGN.md prevents reconciliation of these rules. The recommendations below are provisional and were not implemented:

1. **Platforms, acceptable RTT and hosting budget.** Assume desktop-first for the spike; verify that assumption before selecting input/controller/build targets.
2. **Start condition.** Recommend production 3v3 readiness barrier, with an explicit smaller development mode; decide timeouts/backfill.
3. **Death/respawn.** Player respawn/elimination and disconnected-avatar behavior are not a complete network lifecycle in the current source.
4. **Disconnect/reconnect.** Grace duration, slot reservation, whether the avatar remains attackable, abandonment/forfeit, and claim release.
5. **PvE credit.** Teamwide, proximity, contributor or last-hit XP? The current fixed-recipient full grant does not answer contested-camp design.
6. **Loot rights.** Preserve current first-opener overflow claim initially, then explicitly decide team sharing, disconnect timeout and claim transfer.
7. **Finished permissions.** Recommend freezing all reward-producing commands; current implementation only prevents further King objective damage.
8. **Simultaneous kills and lag compensation.** Preserve first server-committed King death unless a tie rule is chosen; define hit/dodge historical time and rewind limits.
9. **Equipment timing and death restrictions.** Current server migration must not silently change hit-time modifiers or equip while dead/attacking.
10. **Visibility.** Which enemy stats/camp status/positions are public? Owner-only backpack/XP detail is the conservative initial replication policy.
11. **Recovery scope.** Recommend player reconnect, but no initial host migration or authoritative process-crash restoration; define how an aborted match is reported.

## Audit deliverable and verification

The completion review modified only this existing report (it remains untracked in Git from the previous audit). No project files were newly created or deleted by the completion review. No gameplay refactors, networking package installation, scene mutations or behavioral changes were performed. Existing changes from earlier milestones were preserved; no commit was made.

The 21 system sections cover the 67 C# files under Assets/Game by subsystem, including data definitions, interfaces, views and editor builders. Each section supplies ownership, authority, replication, request boundaries, untrusted data, preparation, cheating and synchronization findings. No production network readiness or percentage-complete claim follows from that coverage.

Completion checks: re-read AGENTS.md; repeated DESIGN.md search; checked the existing audit against implementation, package/Editor/build-scene configuration; verified key current stack claims against official Photon, Unity, FishNet and Mirror documentation on 2026-09-09; read the active Unity hierarchy and Console through MCP; checked relative Markdown file links and system-field coverage. A read-only MCP command compiled and executed successfully. No Play Mode, standalone build or automated gameplay validation was run. The initial command invocation failed before execution; the corrected read-only invocation succeeded without changing the scene.

The source/configuration/test preservation hash check covers Assets/Game, Assets/Scenes, Packages, ProjectSettings and Tests across the documentation edit. The scene was already saved and was not modified, so there was nothing to save through the Editor. Tooling warnings are not gameplay compiler errors and were left outside this documentation-only scope.

**Audit status: complete within the stated evidence limits. Implementation status: local prototype; multiplayer migration has not begun.** The next proposed task is M16: reconcile the design rules and verify the complete offline battlefield. Building the missing scenes and implementing any refactor remain separate tasks.
