# Neutral camp tiers

DragonlandBattlefield uses the existing NetworkCamp lifecycle and LootTable weighted draws for every tier. Difficulty is metadata on NetworkCampDefinition; no additional camp controller is introduced.

| Tier | Camps | Enemies | XP | Drops | Rarities | Respawn |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 4 home camps | Two wolves, 40 HP / 6 damage each | 25 | 1 | Common / Uncommon | 75s |
| 2 | 4 flank camps | Bear and wolf, 80 HP / 10 damage each | 50 | 1–2 | Uncommon / Rare | 105s |
| 3 | 2 mirrored inner camps | Two knights and bear, 120 HP / 14 damage each | 100 | 2–3 | Rare / Epic | 150s |
| Contested | Map center | One 1,800 HP neutral Stone Warden boss | 300 | 3–4 | Epic / Legendary chance | 210s |

The contested camp opens at 2:30. Other camps start immediately. XP uses the existing living-player proximity rule with a 9m radius. Respawn timers start on clear, as before.

Tier 3 anchors are (0,0,18) and (0,0,-18); the contested anchor is (0,0,0). Existing home and flank positions are preserved.

Loot uses the existing item catalog. The boss table gives Epic entries a multiplier of 1 and Legendary entries 0.1. With current entry weights (Epic 15, Legendary 5), Legendary probability is 1/31 per draw, approximately 10.8% per chest averaged across 3–4 draws. Draws are independent with replacement; a Legendary is never guaranteed.

Apply or reapply using **Heroes of Dragonland > Battlefield > Apply Camp Difficulty Tiers** with the battlefield open in Edit Mode. This updates existing assets and preserves their GUIDs. The boss uses a separate data/prefab variant of the existing knight, so its tuning does not affect Tier 3. See [NeutralBoss.md](NeutralBoss.md) for attacks and validation.

Validation: `Tests/EditorCommands/CampDifficultyValidation.cs` invokes the compiled Editor validator. It checks tier distribution, increasing XP, assigned references, catalog compatibility, 8,000 seeded loot rolls, both permitted rarities being reachable, and every enemy/chest anchor on the NavMesh.
