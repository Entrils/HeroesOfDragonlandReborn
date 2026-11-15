# Milestone 47 - Stone Warden neutral boss

The central contested camp in DragonlandBattlefield now spawns a two-phase Stone Warden. The encounter uses the existing shared EnemyBrain, navigation, perception, melee/area attacks, CombatDefense/CharacterPoise, NetworkEnemy and NetworkCamp reward lifecycle. BossEncounter is a small shared-brain extension; no second complete AI controller was introduced.

## Encounter design

Fixed 1800 HP, 250 poise, 2.8 m/s chase (3.5 m/s when enraged), 4m attack initiation range, 10m detection, 14m home leash. The baseline is intended for 2-4 players, with readable attacks and recovery windows allowing longer solo attempts. There is no encounter party-size check, simultaneous-player mechanic, damage immunity by phase, or forced team ownership. Timing/balance still needs human playtesting; the automated network fixture is not a difficulty test.

Existing central camp scheduling is retained: opens at 2:30, respawns 210 seconds after clear. One boss per camp cycle.

| Phase | Attack | Shape | Windup | Damage | Recovery |
| --- | --- | --- | --- | --- | --- |
| 1 | Cleave | 4.5m / 70 degree cone | 1.1s | 28 | 1.4s |
| 1 | Heavy Sweep | 5.5m / 180 degree sector | 1.5s | 48 | 1.4s |
| 1 | Stomp | 5m circle | 1.8s | 36 | 1.4s |
| 2 | Frenzied Cleave | 4.5m / 80 degree cone | 0.85s | 34 | 1.1s |
| 2 | Crushing Blow | 5.5m / 80 degree cone | 1.5s | 55 | 1.7s |
| 2 | Seismic Stomp | 5m circle | 1.4s | 42 | 1.1s |
| 2 | Shockwave | 7m circle | 1.8s | 46 | 1.1s |

Values are before player mitigation. Boss attacks apply 75 poise damage through shared melee defense. The boss resists isolated ordinary stagger hits, but accumulated coordinated poise damage can interrupt it; it is not arbitrarily immune to combat defenses.

At <=50% health, the boss completes its currently committed attack, then enters a 2.5-second vulnerable enrage transition without attacking. A gold expanding ring, body tint and HUD warning announce the transition. Phase 2 restarts its four-attack pattern, increases chase speed by 25%, and remains latched even if health later rises. No hidden damage pulse accompanies the roar.

Attack patterns and phase values are ScriptableObjects. Each accepted attack snapshots its set, index, origin, facing and start tick; moving the target or crossing the phase threshold never redirects or resizes its advertised area. Clients receive both current phase and committed attack phase so a phase update cannot reinterpret an earlier telegraph. Area attacks deduplicate victim colliders and check visibility/target legality at impact. Moving outside the sector/radius avoids damage.

The boss switches targets about every seven seconds, between committed attacks. It prefers the most recent eligible challenger other than its current target, otherwise another nearby visible eligible player. Repeated hits cannot continuously snap its focus or windup. Dead/invalid targets, targets leaving the leash, and sustained lost visibility try another valid challenger before evading. A full wipe or leash return resets health, phase, attack pattern and pending challenger. Return-home invulnerability prevents free damage against a resetting boss. Once home, a new solo or group attempt starts in phase 1. Ordinary enemies keep their existing behavior.

## PvPvE and rewards

The boss has no player team or fortress guard affiliation. Both admitted teams can damage and attract it. Existing PvP damage remains enabled; the encounter creates no truce, protected arena or team lock.

The boss camp retains server-owned state. Only authoritative confirmed death clears the camp, once per cycle. Completion state is committed before XP, chest spawning and observers. Existing XP event IDs include camp network ID and completion count; repeat completion and repeated XP grants are rejected. A despawned live member does not count as a kill.

The exclusive gold **Warden Reliquary** spawns at the central boss camp after death. Its dedicated table provides 4-5 Epic/Legendary draws, with higher Legendary weighting than the ordinary boss chest. Quantities and item definitions use the existing validated item catalog. It is one contested chest, not a chest per participant. First accepted server claim wins the whole roll. Full-inventory failures retain the same server-only roll for retry, without claiming the chest or rerolling it. The existing atomic chest transaction commits open state before inventory observers, denying duplicate, reentrant and competing claims. Neither team owns the chest. Nearby living players of either team receive the existing 300 XP reward within 20m at completion.

Respawning creates a legitimate new camp cycle and reward event. Match cleanup clears the round's objects through the existing lifecycle. No new persistent reward currency or permanent boss unlock was introduced.

## Presentation and authoring

BossHealthPresenter discovers nearby same-runner boss instances using the BossEncounter lifecycle registry. GameplayHudView provides a separate centered boss panel with name, health, phase/enrage and current attack. Target lock is not required. The panel hides on encounter exit, death, invalid session and inactive gameplay. Narrow layouts move the panel below the top HUD columns. Body tint and warning geometry use replicated state and remain presentation only. Dedicated character/attack animations and audio still use the existing prototype art; this milestone delivers encounter mechanics and readable cues.

BossEncounterDefinition is saved as `Data/Battlefield/StoneWardenEncounter.asset`. Phase one retains `NeutralBossAttacks.asset`; phase two uses `WardenPhaseTwoAttacks.asset`. Existing BossKnight definition/prefab/stats are upgraded in place, preserving GUIDs. The battlefield scene adds BossHealthPresenter to its existing HUD. No scene geometry was removed.

`Heroes of Dragonland > Battlefield > Configure Neutral Boss` saves encounter assets, prefab, camp and HUD, rebuilds Fusion registration, and force-reimports configured Fusion config assets. The explicit import fixes stale prefab registration in development builds; it also registers the prior Archer/Mage prefabs. EnemyArchetypeSetup now uses the same explicit config refresh. Authoring changes use Unity MCP/Editor APIs, not hand-edited serialized YAML.

## Verification

`Heroes of Dragonland > Battlefield > Validate Boss In Play Mode` exercises boss assets, both teams damaging boss, committed telegraphs, deliberate aggro switching, all phase-two attacks, sector/area hit rules, evasion, poise resistance/break, PvP in both directions during combat, replacement of a lost target, wipe/health reset, protection during return home, a fresh solo attempt, authority denial, death, exclusive loot rarity and value, atomic first claim, competing/reentrant/repeated claims, and boss HUD show/hide.

Final Play Mode result: **63 boss assertions passed**, plus all **35 shared archetype regression assertions**. Temporary test actors and NavMesh data are removed in finally. The boss health HUD was captured and visually inspected in `Builds/Milestone47/boss-hud.png`.

A Windows development build succeeds with zero errors. Build summary: `Builds/Milestone47/build-result.txt`. Existing obsolete API/package and optional Pipeline configuration warnings remain.

The opt-in development `-hodrBossCheck` probe uses a real host/client room with four roster-filling bots. It fast-forwards the boss spawn delay and health budget and keeps acceptance actors alive during the boss phase, then uses real owning-player spell requests to damage the neutral boss from both teams, cross its phase threshold and land the lethal hit. Normal owning-player chest requests compete for the real server reward. Checks cover replicated phases/HUD, passive client boss simulation, replica stat/camp/loot mutation rejection, one server death/completion/linked chest, one atomic inventory revision, repeated XP denial, and owner-request PvP damage. The fixture is explicitly not enabled in ordinary play and is not used to claim balance validation.

Final host/client logs: `Builds/BossAcceptance-0ea0fae4/host-check.log` and `client-check.log`, also referenced by `Builds/m47-acceptance-processes.json`. Both peers logged `[BossNetwork] PASS` on the final build. Acceptance players were stopped after verification. Final build: succeeded, zero errors, 98 warnings. Final Editor Console and saved component/reference checks are performed through Unity MCP.

## Files changed for Milestone 47

New runtime: `AI/BossEncounter.cs`, `AI/BossEncounterDefinition.cs`, `AI/BossNetworkProbe.cs`, `UI/BossHealthPresenter.cs` and Unity metadata.

Modified runtime: `AI/EnemyBrain.cs` (phase and target hooks), `AI/EnemyDefinition.cs` (optional boss profile), `AI/EnemyMeleeAttack.cs` (committed phase attack set and pattern reset), `AI/NetworkEnemy.cs` (replicated phase/committed phase), `UI/BossTelegraphPresentation.cs`, `UI/NetworkEnemyPresentation.cs`, `UI/GameplayHudView.cs`. `World/Camps/NetworkCamp.cs` adds only an opt-in development fixture entry to bypass the spawn schedule; production reward logic is reused unchanged.

Modified authoring/validation: `World/Editor/NeutralBossSetup.cs`, `NeutralBossValidation.cs`, `GameplayHudSceneBuilder.cs`, and `AI/Editor/EnemyArchetypeSetup.cs` (config reimport).

Modified saved content: `Data/Battlefield/BossKnight.asset`, `BossKnightStats.asset`, `BossKnight.prefab`, `NeutralBossAttacks.asset`, `BossCamp.asset`, and `Assets/Scenes/DragonlandBattlefield.unity`.

New saved content: `StoneWardenEncounter.asset`, `WardenPhaseTwoAttacks.asset`, `WardenReliquary.prefab`, `WardenReliquaryLoot.asset`, `WardenGold.mat`, their metadata, and this document. No assets deleted; no commit created. Existing Milestone 45/46 working-tree changes are preserved.
