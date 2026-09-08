# Milestone 6: item definitions

Create assets through Create > Heroes of Dragonland > Item Definition. Each item
stores its display name, description, optional Sprite icon, category, rarity,
positive maximum stack size, nonnegative integer value, optional equipment
requirements and optional stat modifiers. Empty lists mean no requirements or
modifiers. Icons on the sample items are intentionally unassigned.

Requirements describe minimum stat values (all must be met). Modifier operations
are Flat and AdditivePercent (0.1 = +10%). The intended later calculation is
(base + sum of flat modifiers) * (1 + sum of percent modifiers). Entries describe
data only; this milestone does not apply equipment, healing, or stat changes.
The current CharacterStats resource implementation remains independent. Level,
attributes, armor and damage stat identifiers reserve names for later equipment
integration; they do not add these systems now. Enum numbers must remain stable.

## Identity and serialization

The editor importer bakes the asset's GUID into ItemDefinition.Id. It is a
32-character lowercase string that is available in builds without UnityEditor.
Preserve .meta files: moving/renaming preserves identity; duplicating the asset
assigns a new identity. Do not edit IDs manually or regenerate published metadata.
Runtime clones represent the same definition, not a new item identity.

Send/save the ID string, such as {"itemId":"..."}, rather than a Unity instance ID,
asset name, array index, or serialized ScriptableObject. ItemCatalog.TryGet resolves
the ID against local definitions; unknown and ambiguous IDs return false. Both
peers must ship matching content/catalogs. Inventory instance IDs, quantity,
ownership, transport, server authority and content version negotiation belong to
later milestones. Assign/load the catalog explicitly in the future consumer so
Unity includes it in builds; no Resources singleton or scene manager is added.

## Samples and validation

Assets/Game/Data/Items contains IronSword (Common Weapon), ScoutArmor (Uncommon
Armor), ManaPotion (Rare Consumable), DragonScale (Epic Misc), Dragonblade
(Legendary Weapon), and TestItemCatalog. Equipment examples include level
requirements, flat stats, and an additive percentage modifier.

Tools > Heroes of Dragonland > Validate Item Definitions checks every definition
and catalog. The same validation runs before builds, rejecting invalid fields,
missing catalog entries, duplicate IDs and identities inconsistent with metadata.

Tests/EditorCommands/ItemValidation.cs is a repeatable Unity MCP RunCommand script
outside Assets. Checks passed for category/rarity coverage, JSON ID roundtrip and
catalog lookup, stable reimport identity, unknown/duplicate rejection, invalid
stack sizes, values, enums and nonfinite modifiers. Project compilation and a
Play Mode catalog lookup smoke check passed; Console contained no warnings/errors.
