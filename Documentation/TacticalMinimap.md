# Milestone 62 — Tactical Minimap

<!-- HOME-INTEGRATION-2026-11-17 -->
**Home integration update (2026-11-17):** Actual 15-landmark definition/subjects/UI installed and validated. Native match validator and actual opposing-peer normal enemy exclusion with allied bots/death/respawn/reconnect passed; ten disable/reenable cycles cleared marker/UI/scope state. HUD overlap repaired and real 720p captures reviewed. Full camp/boss/gate/spectator/world-icon matrix remains open; optional world icons remain disabled.

See [the live validation record](HomeIntegrationValidation.md) for actual outcomes, failures, evidence and remaining checks. Earlier offline-only status below is historical.

## Delivery and limits

Offline implementation: public map definition/setup, passive subject registry, local authoritative-state adapter, UI Toolkit schematic minimap/compass, optional projected world objective icons, bounded temporary-reveal snapshot cache and tests. No gameplay/map layout/network authority changes. No new minimap RPC, NetworkBehaviour, replicated positions or render-to-texture camera.

**Unity integration remains pending:** this computer has no Unity/MCP. No scene/prefab/map `.asset` or generated metadata was fabricated. Runtime/Editor compilation, Console, UI layout, live camp states and actual host/client privacy have not been tested here. Full Unity integration and visual approval must be performed at home. M52/M53 remain deferred.

## What appears

| Marker | Information source | Presentation |
|---|---|---|
| Local player | Existing admitted local NetworkTestPlayer/NetworkCharacterStats | White `@` plus team letter; position is read only after visibility checks |
| Allied players, including team bots | Same-runner, same-team published player replicas | `oA` / `oB`; dead or suspended avatars hidden |
| Both fortresses | Authored public TeamSpawnArea anchors | `FA` / `FB`, fixed public landmarks |
| Both Kings | Current NetworkMatch.TeamAKingId/TeamBKingId, published King object and replicated Dead | `KA` / `KB`; defeated King gains `x` |
| Neutral camps / boss camp | Public camp anchors; NetworkCamp.State for live status | `C` / `B`, `x` cleared, `?` unknown/pending, `-` faulted |
| Major objectives | Existing authored fortress gate entries; published gate IsOpen/MatchId | `GA` / `GB`; open gate gains `x`. Additional public landmarks can be authored in the definition |
| Temporary revealed enemy | Explicitly supplied authorized position snapshot only | Orange `!`, expires; never follows an enemy Transform |

North is world +Z and stays at the top. The facing indicator shows N/NE/E/etc. and local heading in degrees; `@` identifies the local player without implying a fixed heading. Team letters, different glyphs and state suffixes supplement color. No subtle color-only gameplay information. A permanent legend explains the glyphs; custom production sprites can replace the placeholder glyphs later without changing state/privacy code.

The map is available to an assigned local connection during Playing/Finished, including while dead; the dead local avatar itself is hidden. It hides in lobby/disconnect/suspension/unassigned spectator state. Player motion uses existing rendered replicas; objective status uses existing replicated fields. It never guesses cleared state from a missing enemy/camp or uses a server-private contribution ledger, spawn timer, AI target or loot roll.

Public terrain/region/route rectangles are a schematic background derived in Editor from the existing map's ground renderers. Rotated trail pieces use their axis-aligned bounds, so route drawing is approximate, not a navigation mesh or collision overlay. World X/Z projects with square padding to preserve proportions. Dynamic markers outside the projected map are omitted rather than falsely clamped to an edge. Local marker draws above overlapping markers; co-located objective/fortress glyphs and small-window HUD spacing require visual review.

## Modules

- `TacticalMapDefinition`: ScriptableObject with public center/size, region rectangles and public landmarks. No enemy positions/hidden server state. Existing definition assets are preserved by the builder; reviewed layouts can be edited through MCP.
- `TacticalMapSubject`: passive registry on player/camp/gate prefab roots and scene objects. No update loop or network fields. Registry membership alone never makes a player visible.
- `TacticalMapSource`: samples at 10 Hz, scoped to the bootstrap's running runner/match/round and assigned local actor. Filters opposing players by team before reading their health/coordinates. King discovery uses published match IDs. Camp/gate status matches a public anchor within one meter; absent or ambiguous matches stay Unknown.
- `TacticalMinimapView`: a separate passive UI Toolkit document reusing the existing PanelSettings. No input capture or render camera. Marker labels are reused, capped at 80; public definition budget is 48 landmarks and 128 background rectangles.
- `TacticalMapRules` / `EnemyRevealCache`: engine-independent projection, visibility and reveal lifetime rules. Cache is bounded to 16 targets and a maximum 10 s from observation. Invalid values, wrong audience/scope, expired/future observations and stale/duplicate updates are rejected. Revocation keeps a short-lived tombstone so the same old observation cannot restore the marker.
- `TemporaryEnemyReveals`: dormant local ingress for a future authorized reveal producer. No producer or automatic enemy discovery is installed in M62.

Optional world objective icons are off by default (`showWorldObjectiveIcons` on the view). When enabled they project only the same already-public fortress/King/camp/objective marker positions into the active camera view. They do not project player/reveal markers, do not introduce GameObjects/collision/network data, and can appear through geometry because the anchors are public. Keep disabled if they clutter combat or duplicate existing objective cues; there is no occlusion or offscreen arrow system in this milestone.

## Privacy boundary and future reveals

The minimap never enumerates NetworkEnemy AI or renders scene objects through a minimap camera. Normal player markers take a lazy position reader: the visibility rule rejects enemies before invoking it. The host uses the same client-facing filter despite having authority; there is no host-only reveal shortcut. Published public objectives are intentionally visible to both teams as requested.

`TemporaryEnemyReveals.ReceiveAuthorizedSnapshot` is only a local presentation sink, **not an authorization service**. Future gameplay must decide whether a reveal is allowed on the server and deliver the minimal snapshot only to permitted recipients. A future adapter must verify match/round/team/recipient and freshness, bind to the current local Scope, and translate the server lifetime into the local unscaled clock before calling the sink. Do not broadcast an enemy's continuous coordinates to every client and rely on hiding its icon afterward. No client reveal request RPC or reveal gameplay mechanic is implemented here.

A snapshot contains only an opaque target token, position, observation/expiry times, audience team and current scope. It does not require discovering the enemy GameObject. The renderer shows the last authorized position until expiry; a moving enemy is not tracked between authorized observations. Scope changes (runner/match/round/local avatar/team), disconnect, unassigned state and disabled ingress clear prior snapshots. Revoke a token to remove its marker immediately; newer authorized observations can reveal it again.

This milestone adds no replication or exposure of server-private data. It does **not** retrofit interest management/encryption into existing Fusion replication or claim that a modified client cannot inspect positions already replicated by other gameplay systems. The absence of minimap icons is not a general anti-cheat or fog-of-war guarantee. Actual future reveal transport/privacy must be validated separately when that gameplay is introduced.

## Unity integration through MCP

1. Read AGENTS.md, ArtDirection.md and the current home project/Console. Fix compile/import errors before invoking setup; preserve existing metadata. Existing UI Toolkit HUD is required. M60/M61 are independent runtime systems; follow the home handoff dependency order to avoid partial integration.
2. Open the current `Assets/Scenes/DragonlandBattlefield.unity` in Edit Mode through MCP. Inspect its public ground, spawn areas, camp entries and fortress gate authoring.
3. Invoke `HeroesOfDragonland.Art.Editor.TacticalMinimapSetup.BuildAndInstallLoadedScene()`. It creates only a missing `Assets/Game/Data/Minimap/BattlefieldTacticalMap.asset` from those current public anchors/geometry, adds passive subjects to applicable prefabs under `Assets/Game/Data` and scene objects, wires one bootstrap source/reveal ingress and separate UI document, and rebuilds the existing Fusion prefab table. New components are plain MonoBehaviours. No actor/match/map positions are moved and no reveal producer is installed.
4. Invoke `.Validate()`, inspect Hierarchy/references, map landmark positions/boss classification, sorting/spacing and Console. Save the reviewed scene/assets through MCP. Existing map definitions are not regenerated over authored edits: reconcile them against actual anchors through MCP if the map changed.
5. Start a match with an admitted living local player through MCP. Invoke `TacticalMinimapValidation.Start()`, inspect Status and Console. It checks loaded public markers/Kings/camps, normal enemy exclusion and passive schematic UI; it is not a substitute for the matrix below. No fake enemy reveal is injected into the live game.

## Required acceptance matrix — pending Unity

- Actual host/client on opposing teams: each sees own/allied players and both public fortresses/Kings/camps/objectives; neither gets normal enemy player markers anywhere on the map. Host must obey the same rule. Test a team bot and a third local ally.
- Local move/turn: correct X/Z placement, north +Z, facing cardinal/degree indicator. Check all map edges, non-square bounds, overlapping fortress/King/player markers, 1280×720 and smaller windows. Review overlap with HUD/boss panel; adjust `topOffset` through MCP.
- Camp active → cleared → scheduled pending/respawn: clear suffix comes from replicated State. Missing/unobserved/ambiguous camp remains Unknown. Boss camp remains identifiable independently of current enemy visibility. Gate opened and King defeated reflect their published state.
- Death, respawn, suspend, reconnect, local-avatar replacement, round/match/runner replacement: no ghost ally positions or old reveal snapshots. No local actor/unassigned spectator fails closed. Disable UI/source/reveal ingress and verify cleanup.
- Future reveal integration (not implemented): authorized recipient gets only permitted snapshots; opposing audience receives none at the transport layer. Expiry, explicit revoke, late/replayed data and team/round mismatch must fail closed. Enemy movement after an observation must not move its snapshot marker without a new authorized update.
- Optional world icons: public objectives only, behind-camera/offscreen markers hidden, no player/reveal icons or combat input capture. Leave disabled if readability suffers. Profile marker/UI update cost on a 3v3 client; no added minimap network traffic.

## Files and checks completed here

New: six runtime files under `Assets/Game/UI/Minimap/`, `Assets/Game/Art/Editor/TacticalMinimapSetup.cs`, `TacticalMinimapValidation.cs`, `Tools/Minimap/TacticalMapTests.cs` and this document. Modified for M62: `Documentation/UnityIntegrationHandoff.md`. M60–M62 are included together in the explicitly requested delivery commit. Deleted: none. No existing serialized assets/prefabs/scenes/metadata were rewritten offline. Future home integration commits/pushes still require a separate instruction.

**36 compiled engine-independent checks passed**: hidden-position callback is never invoked for either opposing team, spectator/death/suspend filters, authorized allies, map axes/bounds/aspect, compass, reveal audience/scope/freshness/expiry/replay/revoke/renewal and bounded spam. Compile `TacticalMapRules.cs` plus `Tools/Minimap/TacticalMapTests.cs` as a console program to reproduce. These results do not establish full Unity compilation, networking security or visual correctness.
