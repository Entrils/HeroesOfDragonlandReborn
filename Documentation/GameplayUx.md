# M64 Gameplay UX

The Battlefield HUD presents **Kill the enemy King** as the primary objective. The secondary summary explains why to clear camps, gain XP and find equipment, defeat the contested boss, and weaken the enemy fortress. It appears at the start of a round and during play.

| First-match moment | Feedback |
| --- | --- |
| Ready and spawn | The HUD shows the ready/start flow and the round countdown. |
| Try target lock | The lock panel shows the target, health, and switch/unlock controls. A failed acquire suggests aiming at a nearby enemy. |
| Run short of stamina | A warning appears under the stamina bar and clears after recovery. |
| Try a skill without enough mana | The skill card and authoritative result show the amount needed and available. |
| Clear a camp | Nearby players receive a completion notice. The camp shows when its chest is available and when the camp returns. |
| Approach loot | The interaction prompt appears only in reach, with a clear line to an unopened chest. A failed claim explains when the bag needs space. |
| Inspect or equip loot | The bag shows capacity, and the item details list missing requirements and their shortfalls. A selected bag stack can be discarded with an explicit confirmation. |
| Gain a level | A brief notice shows the new level and points to Skills. |
| Defend the King | Damage shows a warning beside the King bars on the defending team's HUD. |
| Die and respawn | The HUD explains the respawn timer or spawn wait and what is retained. Round end replaces the respawn state with the result. |

Interaction prompts and brief status messages share the main HUD. The status area displays one message at a time, with one deferred progress notice for closely spaced events. Combat HUD prompts are suppressed when modal input is blocked.

## Acceptance

The Windows development build passed the standalone HUD, respawn and inventory fixtures on host and client. They cover two rounds with local HUD rebinds, objective visibility, low stamina, team-specific King alerts, target lock, respawn, requirements, inventory space and discard confirmation. Logs are in `Builds/LifecycleAcceptance-14925b6b/`, `Builds/RespawnAcceptance-9f9b9353/` and `Builds/InventoryUiAcceptance-afd29a86/`.

A rendered first-round frame at 1280 × 720 is in `Builds/M64-Capture-224bfb01/Battlefield-HUD.png`. The remaining visual walkthrough covers camp-to-chest feedback, low-mana skill cards, and notice placement at smaller resolutions.
