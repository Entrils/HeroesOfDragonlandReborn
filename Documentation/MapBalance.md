# Milestone 89 — Competitive map balance audit

**Map** `Assets/Scenes/DragonlandBattlefield.unity`. Measurements use the scene's saved NavMesh, static colliders, six legal spawn markers per team, and the match prefab's current balance and fortress definitions. Times are travel estimates at the player prefab's 5 m/s, without fighting, stamina actions, live opponents, or waiting at a blocked route. The independent ground grid uses NavMesh-tested links. For closed-gate checks it adds the actual gate box plus player clearance and the existing hostile spawn-zone restriction. Eye-height rays are 1.6 m above the sampled ground. They measure collider obstruction and do not model visual foliage or player spotting.

## Spawn, boss, and respawn

| Route or measure | Team A | Team B |
| --- | ---: | ---: |
| Spawn to center, baked polygon path, six markers | 8.62–9.34 s, mean 8.99 s | 8.65–10.38 s, mean 9.69 s |
| Spawn to center, independent 0.5 m ground grid | 8.92?9.57 s | 8.98?9.68 s |
| Fastest routed approach via own north ridge end | 10.17 s | 8.77 s |
| Fastest routed approach via own south ridge end | 8.74 s | 10.09 s |
| Respawn marker to own King | 2.40–3.14 s | 2.37–3.11 s |
| Respawn marker to own main gate threshold | 2.30–2.92 s | 2.30–2.92 s |
| Level 1 opening death to center, best polygon route | 13.62 s | 13.65 s |
| Level 10 death at 20:00 to center, best polygon route | 30.62 s | 30.65 s |

The polygon-path mean favors A by 0.70 s. The independent half-metre grid reduces the paired marker difference to 0.06?0.12 s. Rotated routes via the ridge ends are comparable, so the polygon mean is not sufficient evidence to move either spawn. The center is the Boss Knight site. It spawns at 2:00 and has no straight eye-height line from any spawn marker. Both teams' best arrival is within 0.03 s, and the approach lanes rotate with team side. The current server profile gives both teams 5.00 s at opening level 1, 11.33 s at 10:00 and level 5, and 22.00 s at 20:00 and level 10. Both receive 3.00 s of protection. Defenders on either team can return to their King much sooner than a new attacker can travel there from center. The values in the older balance report describe an earlier configuration.

## Camps, rewards, and retreat

Camps are numbered in authored spawner order. Coordinates are world X/Z. Entries are the best estimate across the six spawn markers. Return columns run the route toward the nearest **own** marker for the indicated team. These are fallback route estimates and do not imply access through an opponent's protected spawn zone.

| Camp | X/Z | A arrival | B arrival | A return | B return | To center |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| C1 home | −33 / 18 | 4.34 | 14.20 | 4.34 | 14.20 | 8.09 |
| C2 home | −33 / −18 | 2.43 | 15.32 | 2.43 | 15.47 | 7.87 |
| C3 home | 33 / −18 | 14.19 | 4.33 | 14.19 | 4.33 | 8.01 |
| C4 home | 33 / 18 | 15.34 | 2.41 | 15.34 | 2.41 | 7.88 |
| C5 flank | −10 / 34 | 9.64 | 10.86 | 9.52 | 10.80 | 7.63 |
| C6 flank | −10 / −34 | 8.00 | 11.98 | 8.00 | 12.14 | 7.25 |
| C7 flank | 10 / −34 | 10.85 | 9.65 | 10.79 | 9.65 | 7.63 |
| C8 flank | 10 / 34 | 12.29 | 8.00 | 12.00 | 8.00 | 7.25 |
| C9 boss | 0 / 0 | 8.62 | 8.65 | 8.62 | 8.65 | 0.00 |
| C10 high | 0 / 18 | 8.76 | 7.57 | 8.76 | 7.57 | 3.60 |
| C11 high | 0 / −18 | 7.55 | 8.74 | 7.55 | 8.89 | 3.60 |

| Camp class | Sites | First spawn | XP per eligible winner, early / late | Repeat, early / late | Chest draws per clear |
| --- | ---: | ---: | ---: | ---: | ---: |
| Home | 4 | 0 s | 25 / 25 | 75 / 75 s | 1 |
| Flank | 4 | 0 s | 60 / 120 | 105 / 75 s | 1–2 |
| High | 2 | 150 s | 120 / 180 | 165 / 120 s | 2–3 |
| Boss | 1 | 120 s | 360 / 540 | 210 / 150 s | 4–5 |

Late pacing starts at 10:00. A map half has two home and two flank camps, giving both halves the same nominal 109 early or 232 late camp XP per minute per eligible recipient if every camp clears immediately on respawn. The two high camps together are about 87 early or 180 late XP/min after unlocking. The boss alone is about 103 early or 216 late XP/min after unlocking, before separate kill and assist XP. These are timer ceilings, not observed farming rates. Clear time, contested contribution, reward radius, and catch-up can change actual income.

**Safe farming.** Home camps C2 and C4 are the safest measured opening farm routes. The owner arrives or retreats in roughly 2.4 s, the other side takes over 15 s from spawn, and no other camp has a direct surveyed eye-height line to them. C1 and C3 have the same cover pattern and a 4.3 s fallback. They are outside the protected zone and can be invaded.

**Fast rotations.** The two outer lanes are the best measured farm circuits. The paired same-edge flank crossing is 4.00 s either way. Its nearest high camp adds 3.78–4.06 s, then the boss is 3.60 s away. The home-to-flank connection is 7.20–7.24 s. Rotating from one team's north flank farm to the same team's south flank farm takes 13.73 s. A full 55-pair camp matrix is available in the editor audit.

**Team-specific edges.** Team A reaches southern high camp C11 about 1.19 s earlier from spawn. Team B reaches northern high camp C10 about 1.19 s earlier. Their closest home retreats differ by 0.02 s. Neither team has a denser home farm or a materially faster best boss route.

## Fortress approaches, sightlines, and low-use ground

| Attacked fortress / route | Center to entrance | Entrance to King, baked route | Static opening | Defender's best return to entrance |
| --- | ---: | ---: | ---: | ---: |
| A main | 7.59 s | 3.24 s | 5.90 m | 2.30 s |
| A north sally | 7.37 s | 2.00 s | 6.00 m | 3.24 s |
| A south sally | 7.19 s | 4.19 s | 6.00 m | 1.47 s |
| B main | 7.66 s | 3.26 s | 5.90 m | 2.30 s |
| B south sally | 7.33 s | 1.99 s | 6.00 m | 3.24 s |
| B north sally | 7.21 s | 4.18 s | 6.00 m | 1.47 s |

The spawned gate is 5.80 m wide across the 5.90 m main opening and seals that approach for a player capsule. The King-side sally is the dominant measured way around a closed main gate. The spawn-side sally is an alternate retreat exit for defenders. Its naive route to the King crosses the protected footprint, which is why closed-gate tests apply the actual spawn entry restriction. A closed gate is a deliberate choke on the central approach. The closed-gate ground grid found both sally routes legal on either team. The King-side port reaches the King in 2.13 s from either side. The spawn-side port takes 4.46 s from either side after avoiding the protected footprint. A coarser 1 m grid missed some of the fast route on Team B, which is why the final check uses a half-metre grid.

**Sightlines and chokes.** From center the two high camps are visible along the north/south axis. Each high camp also sees the two outer camps on its edge. The low camp anchors see none of the other camp anchors. Both fortress entrances and the four surveyed ridge ends lose a direct view of center. The ridge-end spans measured 8 m on each mirrored lane with the same capped lateral ray. Those bends concentrate entry into the center, with an exit on either side, and can break sight on retreat. A measured clear lane can still be held by live actors.

**Dead zones.** A 3 m ground-plane sweep found 717 sampled walkable points, all connected to center on the baked NavMesh. Seventy-four are more than 18 m from any camp or King, concentrated along outer corners and between the central ridges and fortress fronts. These are low-reward walking areas and covered rotation space. The sample cannot rule out smaller inaccessible pockets or evaluate combat pressure around the edges.

## Bot evidence and reproduction

The existing six-bot opening run on the same saved Battlefield layout recorded nine Team A kills and ten Team B kills, seven camp clears, and 17 generated/claimed items in a 55-second sample. Both teams used camp, chest, guard, and gate goals. The sample ended before the boss spawned or a fortress fell, and is suitable for checking opening activity, not estimating win rate or proving live siege timings.

In Unity Edit Mode, open only Dragonland Battlefield and run **Heroes of Dragonland → Map → Measure Competitive Battlefield**. It prints spawn samples, camp rewards and the full pairwise travel matrix, occlusion checks, approach widths, the protected spawn/closed gate model, and low-density samples. Results distinguish baked polygon paths from sampled-grid routes.


## Handoff at M89(half)

Unity's saved-scene validation passed the route, enemy-anchor, and respawn-ramp checks. The measurements did not justify a layout change. The Battlefield scene, NavMesh bake, team spawn points, rewards, and biome geometry were left intact.

The remaining checks are human 3v3 route timing, live defense of the two sally approaches, and a full boss/siege match. For model work, re-run this audit after any changes to colliders or the NavMesh bake. Keep the ridge-end connections, sally openings, and home-camp sightline breaks covered by the measurements above.

New files are this report, `Assets/Game/World/Editor/CompetitiveMapAudit.cs`, `CompetitiveMapGrid.cs`, and their Unity metadata. No assets were deleted.
