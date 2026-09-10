# Milestone 13 — session character attributes

Implemented Vitality, Endurance, Mind, Strength and Dexterity using the existing
CharacterAttributes and SessionProgression components. No skill tree or persistence.

## Runtime contract

- TryGetUnallocatedBaseValue returns the configured innate attribute.
- GetAllocatedPoints returns the integer session investment.
- TryGetBaseValue returns base + allocation (equipment requirement contract).
- TryGetValue returns max(0, (base + allocation + equipment flats) * (1 + equipment percentages)).
- GetEquipmentModifier returns final minus base + allocation, including penalties.
- SessionProgression.TryIncreaseAttribute(attribute, sessionId, out reason) spends
  exactly one point for +1. Rejects insufficient points, invalid attributes,
  stale sessions, disabled progression, missing authority and reentrant requests.
  Observers see both the allocation and deducted balance committed together.
- New match IDs clear allocations alongside XP/level/points. Reusing the same ID
  preserves them. Equipment is still managed by its existing inventory lifecycle;
  attribute reset neither deletes nor unequips items.

Requirements use base + allocated points, excluding equipment bonuses, preserving
Milestone 9's rule against equipment qualifying itself or circular gear dependencies.
Level requirements still read actual session level. Legacy Intelligence and enum
numeric values remain compatible with existing assets; Intelligence is not spendable.

## Resource formulas and configuration

Defaults: base 10 for each attribute; +10 health per Vitality above its innate base,
+5 stamina per Endurance, +5 mana per Mind. Starting maxima remain CharacterStatsConfig
values (100 by default). Formula:

    maximum = startingMaximum + (finalAttribute - innateAttribute) * resourcePerPoint
    maximum = (maximum + directEquipmentFlats) * (1 + directEquipmentPercentages)

Health maximum is at least 1; stamina/mana are at least 0. Max changes retain current
amounts, clamp on decrease, preserve regeneration delays, and never heal or revive.
Explicit ResetToFull remains the spawn/restore operation.

Create an optional Heroes of Dragonland/Attribute Progression ScriptableObject and
assign it to CharacterAttributes to tune all five bases and three resource rates.
Without an asset, legacy serialized bases remain and new attributes default to 10.
Existing scene components work without YAML edits or additional scene components.

In Play Mode, Player's SessionProgressionDebug provides Grant Debug XP and Spend Point
On Attribute context menus; choose Attribute in its Inspector. No player-facing UI yet.

## Validation status

All runtime C# compiled against the installed Unity 6 and project package assemblies
using Unity's Roslyn compiler: no errors (serialized-field CS0649 warnings only).
Unity MCP was not available in this session: live Console, Hierarchy and Play Mode
checks could not be run. AttributeValidation.cs is prepared for Unity MCP in Play Mode;
it covers spending, stale/invalid requests, authority, reentrancy, actual equipment
requirements and flat/percent modifiers, capacity updates, no healing/revival, and
inactive/new/same-session reset. Run existing EquipmentValidation and
ProgressionValidation as regressions. Runtime validation remains pending.

## Files

Modified: Assets/Game/Core/CharacterStats.cs, ResourcePool.cs;
Assets/Game/Equipment/CharacterAttributes.cs, Equipment.cs, IEquipmentRequirementSource.cs;
Assets/Game/Items/ItemTypes.cs;
Assets/Game/Progression/SessionProgression.cs, SessionProgressionDebug.cs.

Added: Assets/Game/Progression/AttributeProgressionDefinition.cs and metadata;
Tests/EditorCommands/AttributeValidation.cs; Documentation/Milestone13.md.

No assets deleted, scenes changed, generated project files edited, or commits made.
SessionProgression owns allocation state to make point spending and match reset atomic;
CharacterAttributes calculates values; CharacterStats manages live resource pools.
