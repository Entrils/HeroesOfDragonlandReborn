# M65 First-time onboarding

Choose **Training grounds · Offline** from the main menu. The small arena opens without starting a network session. One objective at a time appears with a gold marker or the relevant control. Health, stamina, mana, level and XP stay visible during practice.

| Station | Practice |
| --- | --- |
| Courtyard | Reach the marker, turn the camera, lock onto the dummy, and land a light and a heavy attack. |
| Guard | Dodge, block a real strike, and parry a telegraphed strike. Recover stamina before continuing. |
| Skills and loot | Learn and cast Fire Projectile to spend mana. Use the lever, open the chest, inspect the bag and equip the Iron Sword. |
| Camp | Defeat a neutral enemy, receive camp XP and spend a level-up point. |
| Fortress | Break the gate and defeat the enemy King. The offline match's King victory finishes training. |

**Esc** pauses training and offers a skip back to the main menu. The menu also offers a first-time skip. Completion and skip are saved locally as separate flags. Only victory sets `TutorialCompleted`, and training remains available for replay.

Unity Build Settings includes `TrainingGrounds.unity` after `DragonlandBattlefield.unity`. Include both scenes when building a normal player. The arena uses the existing offline movement, defense, skills, loot, camp, equipment, progression and match components with short practice encounters.

The Windows development build passed the full offline traversal and the separate pause/skip check. `-hodrTutorialCheck` drives the real gameplay components through all 18 lessons and verifies that victory returns to the main menu. Add `-hodrTutorialSkip` to verify pause, skip and return. These fixtures leave the player's tutorial preferences alone.

Logs are in `Builds/M65-Offline-25af7b7c/` and `Builds/M65-Skip-e05c938a/`. The rendered parry prompt is in `Builds/M65-Visual-7e28436f/Parry.png`.
