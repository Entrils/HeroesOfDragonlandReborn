# Milestone 60 — Audio System

## Delivery status

Offline implementation: modular playback/settings/presentation code, 24 catalog recipes, 25 original synthetic WAV placeholders, Unity mixer/catalog/prefab/scene setup tools, native Play validator and source tests. Voice is intentionally an empty category/recipe; no voice chat or recorded dialogue is implemented.

**Unity integration is pending.** This computer has no Unity Editor/MCP. No `.mixer`, catalog `.asset`, prefab/scene edits or Unity-generated metadata were fabricated. Full C# compilation/Fusion weaving, native playback, listening, Console, build, UI layout and host/client checks have not run here. M52/M53 remain deferred. These are functional sound placeholders for wiring/listening tests, not final sound design, recordings or a final score.

## Modules and routing

- `Assets/Game/Audio/AudioCatalog.cs`: ScriptableObject mapping stable cue IDs to clip variants, bus, gain, pitch variation, priority, distance, cooldown and concurrency cap. Swap clips here without changing gameplay/network code. The builder preserves existing catalogs.
- `GameAudioService`: one instance on the existing `MultiplayerBootstrap`, found through the actor's runner. Preallocates 24 one-shot AudioSources (configurable 8–32). M61 extends the original Music/Ambience pair to two crossfading Music decks plus one Ambience source, for 27 sources by default; call sites never instantiate sources. No static cross-runner singleton, network behaviour or audio RPC. See `DynamicMusic.md` for adaptive music ownership.
- `AudioVoiceBudget` / `AudioMath`: engine-independent admission/volume policy. Higher priorities can replace lower priorities; equal priorities never steal. Per-cue maximum and per-actor/cue cooldowns bound bursts. A fixed 128-entry recent-cue ring bounds cooldown bookkeeping; extreme bursts can evict older cooldown records, but cannot exceed voice/per-cue caps.
- `AudioPreferences`: local PlayerPrefs, clamped 0–1 values, logarithmic mixer gain, silence floor −80 dB. Applies in Start, saves on panel close, focus loss, disable and quit; no disk write on every slider drag.
- `Assets/Game/UI/AudioSettingsView.cs`: separate UI Toolkit document using existing PanelSettings. Master/Music/SFX sliders, Audio button/F10, Escape/Close. Uses existing `LocalGameplayInputGate` while open; simulation continues. Restores ownership on disable.
- `AudioAmbienceController` / `AudioAmbienceZone`: local camera/listener-based selection, forest fallback and fortress volumes at existing TeamSpawnArea anchors. One ambience bed fades down over 0.5 s, switches, then fades up over 0.5 s. No new colliders/triggers/path changes. Synthetic music is opt-in (`playPlaceholderMusic`) for tests.

Mixer asset generated at `Assets/Art/Audio/DragonlandAudio.mixer`:

```text
Master [MasterVolumeDb]
├── Music [MusicVolumeDb]
├── SFX [SfxVolumeDb]
├── UI
├── Ambience
└── Voice
```

All child groups route directly into Master. Source volumes contain recipe gain only; Master gain is never multiplied twice. SFX controls world/combat/match cues, while UI/Ambience/Voice retain their own centralized groups under Master. Those three buses have no separate sliders in this milestone.

## Event hooks

| Requested sound | Presentation source | Scope |
|---|---|---|
| Player footsteps | Existing CharacterVisual Footstep animation cue, already filtered for grounded movement/death | Spatial; 3 clip variants, 14 m |
| Jump | Rendered airborne edge with positive vertical speed | Spatial, 18 m |
| Dodge | Existing accepted NetworkPlayerDodge sequence/start tick | Spatial, 18 m |
| Attacks | Existing accepted NetworkPlayerCombat sequence/start tick | Spatial, 25 m |
| Hit | Replicated health decrease, excluding fatal damage | Spatial, 24 m |
| Block / parry | Local `NetworkImpactVfx.Presented` event from the existing confirmed M59 outcome RPC | Spatial, 26/28 m |
| Player death | Replicated alive → dead edge | Spatial, 28 m |
| Chest | Replicated closed → open edge | Spatial, 18 m |
| Camp completion | Existing CompletionCount increase | Spatial, 32 m |
| Forest / fortress ambience | Camera position and presentation zones | Local 2D bed |
| Enemy aggro | Existing replicated idle/patrol/return → alert/chase/attack edge | Spatial, 28 m |
| Enemy attacks | Existing replicated attack sequence, within the windup | Spatial, 26 m |
| Enemy damage / death | Replicated health decrease / alive → dead edge | Spatial, 22/28 m |
| Match start | Live transition into Playing, including new-round counter change | Local 2D, once |
| King under attack | Nonfatal health decrease on own team's King | Local 2D warning, 2 s cooldown |
| King death | Replicated alive → dead edge | Spatial, 70 m |
| Victory / defeat | Existing FinishCount increase plus confirmed Winner vs local team | Local 2D; unassigned spectators receive neither |
| UI click | Audio settings panel opened | Local 2D |

Animation footstep events and Humanoid CharacterVisual must exist in the current character pipeline. Do not rerun/rebuild the humanoid pipeline just to install audio: inspect existing clips/event forwarding first. Attack/dodge cues deliberately use accepted sequences rather than additional animation-event playback, preventing duplicates.

Health polling coalesces damage into one audible reaction per rendered snapshot; it does not promise a sound per damage packet. Blocked chip damage can legitimately produce both metal contact and a damage reaction. Fatal damage plays death instead of an additional hit. No hit sound is inferred from predicted collision. Initial snapshots seed baselines without playing historical deaths, open chests, camp clears, attacks or victory. Disabled, invalid/despawned and suspended actors reset baselines. Rounds reset counters. Existing rendered character state supplies jump/footstep presentation; it is not new authoritative state.

## Multiplayer and mix limits

Only existing gameplay state/events travel over the network. M59's confirmed impact handler gains a local subscriber event, fired independently of VFX pool admission; its RPC signature/payload, authority and damage resolution are unchanged. No audio IDs, clip data, networked sources or new audio-specific RPCs are added. Every replica chooses variants and mix locally, using a service-owned random generator that does not consume Unity's shared gameplay random state. Rebuild compatible peers after integration as usual; audio components are plain MonoBehaviours.

Higher-priority King/match cues (95–100) can displace footsteps (10); ordinary attacks are 65, block 70 and parry 85. Typical cue cap is four; warnings/stingers cap at one. Spatial requests beyond recipe max distance are rejected before admission. Doppler is disabled. Beds have dedicated capacity so repeated hits cannot steal background loops. Batch/headless execution does not allocate/play sources. No per-frame scene-wide actor searches or raycast occlusion. Runtime source allocation happens only during service initialization.

Use moderate ambience/score gain and keep attack/parry cues short. Do not mask telegraphs with long attack tails or make a sound the only carrier of a gameplay warning; existing HUD/VFX stay in place. No team-specific volume advantages. Actual perceived loudness, loudness balance, voice counts/CPU and six-player masking must be checked in Unity with headphones/speakers. No LUFS, occlusion or production mix quality claim is made from waveform checks.

## Unity integration through MCP

Follow `UnityIntegrationHandoff.md`, M59 before M60. Fix Console/compiler/weaver errors before invoking setup. Preserve reviewed assets and `.meta` files.

1. Import fetched WAVs, allow Unity to generate metadata. In Edit Mode call `HeroesOfDragonland.Art.Editor.AudioSystemSetup.Build()`. It creates missing mixer/groups/exposed parameters and catalog, configures mono PCM SFX with DecompressOnLoad and streaming beds, and adds plain presentation components to applicable prefabs under `Assets/Game/Data`. It preserves existing catalog content, prefab GUIDs and gameplay components, then rebuilds the existing Fusion prefab table.
2. Call `.Validate()`. Inspect actual group routing, parameters and clips. All six groups must exist and the five child buses must feed Master.
3. Open the intended saved gameplay scene (existing UI Toolkit HUD required). Call `.InstallLoadedScene()`. It wires the bootstrap service/preferences/ambience/input gate, a separate settings document, actor/world adapters on scene network objects, the one MainCamera AudioListener and ambience zones under existing fortress spawn areas. It marks the scene dirty; inspect Hierarchy, review zone extents/camera height/UI overlap and save through MCP. Duplicate/non-camera listeners cause a clear preflight failure rather than silent removal.
4. Enter Play Mode through MCP. Close other menus and release gameplay input. Call `AudioSystemValidation.Start()`, read Status and Console. This uses a temporary isolated native pool, exercises culling/cooldown/priority/beds, actual sliders/mixer and input ownership, restores settings and destroys only its temporary objects. This does **not** validate actual multiplayer event delivery or listening quality.
5. Complete the manual matrix below, save reviewed assets/scenes, record results and profile a standalone 3v3 session. Do not mark integration complete based on source tests.

### Mixer bridge and manual fallback

Unity's runtime mixer API does not provide asset authoring. `AudioMixerFactory` isolates an Editor-only reflection bridge for creation/groups/volume exposure, based on [Unity's mixer controller reference](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Audio/Mixer/Bindings/AudioMixerController.cs) and [binding definitions](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/AudioEditor/ScriptBindings/AudioMixerController.bindings.cs). Internal APIs can differ in installed Unity 6000.6: the bridge has **not** been executed here. It fails explicitly on unsupported signatures and never serializes Unity YAML manually.

If it fails, inspect any partially created asset through MCP and finish the same mixer: Master plus Music/SFX/UI/Ambience/Voice, all children of Master. Expose **Volume** of Master, Music and SFX as `MasterVolumeDb`, `MusicVolumeDb`, `SfxVolumeDb`; leave snapshot attenuation at 0 dB. Save and rerun Build/Validate. Do not delete/recreate an existing authored mixer to bypass a conflict. A complete manually configured mixer bypasses the creation bridge. Settings use Start because [Unity documents that SetFloat should run after Awake/OnEnable](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audio/audiomixer/setfloat).

## Required acceptance matrix — pending Unity

- Host + actual remote client: each player sound; remote footsteps follow the remote character; attacks/dodge/block/parry occur once per accepted cue, rejected actions stay silent. Generic damage, blocked zero damage, parry and death checked separately.
- Enemy idle → aggro → attack → damage → death; no historical roar/attack/death for a late observer. King under attack warns only defenders, respects cooldown, remains audible at distance; King death plus correct victory/defeat stinger on opposing peers.
- Chest opened by self/other: one spatial opening; already open on join: silent. Camp CompletionCount edge once; no historical clear on joining. Start/new round once; reconnect/finished-match join does not replay victory.
- Death/respawn, suspend/reconnect and runner replacement: no duplicate subscriptions or stale events. Fresh actions after rebaseline audible. Disable service: all one shots/beds stop; disable/enable ambience controller: bed resumes.
- Master zero mutes all buses, SFX zero mutes combat/world/match, Music zero mutes enabled test music. UI/Ambience route via their own buses. Restart app and confirm values persist. Panel open blocks gameplay input, close/disable releases it without leaking held attack input. Check F10 and small-window HUD overlap.
- Cross forest/fortress boundaries and overlap: no stacked ambience, smooth bounded fade; no changed collision/navigation. One listener, default 27 service sources after M61 (24 one shots + 2 Music + 1 Ambience), one-shot cap respected under 3v3 + enemies; profile Audio CPU/voices/memory and listen for masking/clipping. The updated M60 Play validator expects capacity + 3.

## Files and validation performed here

New: `Assets/Game/Audio/` (data/policy/service/preferences/actor/world/ambience modules), `Assets/Game/UI/AudioSettingsView.cs`, three `Assets/Game/Art/Editor/Audio*.cs` setup/validation files, `Assets/Art/Audio/Placeholders/` (25 WAVs), `Tools/Audio/` generator/manifest/checks/report, this document. Modified: `NetworkImpactVfx.cs` adds only a guarded local event; `UnityIntegrationHandoff.md` includes M60. Deleted: none. No scenes/prefabs/assets with existing metadata were rewritten offline.

Actual results: **22 compiled .NET audio policy checks passed**, including 6000-request saturation, priority, per-actor cooldown, per-cue caps, stale generation protection and invalid preferences. **24 recipe / 25 WAV source checks passed** for coverage, PCM format, non-silence, headroom, DC offset, fade endpoints and loop endpoints. `Tools/Audio/source_validation.json` records clip hashes/levels. These do not constitute Unity compilation, playback or listening tests.

Reproduce source checks with Python 3: `python Tools/Audio/validate_audio_sources.py`. Compile/run `Assets/Game/Audio/AudioPolicy.cs` + `Tools/Audio/AudioPolicyTests.cs` as a console program (no Unity dependencies). `generate_placeholder_audio.py` creates only missing WAVs/manifest by default; `--replace` is explicit regeneration. Replace synthetic clips with reviewed licensed/original recordings via catalog references, preserving definitions/mixer/hooks. Spatial recordings should remain mono; real music/ambience can use appropriate formats/import settings after profiling. Do not regenerate placeholders over reviewed production clips.
