# Milestone 61 — Dynamic Music System

## Delivery and status

Ten adaptive states, local context sampling, configurable release tails, a two-deck crossfader integrated into M60, a separate music ScriptableObject catalog, ten original synthetic music WAV fixtures, setup tools and validation. No music playback/state is networked. No gameplay/authority/map layout changes.

This is an **offline implementation awaiting Unity integration**. There is no Unity/MCP on this computer. Unity compilation, Console/imports, actual catalogs/scenes/prefabs, native fades, listening, real local contexts and host/client acceptance have not been run. No serialized Unity YAML or fake metadata was created. M52/M53 remain deferred; production music is still to be supplied.

## Selection rules

The director samples existing rendered gameplay state every 0.2 seconds on its own bootstrap/runner. Alive actors register through a small passive `MusicContextSubject` component; there are no per-frame scene-wide searches or new network messages. Distances are planar and configurable on `LocalMusicContext`. Default thresholds are 18 m for ordinary combat, 36 m for bosses and 24 m for objective assault.

| State | Trigger | Exit / priority |
|---|---|---|
| Lobby | No active match, lobby, preparation, return to lobby | Lifecycle gate |
| Match Start | Live transition into Playing or observed new round | 3 s intro; urgent combat can override |
| Exploration | Playing, no higher context | Fallback |
| Neutral Camp Combat | Engaged non-boss neutral enemy targeting local player, or nearby while local player attacks/takes damage | 4 s release tail |
| PvP Combat | Hostile player within 18 m plus that player's attack or local attack/recent damage | 6 s release tail |
| Boss Encounter | Engaged boss or boss phase transition within 36 m | 6 s release tail |
| Fortress Assault | Nearby engaged hostile fortress guard, or recently damaged gate/King within 24 m | 6 s release tail |
| King Danger | Own alive King took damage in last 2 s; also critical King (≤35% HP) with an attacking hostile player nearby | 8 s release tail; applies to defenders throughout the map |
| Victory | Finished match, assigned local team matches confirmed Winner | Result gate, non-looping by default |
| Defeat | Finished match, assigned local team differs from confirmed Winner | Result gate, non-looping by default |

Within Playing:

```text
King Danger > Boss Encounter > Fortress Assault > PvP Combat
            > Neutral Camp Combat > Match Start > Exploration
```

Finished-match results and lobby lifecycle gate override combat. An unassigned spectator gets Lobby at match end, never a fabricated defeat. Dead/suspended/missing local actors clear ordinary combat tails; own-team King danger can remain relevant during respawn. Joining/reconnecting into Playing resolves the current context without replaying a historical Match Start intro. Joining a finished match can resolve its current result music; M60's one-shot victory/defeat cue still follows its own live-event rules.

Boss music requires active aggro/phase transition; passing an idle boss does not trigger it. Walking into a friendly fortress alone is insufficient for Assault. Neutral combat uses replicated neutral NPC identity/activity because camp membership is not exposed per enemy on remote clients. Nearby-fight/PvP classification is a presentation heuristic, not an authoritative combat log: an attack near a hostile player can count even when its intended target differs. Health snapshots can coalesce damage/healing. These choices avoid adding network traffic solely to drive music. Tune radii after testing the actual camera/map; no walls/occlusion inference is claimed.

Round, runner and match identity changes discard old leases. Health sampling seeds its initial value and resets on round restoration/respawn rather than treating old damage as a fresh event. Timer release tails prevent rapid Exploration/Combat flapping between attacks. Future local encounter/zone adapters can call `DynamicMusicDirector.Signal(MusicState, seconds)`; only combat/danger states are accepted, with a maximum 30 s lease and lifecycle/local-life gates retained. This hook does not alter server state or guarantee exact musical beat alignment.

## Playback and transition ownership

`GameAudioService` preallocates two Music sources plus one Ambience source alongside the existing one-shot pool: **27 sources at the default 24-voice capacity**. Music uses the existing Music mixer group, so Master/Music sliders and persisted preferences from M60 apply. No new mixer or parallel music pipeline. Batch/headless execution allocates no audio sources.

`MusicDecks<AudioClip>` provides linear crossfades, default 1.25 s, using unscaled time. Both sources remain 2D, pitch 1, no Doppler, no new sources per transition. At stable playback there is one music source; overlap is bounded to two. Music native voice priority is 128 so important M60 combat/warning sources can take precedence under voice pressure. Global music catalog gain defaults to 0.35; normalize future tracks and listen for masking.

- Repeated state requests and states sharing the same clip retain playback/time position. Loop-mode changes update the existing deck without replaying it.
- A reversal during a fade reuses the still-playing outgoing track from its current position/gain.
- A third distinct clip requested mid-fade waits for the current fade to finish, then uses the now-silent deck. The latest pending request replaces earlier pending ones; returning to a live deck cancels obsolete pending music. This avoids cutting an audible clip with only two sources. A new high-priority state is selected immediately, but its music can wait up to the remaining current fade (normally ≤1.25 s). M60's immediate King warning remains independent.
- Non-looping intro/result tracks do not restart when they naturally end or when the same state is sampled repeatedly. Stop/disable clears pending transitions. The director reapplies current context after service re-enable; it does not inherit stale playback.
- `AudioAmbienceController` keeps owning ambience, and stops starting its optional M60 test music when an active music director is present. The M61 installer also turns that test-music flag off. Other components should not write music while the director owns it.

Transitions are frame-driven fades, not bar/beat-synchronized arrangements or network-synchronized stems. No production mix/listening result is claimed from policy tests.

## Production track hooks

`MusicCatalog` lives at `Assets/Art/Audio/DragonlandMusicCatalog.asset` after Unity setup. It maps every `MusicState` to `AudioClip` and loop mode, with global volume/crossfade and `MusicTiming` intro/release values. Replace clip references in that catalog to integrate final licensed/original tracks, preserving the catalog GUID, mixer and code. Multiple states may deliberately share one clip without resetting it. Future music may be stereo; the mono restriction applies only to delivered synthetic fixtures. Profile production streaming/compression and track start latency in standalone builds.

`DynamicMusicDirector.State` and the guarded local `StateChanged` event expose the selected context for diagnostics/future presentation. No music RPC, NetworkBehaviour, Networked property, replicated timer, authoritative combat mutation or shared gameplay RNG consumption was added.

Placeholders: `Assets/Art/Audio/MusicPlaceholders/Music_<State>.wav`, 22.05 kHz mono PCM, seven 8 s loops, one 3 s Match Start and two 4 s result cues (about 3 MB total). All are deterministic original synthesis from `Tools/Audio/generate_music_placeholders.py`, no sampled/downloaded music. A shared D-minor-compatible palette varies pulse/motif/intensity between states. These are transition/mix fixtures, not a finished fantasy soundtrack. Existing sources/manifest are preserved unless `--replace` is explicitly requested.

## Unity setup through MCP

1. Complete M60 first. Fix all compile/import/Console errors before invoking setup; preserve existing metadata. No new scene is needed.
2. Invoke `HeroesOfDragonland.Art.Editor.MusicSystemSetup.Build()`, then `.Validate()`. It imports placeholder music as streaming PCM, creates only a missing catalog, preserves reviewed catalog entries and adds passive context subjects to applicable prefabs under `Assets/Game/Data`. The existing Fusion prefab table is rebuilt; the new components are plain MonoBehaviours.
3. In the intended gameplay scene, call `.InstallLoadedScene()`. It wires one `LocalMusicContext` and `DynamicMusicDirector` onto the existing bootstrap, assigns the catalog, disables the optional old test music and adds missing subjects to scene network actors. Inspect Hierarchy/references and save through MCP. It does not move objects, replace the map or create another listener.
4. Enter non-headless Play Mode through MCP and run `MusicSystemValidation.Start()`. Read Status and Console. Its temporary isolated service checks real source cursor/no-restart, fade overlap/gain, reversal/queueing, Music bus routing, bounded sources, graceful stop and non-loop completion handling. It also checks required subject hooks on loaded actors. Temporary fixtures are destroyed afterward; authored scenes/assets are retained. Source-start/streaming failures require investigation, not reporting a pass.
5. Run the actual context/listening matrix below, then save reviewed assets/scenes and record actual outcomes.

## Required acceptance matrix — pending Unity

- Lobby → Match Start → Exploration; short intro runs once, late join into Playing skips it, rematch gets a new intro, disconnect returns to Lobby. Holding the same state must leave the track cursor advancing rather than resetting.
- Neutral camp nearby and remote: only appropriate local context changes. Dead/returning neutral enemies stop renewing combat; release tail expires. Camp cleared returns smoothly after the tail.
- Real host/client PvP and two simultaneous distant fights: local participants/near observers react, distant players continue Exploration. No music network messages. Death/suspend clears the local fight; respawn/reconnect gets current context without old leases.
- Active boss near local player overrides ordinary combat; idle/distant boss does not. Boss reset/death exits after release tail. Boss transition remains in encounter music.
- Fortress guard/gate/King assault: proximity alone is insufficient, local assault context selects Fortress. Own King damaged anywhere selects King Danger for defenders; attackers do not receive the defender state. Critical-but-safe King does not keep danger forever. Danger overrides Boss/PvP and exits after threat/tail expires.
- Confirmed winner gives opposing Victory/Defeat music. Result cue ends without restarting; repeated snapshots do not restart it. Unassigned spectators do not get a defeat. New round clears results.
- Rapid Camp → PvP → Boss → King changes, reversal while fading, same clip mapped to two states and latest queued track: no hard source replacement, restart, allocation growth or lost final state. Check the bounded delay for a third track during a fade.
- Music/Master sliders affect both decks; mute stays muted through transitions and app restart. M60 one-shot events and Ambience still work; run the updated M60 Play validator (source budget now capacity + 3).
- Listen to fades, streaming latency, final cue endings and 3v3 attack/parry audibility; profile Audio CPU/streaming/voices on a standalone client. Only one scene music owner, at most two music sources, no raw playback synchronization.

## Validation and file inventory

Actual offline results: **38 policy/crossfade checks plus 10,000 rapid transition iterations passed** (10038 assertions in the compiled .NET harness). Covers all ten states, priority, release tails, death/team gates, round/reconnect, no repeated intro, no same-track restart, reversal, pending replacement/cancellation and bounded mix. All **10 distinct WAV fixtures passed** format/headroom/DC/endpoint/loop-flag checks; hashes/levels are recorded in `Tools/Audio/music_source_validation.json`. These do not establish full Unity compilation, native timing or listening quality.

New files: six runtime modules under `Assets/Game/Audio/Music/` (`MusicPolicy`, `MusicDecks`, `MusicCatalog`, `MusicContextSubject`, `LocalMusicContext`, `DynamicMusicDirector`), two Editor setup/Play-validation files, ten music WAVs, generator/manifest/source-validator/report/policy test under `Tools/Audio/`, and this document.

Modified for M61: `GameAudioService.cs` (two-deck central playback API), `AudioAmbienceController.cs` (music ownership), `AudioSystemValidation.cs` (capacity + 3), `AudioSystem.md` and `UnityIntegrationHandoff.md`. M60–M62 are included together in the explicitly requested delivery commit. Deleted files: none. No Unity serialized assets/scenes/prefabs or existing `.meta` files were rewritten offline. Future home integration commits/pushes still require a separate instruction.

Reproduce source checks: Python 3 `Tools/Audio/validate_music_sources.py`; compile `MusicPolicy.cs` + `MusicDecks.cs` + `Tools/Audio/MusicPolicyTests.cs` as a console program without Unity dependencies. Native setup/Play checks require Unity as described above.
