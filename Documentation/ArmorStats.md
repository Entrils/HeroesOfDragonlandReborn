# Armor stats and equipment weight

ItemDefinition uses the existing StatModifiers list for armor bonuses. Armor (serialized stat 6) is physical defense; MagicalDefense (12) is a placeholder with no damage channel yet. MaxHealth, MaxStamina, MaxMana and attribute modifiers support flat and additive percent bonuses. Equipment sums flat and percent separately: `(base + sum(flat)) * (1 + sum(percent))`, clamped to zero. CharacterAttributes applies attribute-derived resource gains before direct resource modifiers, using AttributeProgressionDefinition. Equipping never heals; removing gear clamps current resources to the new maximum.

All six equipped slots contribute ItemDefinition.Weight, including weapons. Inventory contents do not contribute. DefaultArmorRules is assigned to the four existing equipment-bearing player prefabs. Assign an ArmorRules asset to Equipment on new actors. Actors without one use the same initial defaults. Scout Armor weighs 10.

Default classes and dodge multipliers:

| Class | Total weight | Distance | Duration | Recovery | Stamina cost |
| --- | --- | --- | --- | --- | --- |
| Light | below 15 | 1 | 1 | 1 | 1 |
| Medium | 15 to below 30 | .85 | 1.1 | 1.25 | 1.2 |
| Heavy | 30 or above | .65 | 1.25 | 1.6 | 1.5 |

Thresholds, multipliers and defense scale are configurable in DefaultArmorRules. Base dodge values and invulnerability fractions remain in DefaultDodge. Invulnerability fractions follow the adjusted roll duration. A roll captures weight modifiers at its start; equipment changes cannot alter that roll. Networked roll multipliers replicate for matching visual duration.

Physical damage is reduced in CharacterStats.ApplyDamage after blocking and before resource removal: `incoming * defenseScale / (defenseScale + physicalDefense)`. Default scale 100 gives 50% reduction at 100 defense. Zero defense preserves damage; invulnerability and authority checks remain enforced. Existing ApplyDamage calls are treated as physical. Magical defense is displayed/aggregated but does not affect damage yet.

Validation: Tests/EditorCommands/ArmorSetup.cs creates/binds the data through Unity MCP and checks thresholds/formula. ArmorValidation.cs runs isolated Play Mode checks for equip/unequip, defense, resource and attribute bonuses, no free healing, and roll cost/distance/recovery in each weight class. Network two-client roll presentation still needs a manual session check.
