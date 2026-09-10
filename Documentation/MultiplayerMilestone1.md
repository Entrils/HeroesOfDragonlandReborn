# Multiplayer Milestone 1 — implementation and network validation

## Resume review — 2026-09-09

**Status: foundation implemented; real multi-process network checks passed. Final Unity Editor Console and visual review remain pending because Unity MCP is not exposed in this session.**

The previous handoff below was stale. On resuming, Fusion 2.1.2 was already imported under `Assets/ThirdParty/Photon`, Fusion configuration existed under `Assets/Photon/Fusion/Resources`, and the network sources no longer had the `HODR_FUSION` guard. The saved test scene, player prefab, materials and Windows development build already existed. No SDK or App ID input was needed to run the tests: actual Photon connections succeeded.

The existing build report at `Builds/MultiplayerFoundation/build-result.txt` records **Succeeded, errors=0, warnings=517**. The compiled game assembly is dated 2026-09-09 01:11 local time, later than the networking sources. This session tested that existing build; it did not rebuild or inspect the 517 build warnings through the Editor. Do not interpret this as a warning-free build or a current clean Console claim.

### Executed validation

All processes used the existing `Builds/MultiplayerFoundation/DragonlandMultiplayer.exe`, `-batchmode -nographics -hodrProbe`, and independent log files under `Builds/MultiplayerValidation/`. These are real independent Fusion peers, not mocked network callbacks. The host room was `hodr-m1-resume-0909`; the server room was `hodr-m1-server-0909`.

| Check | Observed result |
| --- | --- |
| Host + five clients | All six peers recorded `players=6 A=3 B=3`. |
| Unique ownership | Participants 1–6, owners 1–6, object IDs 1025/1028/1029/1031/1033/1035 matched across all peers; each peer owned exactly one initial avatar. |
| Movement synchronization | Client 1 supplied movement through `-hodrMove`; its owner 2 avatar reached `(18,1,-4)` from the Team B spawn at `(8,1,-4)`. Host and every client recorded the final position. Host avatar remained at `(-8,1,-4)`. |
| Capacity | Extra client failed with `GameIsFull`; Photon reported error 32765. No seventh avatar was admitted. |
| Graceful disconnect | Client 1 used `-hodrDisconnect 35`; host recorded `Left owner=[Player:2] count=5/6`. |
| Replacement/late join | Replacement joined as participant/owner 7, object 1037, Team B; all six slots were occupied again. Replacement received no departed owner 2 object. |
| Server mode | Server initially recorded zero avatars, then two clients with `A=1 B=1`. No local server avatar. |
| Abrupt disconnect | The first server client process was forcibly terminated. After transport detection, server recorded owner 2 leaving and `players=1 A=0 B=1`; surviving client also recorded that roster. |
| Standalone roster rules | Recompiled and reran `NetworkTeamRosterValidation.cs`: **150 checks passed**. |
| Recorded peer consistency | `Tests/Standalone/VerifyMultiplayerLogs.ps1`: **80 checks passed** against these logs. This verifier intentionally uses this run's IDs, not arbitrary future runs. |

Log inspection found the expected Fusion warning that `StartGameArgs.Scene` is absent: each process loads the same local arena, and only dynamically spawned objects are networked. The excess-client log contains the expected `OperationException`/error for a full room. No other exception header was found in the collected logs. Stack traces can contain `SetException` method names without indicating an additional unhandled exception.

### Scope and remaining checks

Server assigns teams and lanes and spawns avatars with distinct input authority. Input contains movement intent only; team and transform state are server-written. The existing planar server motor and NetworkTransform remain isolated from combat, inventory, AI, camps and the offline player controller. No networking architecture change was made during this continuation.

Still pending: Unity MCP Console/Hierarchy inspection, interactive rendering/WASD review, same-process failure/retry and disconnect/restart, and scene teardown during an in-flight startup. Automated motion in headless players verifies the transport path, not keyboard focus or visual interpolation quality. These checks must not be reported as passed from the current evidence.

Changed during this continuation: this document. Newly created: `Tests/Standalone/VerifyMultiplayerLogs.ps1`, plus local test logs and roster executable under ignored `Builds/MultiplayerValidation/`. No gameplay source, scene, prefab or asset was changed; no files were deleted and no commit was made.

To review the scene interactively, open `Assets/Scenes/MultiplayerFoundationTest.unity`, start Host, then launch the existing executable with `-hodrMode client -hodrRoom dragonland-m1`. WASD moves the focused peer's avatar. Keep all peers on the same room name.

## Historical handoff — superseded by the resume review above

Status: **INCOMPLETE — SDK/App ID required; network implementation not compiled or runtime-validated.**

The requested scope authorizes the first network implementation despite the earlier audit's documentation-only restriction. The approved architecture is Photon Fusion 2 client/server, with Host for development and Server for dedicated simulation. No alternate stack was substituted.

## Current work

- `Assets/Game/Core/NetworkTeamRoster.cs`: pure server-side six-connection roster; balanced assignment, three per team, duplicate join handling, leave and reset. Reuses existing `TeamId`.
- `Assets/Game/Core/MultiplayerBootstrap.cs`: prepared Fusion Host/Server/Client lifecycle, six-player room limit, server spawn and admission recheck, input ownership, player-object association and disconnect cleanup.
- `Assets/Game/Player/NetworkPlayerInput.cs`: movement intent only; no position/team payload.
- `Assets/Game/Player/NetworkTestPlayer.cs`: server-only fixed-tick movement with finite/clamped input and arena bounds; replicated team/participant/spawn lane, NetworkTransform presentation. No client prediction yet.
- `Assets/Game/UI/NetworkPlayerPresentation.cs`: team colors and participant/owner labels.
- `Assets/Game/UI/MultiplayerTestPanel.cs`: development connection controls and status.
- `Assets/Game/World/Editor/MultiplayerTestSceneBuilder.cs`: prepared isolated arena/prefab authoring, without offline gameplay components.
- `Tests/Standalone/NetworkTeamRosterValidation.cs`: independent admission/capacity/replacement tests.

Fusion-dependent sources are guarded by `HODR_FUSION` until the real SDK can be imported and verified. This guard preserves offline project compilation; it is not evidence that the Fusion code compiles. No networking package, prefab or multiplayer scene currently exists. Existing gameplay source and scene behavior were not modified. No files were deleted and no commit was made.

## Blocker and resume procedure

The [official Fusion SDK download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download) lists 2.1.2 Stable, but its download endpoint returned **No Access / Sign In**. A locally downloaded `.unitypackage` and the user's Fusion App ID were requested. No third-party SDK mirror, sample App ID or different stack was used.

Once supplied:

1. Import the SDK through Unity MCP, preserving metadata. Keep third-party assets under `Assets/ThirdParty` through Unity asset operations where supported by the SDK; inspect path-dependent tooling first.
2. Configure Fusion App ID, consistent test region/app version, prefab registration and weaving. Enable `HODR_FUSION` through Editor settings only after SDK import. Resolve real API/compiler issues; exact Unity 6000.6 compatibility remains unverified.
3. Invoke `MultiplayerTestSceneBuilder.Build()` through Unity MCP; verify/bake/register the network prefab and references. Build with the isolated test scene explicitly included; preserve the offline build scenes.
4. Run Host plus two independent clients, then Host plus five clients and one excess join attempt. Also smoke-test Server plus clients, confirming no server avatar.
5. Fix and validate startup failure/retry, shutdown races, late-join transforms and team data, per-owner input, replacement spawn placement, abrupt connection loss, scene teardown and clean restart.

Prepared command line: `-hodrMode host|server|client -hodrRoom <same-room-name>`. Server can use `-batchmode -nographics`. This is a planned executable interface, not an existing runnable build.

## Validation evidence

- Standalone roster test compiled with the installed Unity Mono C# compiler and executed successfully: **150 checks passed**. Covers all six possible departures, 3v3 distribution, duplicate joins/leaves, seventh rejection, replacement team and reset.
- Unity MCP initially inspected saved `SampleScene`, outside Play Mode; Windows standalone build support is installed. Intermittent MCP discovery failures occurred; a later source refresh succeeded.
- Fusion-dependent source compilation, prefab weaving, network scene creation, Play Mode, multi-process launch, movement replication and disconnect transport tests: **not executed**.

## Intended test boundaries

Connections identify participants for this milestone; they are not authenticated persistent accounts. Reconnecting creates a new participant number and releases old team capacity after disconnect detection. No reconnect grace, persistence, host migration or production matchmaking is claimed.

The arena motor is intentionally planar, server-simulated and bounded; it does not replace the existing third-person controller or implement jump, prediction, combat, inventory, AI or camps. Client-transform writes never become server motion; only movement input is submitted. The next movement milestone can add prediction after SDK/controller compatibility is established.

Acceptance remains pending until actual logs show unique objects/owners, matching team/transform state across peers, rejection above capacity and despawn/slot release on both graceful and abrupt disconnect. A clean Console with guarded code does not satisfy that acceptance.
