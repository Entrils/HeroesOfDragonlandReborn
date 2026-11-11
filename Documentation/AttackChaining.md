# Milestone 43 — authoritative attack chaining and input buffering

`WeaponMoveset.Combo` references a reusable `WeaponComboDefinition`. Each light step has an explicit next-light index (`-1` ends the chain), optional light-to-heavy link, buffer window, mandatory recovery fraction and dodge cancel rule. Heavy is terminal. There are no weapon-specific branches in player combat or additional Animator transitions.

The supplied Sword/Spear/Staff chains are Light1 → Light2 → Light3. Greatsword has Light1 → Light2. The last light does not wrap. Sword/Spear/Staff permit light-to-heavy transitions from their nonfinal light steps; Greatsword does not. The supplied heavy moves cannot chain or cancel into dodge. Custom definitions may use different links, including shorter chains; a maximum accepted-attack count still bounds any authored cycle.

## Authoring contract

| Setting | Meaning |
| --- | --- |
| Light steps | One rule per corresponding `WeaponMoveset` light move; links are zero-based indices |
| Next light index | Next allowed light step, or `-1` for no continuation |
| Allow heavy | Permits a buffered transition to the moveset's heavy move |
| Buffer start/end | Inclusive normalized interval over **swing + recovery**, before speed scaling |
| Minimum recovery | Fraction of recovery that must finish before consuming a buffered request; the entire swing always finishes first |
| Allow dodge cancel / dodge cancel recovery | Whether and how late in recovery a paid dodge may interrupt; never during the swing |
| Buffer lifetime | Maximum simulation seconds a queued press survives; subsequent rejected presses do not refresh it |
| Restart delay | Additional simulation seconds after natural chain completion/interruption before a fresh attack can start |
| Maximum chain attacks | Bound of 1–32 accepted attacks, including the first attack and any heavy finisher |

Defaults: buffer `[0.45, 0.92]`, lifetime `0.6 s`, mandatory recovery `50%`, restart delay `0.3 s`. Sword/Spear/Staff allow dodge cancels after `85%` recovery; Greatsword does not. Data can require full recovery by setting minimum recovery to `1`. Move swing/recovery/hit-window durations still come from M42 profiles and are divided by that move's attack speed.

Run **Heroes of Dragonland → Combat → Build Attack Chains** through Unity MCP in Edit Mode to create missing definitions in `Assets/Game/Data/Weapons/Combos/` and link them to existing movesets. Existing assigned definitions and authored assets are preserved. Invalid indices, windows, timers, heavy continuations or unbounded attack limits fail validation. M42's serialized `comboResetDelay` is preserved for asset compatibility but no longer controls chaining; use the combo definition. A missing combo permits standalone attacks with full recovery and a conservative restart delay, without buffering or attack cancels.

## Simulation flow

`MeleeAttack.RequestAttack` is a press intention. While idle, it can accept a first attack. While busy, it can reserve **one** allowed next attack only inside the current move's buffer window. First valid request wins; later light/heavy requests cannot overwrite or prolong it. Queueing spends no resources and does not increment the accepted attack sequence.

`AttackChainState` owns the simulation clock, one-slot buffer, expiry, chain count and restart lockout. It never authorizes damage or pays resources. On a server/offline attack tick, `MeleeAttack` samples the existing hit window, then attempts at most one eligible buffered transition. It rechecks the link/count, equipped weapon/moveset, requirements, living/enabled state, defense, dodge and stamina before committing a new attack. A successful continuation commits its new profile/geometry/facing and pays its exact M42 stamina cost once. Insufficient stamina drops that request and lets the old recovery finish; later regeneration cannot silently retry it.

Accepted chain transitions may shorten only the explicitly allowed tail of recovery. No transition can shorten windup, an active hit window or the remainder of the swing. Large ticks cannot execute multiple buffered attacks or apply leftover time to a new swing. Each new swing starts with elapsed time zero and a fresh hit-target set. Without a valid queued transition, full recovery completes; a fresh attack then requires a fresh press and the restart delay. Holding a button never repeats attacks automatically.

Skills select the first light move, retain their existing mana policy, cannot interrupt a busy attack and do not accept combo continuations. Equipment changes, death, stagger, accepted dodge, component disable, match pause, input suppression and active defense discard buffered intentions. An equipment change invalidates the queued link but does not rewrite an already committed strike. Multiplayer equipment policies continue to reject weapon changes while busy.

## Cancel rules

- Light/heavy: only an authored link, a valid buffered press and mandatory recovery permit the next attack.
- Dodge: rejected during the whole swing and before the authored recovery threshold. A legal dodge must successfully pay its own stamina before cancelling the attack; failed dodge payment leaves the attack and buffer intact. Accepted dodge clears the chain/buffer and uses the existing swept motor/invulnerability policy.
- Block, skills and equipment: cannot cancel an attack. Holding block during an attack does not interrupt it.
- Stagger, death, respawn, component disable and match shutdown: existing authoritative interruptions clear the action and queue. Death clears immediately through the stats event, rather than waiting for another animation/frame.

Animator state and animation events never select transitions, pay costs, cancel actions or trigger damage. Presentation continues to use M42's accepted weapon/move index, sequence, start tick, duration and facing.

## Input and networking

Local bootstrap input capture samples press edges once per rendered frame. Monotonic `uint` light/heavy counters retain short taps between Fusion input ticks. Repeated/held input, stale counters and repeated packets cannot generate another intention. The server consumes a counter jump as at most one request, prioritizing heavy if both changed. Unsigned wrap is supported. A newly spawned server combat adapter absorbs its first counter baseline so old session/respawn counters cannot invent an initial attack.

Counters describe **intent**, never a desired combo index or time. The server checks its own elapsed attack clock/window; client timestamps, animations and requested target states cannot bypass it. Presses received outside the window are dropped, not deferred until the window opens. Presses rejected during death/pause/defense/dodge/input suppression are consumed so they cannot replay when that state ends. Missing packets do not rewind counters or fabricate a release. Opening local UI/focus suppression clears pending attack intent while the authoritative simulation continues.

Server `NetworkPlayerCombat` publishes from `MeleeAttack.AttackStarted`, so a transition accepted in `AfterTick` publishes exactly once just like a direct attack or skill. Bots submit rate-limited press counters through the same server path. Offline `PlayerController` submits real press edges through the same `MeleeAttack` buffer. Matching host/client builds are required because the network input layout changed.

## Verification

`AttackChainValidation.ValidateAssets()` checks all four combo references, terminal light rules and input-counter baseline/replay/jump/wrap behavior. Its isolated Play Mode fixture checks complete finite chains for all weapons; optional heavy links; rejection of early presses outside the buffer window; single-slot deduplication; mandatory recovery; payment on consumption; expiry; insufficient-stamina drop; no automatic retry; restart lockout; bounded graphs; immediate death/stagger invalidation; equipment invalidation; and late dodge eligibility. Buffering may happen during late windup, but consumption always waits for the full swing and required recovery. `WeaponMovesetValidation` retains M42 standalone costs/geometry/clip checks, with directed chains now verified by the M43 fixture.

Launch matching development players with `-hodrChainCheck`, normal `-hodrMode host/client`, the same `-hodrRoom` and `-hodrBots 4` on the host. The opt-in M42 network fixture now submits actual repeated press/release edges, holds the first Sword press to check deduplication, exercises buffered light-to-heavy, and inspects real remote snapshots for all four weapons. Both processes must report `[AttackChainNetwork] PASS`. The fixture checks accepted timing under spam, legal transitions, remote clips and replica authority rejection. Bots remain active, so observed authoritative stagger/death/dodge interruptions invalidate the observer's previous-chain comparison; a legal reset to Light1 is not classified as a buffered link. Server `[ChainAccepted]`/`[ChainFinished]` diagnostics record indices, ticks and stagger state. Test setup grants resources/progression on server actors only; it never fabricates replicated attack states or bypasses the attack-cost path. It is not installed in ordinary sessions and its implementation is excluded from release builds.

## Files

Verified on 2026-11-11: the M43 isolated Play Mode fixture and M42 geometry/cost/clip regression fixture both passed. Development build succeeded with zero errors. Real host and client both reported `[AttackChainNetwork] PASS` in `Builds/ChainAcceptance-3865a5cc/host.log` and `client.log`, including buffered heavy, held-input deduplication and recovery under repeated presses. Earlier observer diagnostics misclassified a bot-induced stagger/reset as an illegal buffered link; server finish logs confirmed the forced stagger and the observer now accounts for replicated interruptions. The successful run retained active bots and recorded real stagger interruptions. Editor assets were saved in Edit Mode with a clean active scene. Existing package/deprecation warnings remain; final gameplay feel and timing balance require director playtesting.

New runtime: `Combat/WeaponComboDefinition.cs`, `Combat/AttackChainState.cs`, `Combat/AttackPressSequence.cs`. New Editor tools: `Art/Editor/AttackChainSetup.cs`, `Art/Editor/AttackChainValidation.cs`. New data: four combo assets and Unity metadata under `Data/Weapons/Combos/`.

Updated: `WeaponMoveset`, `MeleeAttack`, `NetworkPlayerCombat`, `NetworkPlayerInput`, `MultiplayerBootstrap`, `NetworkTestPlayer`, both dodge adapters, offline `PlayerController`, developer bot press generation, the existing weapon/network validation fixtures and skill acceptance input. Four movesets now reference their combo definitions. No new player components or Animator graph branches; no assets deleted and no automatic commit.
