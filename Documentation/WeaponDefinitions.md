# Weapon definitions and equipped combat

Weapon data composes with the existing ItemDefinition, inventory, equipment and stable-ID networking systems. No item IDs, ownership rules or RPC payloads changed.

## Initial archetypes

| Archetype | Base damage | Stamina | Speed | Forward reach | Requirement |
|---|---:|---:|---:|---:|---|
| Sword | 25 | 25 | 1.0x | 2.1 m | Strength 8 |
| Greatsword | 40 | 35 | 0.7x | 2.8 m | Strength 16 |
| Spear | 24 | 20 | 1.1x | 3.2 m | Dexterity 12 |
| Staff | 18 | 18 | 0.9x | 2.5 m | Intelligence 12 |

These are initial editable values, not a finalized balance pass. Speed divides windup, attack duration and recovery. Reach sets the forward extent of the server overlap volume; a target is hit when its collider intersects the volume. Spear has a narrower attack volume.

## Authoring

Create a WeaponDefinition and reference it from a weapon ItemDefinition. Each weapon definition contains its archetype, base damage, stamina cost, speed, reach, stat requirements, inactive scaling placeholders, WeaponAttackAnimationSet and weapon prefab. The attack set references a MeleeAttackDefinition for timing, active frames, movement, hit width/height, stagger and heavy multipliers, plus optional light/heavy animation clips.

Four sets and four collider-free placeholder prefabs are assigned. Animation clip slots remain available for authored character animations; current procedural swing presentation remains in use. Staff is a melee weapon in this slice; no spell system was added. Prefab references are exposed through the existing equipment visual hooks; automatic mesh attachment and animator-controller integration are separate presentation work.

Existing IronSword and Dragonblade items now reference Sword. Their GUID identities, item requirements, and equipment modifiers are retained. For example, IronSword deals Sword base 25 plus its existing +10 bonus, while Dragonblade applies its existing +20% bonus. No new numerical attribute scaling is applied, even if a placeholder coefficient is nonzero. Weapon requirements and item-specific requirements combine by taking the stricter minimum for each stat; requirements use base plus allocated attributes and exclude gear bonuses. UI displays the same requirements checked by equipment and combat.

Greatsword, Spear and Staff item entries were added to the existing item catalog. No starter inventory, automatic equipment, loot drops, or world spawns were changed. Use existing inventory/debug tooling to grant the new catalog items for testing.

## Combat and authority

MainHand selects the combat weapon. An empty MainHand retains the existing unarmed MeleeAttackDefinition fallback. An equipped weapon supplies its combat profile, damage, cost, speed and reach; its legacy profile damage/stamina fields are ignored. Weapon-equipped characters can attack without a fallback player attack profile.

The existing server admission, match, team, invulnerability, obstruction and hit-deduplication checks remain. Combat rechecks requirements and validates weapon data before paying resources. A swing pins its weapon/profile, modified damage, timing and hit volume when accepted. Server equipment requests are refused during attacks, dodge, block and stagger. Clients submit intent and stable item IDs, never damage or weapon numeric parameters. Accepted swing duration is replicated separately, so client presentation does not depend on equipment snapshot arrival order. Existing private inventory/resource replication is retained.

## Validation

- 71 passing Play Mode weapon checks cover all archetypes, real light/heavy hits, weapon cost and timing, reach, requirements/UI, unarmed fallback, immutable swings after an offline equipment change, no fallback profile with an equipped weapon, inert nonzero scaling placeholders, invalid-data rejection and non-authoritative rejection.
- Existing EquipmentValidation passed: six slots, requirements, conservation, full inventory rollback, atomic swaps/events, authority, legacy modifiers and real melee damage.
- Existing ExpandedCombatValidation passed all 25 checks for light/heavy attacks, block, guard break, stagger and immunity.
- Item, weapon, attack-set, catalog and loot-table validation passed.
- Separate host/client equipped-weapon run passed 17 checks: server fixture equips the remote player's sword; weapon-derived damage, stable item identity, archetype, accepted swing duration and death agree across peers. The client actively attempted local health/resource changes, a local weapon attack and unequip; all were rejected.
- Final Windows development build succeeded with zero errors and 39 existing Pipeline, deprecated API and shader warnings. A Fusion Editor reload callback assertion encountered during development was resolved with a clean script compile. Final Unity Console inspection reported zero errors.

Manual review remains for weapon balance, placeholder art and feel in a normal match. Attack animation clips and mesh attachment require authored presentation work.

## Modified code and content

- Assets/Game/Items/ItemDefinition.cs: composed weapon reference, combined requirements and validation.
- Assets/Game/Items/Editor/ItemDefinitionValidation.cs: weapon and attack-set validation before builds.
- Assets/Game/Equipment/Equipment.cs: MainHand weapon resolution and requirement rechecks.
- Assets/Game/Equipment/NetworkEquipment.cs: action-state gate for equipment requests.
- Assets/Game/Inventory/Inventory.cs: fail-closed item resolution with missing catalog.
- Assets/Game/Combat/MeleeAttack.cs: equipped parameters and committed attack snapshots.
- Assets/Game/Combat/MeleeAttackDefinition.cs: fallback field tooltips.
- Assets/Game/Combat/NetworkPlayerCombat.cs: replicated accepted swing duration.
- Assets/Game/UI/NetworkActionPresentation.cs: server-duration presentation.
- Assets/Game/UI/InventoryItemText.cs: weapon stat details.
- Assets/Game/UI/InventoryEquipmentPresenter.cs: matching action-state equipment gate.
- Assets/Game/Core/MultiplayerCombatProbe.cs: development-only equipped-client scenario and weapon snapshot reporting.
- Tests/Standalone/RunMultiplayerCombat.ps1: equipped-weapon scenario.
- Existing IronSword, Dragonblade and TestItemCatalog assets: weapon links and catalog entries.
- Existing MainHand combat player prefabs: saved newly woven swing duration field via Unity MCP.

## New files

- Assets/Game/Items/WeaponDefinition.cs and its .meta.
- Assets/Game/Items/WeaponAttackAnimationSet.cs and its .meta.
- Assets/Game/Data/Weapons/: four weapon assets, four attack profiles, four animation sets, with .meta files.
- Assets/Game/Data/Items/: Greatsword, Spear and Staff items with .meta files.
- Assets/Art/Models/Weapons/: four weapon prefabs with .meta files.
- Tests/EditorCommands/SetupWeaponDefinitions.cs.
- Tests/EditorCommands/WeaponCombatValidation.cs.
- Tests/Standalone/VerifyWeaponCombat.py.
- Documentation/WeaponDefinitions.md.

No files deleted. Existing .meta files preserved. No commits created.
