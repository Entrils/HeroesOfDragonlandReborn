# Milestone 11: neutral monster camps

Milestone 12 now connects this camp reward to player session progression; see
[Milestone12.md](Milestone12.md). The XP notes below describe the Milestone 11 boundary.

SampleScene contains Test Neutral Camp at (-14, 0, 14). Enter Play Mode and approach
the camp to fight three enemies: two Bandits (85 health each) and one Wolf profile
(60 health, using the same placeholder body). A label shows the defeated count.
Kill all three: the camp records its clear, awards a 150 XP event, then spawns one
closed chest at (-14, 0.05, 10). Approach and press F or gamepad Y/Triangle to open it.
Items only enter inventory through that manual chest interaction.

## Configuration

Create > Heroes of Dragonland > Camp Definition creates reusable camp data:

- Enemy groups: enemy prefab root, EnemyDefinition and count per group.
- XP reward: nonnegative integer.
- Chest prefab: an enabled LootChest on the root, including its collider/visual.
- Loot table: existing LootTable; the test camp uses TestChestLoot.

Add Camp to a scene object. Assign its definition, a session-unique Camp ID, one
distinct child spawn Transform per enemy, and a child chest spawn Transform.
Enemy groups consume the points in order. Counts must total 1 to 32, and each
point must sample onto the prefab agent's compatible NavMesh within one metre.
The chest uses its configured point's world position and rotation at completion.
No NavMesh rebake is needed just to reposition spawn markers on an existing mesh.

The test definition is Assets/Game/Data/Camps/TestNeutralCamp.asset. Its chest
prefab is Assets/Game/World/Prefabs/CampLootChest.prefab, created from the existing
scene chest. The original independent scene chest and Test Bandit remain available.

## Ownership and completion

Camp.TryInitialize validates all configuration and spawn points before spawning.
It creates inactive enemy instances under Camp Enemies, assigns each definition
before Awake, registers ownership/death listeners, then activates the group.
EnemyBrain.TryConfigureSpawn provides this pre-Awake factory boundary without
resetting a living enemy. Existing EnemyBrain, CharacterStats, DamageReceiver,
Targetable and NavMeshAgent continue to own AI, health, combat and targeting.

Camp.Enemies exposes read-only membership; DeadCount records each member death
once from CharacterStats.Died. Disabling or despawning a living enemy never counts
as death. An unresolved despawn prevents completion and needs an explicit future
server/admin policy. Enemies cannot be added after initialization. Disabled camps
retain membership and observed deaths, and reconcile on re-enable.

The final death calls TryComplete. Its order is:

1. Latch IsCleared and advance Revision.
2. Record AwardedExperience and emit ExperienceAwarded(CampExperienceReward).
3. Instantiate exactly one chest, configure its table/authority while inactive,
   and activate it closed at the chest spawn point.
4. Emit Cleared(Camp), then Changed.

Repeat calls, repeat death notifications, disable/enable and authority cycling
cannot repeat completion, XP or chest spawning. Destroying a reward chest does
not allow spawning a replacement. Reentrant initialization, completion or authority
changes are rejected during the completion transaction. Observer exceptions are
logged independently so one listener cannot suppress the other notifications or
prevent the chest spawn. Resetting a scene creates a new session of the camp.

Camp never rolls loot, calls TryInteract, or writes inventory. LootChest performs
the roll when the player opens it, using all existing range, obstruction, authority,
catalog, overflow, claimant reservation and repeat-loot rules. Its new
TryConfigureSpawn method only configures inactive, unopened instances.

## XP and future multiplayer

The project has no player experience/level progression service yet. The XP award
is a recorded camp reward plus a single typed event carrying CampId and Amount.
Subscribe before completion (for example from a progression adapter's OnEnable):

```csharp
camp.ExperienceAwarded += HandleCampExperience;
// void HandleCampExperience(CampExperienceReward reward) { ... }
```

An XP service can later distribute that reward to eligible players/party members.
This milestone does not invent recipient, assist or party-sharing rules, and does
not change CharacterAttributes.Level. CampDebugVisual presents the recorded XP
separately from camp/gameplay logic.

Only authority can initialize or complete a camp. SetAuthority(false) also pauses
owned enemy brains and revokes authority on the reward chest. Set authority before
activation/Start on replicas. Promotion can call TryInitialize if no authoritative
spawn has occurred, or reconciles existing dead members if already initialized.

The local flag is not authentication. A later server adapter must own spawn/death
decisions and replicate Camp ID, Revision, membership/death state, clear state,
XP award and chest network identity. Use match/session ID + Camp ID as the reward
deduplication key, give every scene instance a unique ID, and restore the complete
state before authority migration. Transport, replica spawning/snapshot application,
cross-session persistence and XP delivery acknowledgements are not implemented.

## Validation

Passed through Unity MCP in Play Mode:

- CampValidation.cs: actual three-enemy scene camp, mixed type stats, partial kills,
  duplicate death rejection, clear/XP/chest order, one closed chest at the configured
  transform/table, unchanged inventory before manual opening, PlayerInteractor
  opening and inventory deposit, exhausted chest rejection, reentrant calls,
  disable/enable, chest authority and recipient authority.
- CampEdgeValidation.cs: replica initialization/completion rejection, initialization
  idempotency, authority promotion/cycling, unresolved live despawn, death while
  camp is disabled, deferred completion, no chest respawn after destruction,
  empty configuration and missing chest-point rejection without partial spawns.
- Existing LootValidation.cs: weighted generation, bounds, chest/recipient
  authority, obstruction/range, inventory overflow and exact remainder recovery.

Run CampValidation.cs in a fresh Play Mode session after Start, then
CampEdgeValidation.cs via Unity MCP. Exit Play Mode afterwards: the flow test
temporarily pauses AI/player input and moves the player. Fixtures from the edge
test are removed. Physical keyboard/gamepad combat and balance remain manual checks.

## Files

Added:

- Assets/Game/World/Camps/Camp.cs
- Assets/Game/World/Camps/CampDefinition.cs
- Assets/Game/World/Camps/CampExperienceReward.cs
- Assets/Game/World/Camps/CampDebugVisual.cs
- Assets/Game/World/Prefabs/CampLootChest.prefab
- Assets/Game/Data/Camps/TestNeutralCamp.asset
- Tests/EditorCommands/SetupTestCamp.cs (one-time scene setup; rejects duplicates)
- Tests/EditorCommands/CampValidation.cs
- Tests/EditorCommands/CampEdgeValidation.cs
- Documentation/Milestone11.md and Unity-generated metadata for new assets/folders.

Modified: Assets/Game/AI/EnemyBrain.cs, Assets/Game/World/LootChest.cs and
Assets/Scenes/SampleScene.unity. Earlier milestone changes remain in the working
tree. No assets were deleted; no commit was created.
