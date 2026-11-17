# Milestone 54 — Visual Direction Lock

<!-- HOME-INTEGRATION-2026-11-17 -->
**Home integration update (2026-11-17):** Unity benchmark saved separately from gameplay, with existing source visuals, matched crests and review cameras. Initial Unity overview was inspected and mirrored labels repaired; timed perception, grayscale/CVD and moving-warning approval remain open.

See [the live validation record](HomeIntegrationValidation.md) for actual outcomes, failures, evidence and remaining checks. Earlier offline-only status below is historical.

## Scope and status

Chosen direction: **Weathered Dragonland** — grounded, selectively stylized medieval fantasy. Ancient stone, worn iron, pale woodland and restrained dragon heraldry frame clearly visible combatants. Darkness comes from subject matter, architecture and weathering, not crushed blacks or obscuring fog. This document is the M54 authoring baseline; in-engine acceptance remains pending.

AGENTS.md was read. DESIGN.md was not found in this checkout, including a hidden-file filename search. Requirements come from the M54 brief and existing project contracts: [ArtPipeline](ArtPipeline.md), [CharacterVisual](CharacterVisual.md), [ThirdPersonCamera](ThirdPersonCamera.md), [CampDifficulty](CampDifficulty.md), and [NeutralBoss](NeutralBoss.md). M52/M53 are deferred, not prerequisites or completed work. No gameplay, camp distribution, combat timing, movement, collision, networking or rewards change here.

## 1. Overall art style

Use realistic construction with simplified, deliberate shapes and restrained surface detail. Broad planes, readable bevels and clear material boundaries take priority over micro-scratches. Souls-like weight and age, with competitive clarity at the existing third-person camera. Avoid photoreal clutter, tiny decorative spikes, cartoon outlines and all-black silhouettes. Dragon motifs appear as structural arches, crests and occasional fossil-like forms, rather than covering every object.

The first production biome is **Ashen Borderwood**: a desaturated temperate forest surrounding a ruined border fortress, with pale limestone, dark timber and a limited corrupted-stone accent. This is a visual kit for the existing map, not a replacement layout or new biome mechanic. Highlands, ruins and corrupted pockets reuse this palette and construction language.

## 2. Character proportions

Target adult human proportions around 7.5 heads tall, approximately 1.8m standing height. Hands, weapon guards and shoulder shapes may be modestly emphasized for distance readability. Keep the current avatar rig, sockets and gameplay capsule as the integration reference; do not rescale actors or change reach to fit artwork. Prototype proportions remain placeholders until deformation review.

Armor has a readable head/torso/limb separation. Cloth panels and broad heraldic fields carry identity; avoid full-body team paint. Preserve the same visual mass and coverage for both teams. No extreme capes, horns or shoulder silhouettes that imply different hitboxes. Heavy/medium/light visual distinctions must reflect existing equipment definitions, not invent classes or stats.

## 3. Environment style

Pale trunks, sparse low vegetation, broken stone and compact rock masses. Travel surfaces are broad, muted and low frequency; decorative noise increases toward route edges. Roots, grass and rubble cannot visually conceal feet or ground warnings on fighting surfaces. Canopies frame the sky above combat and camera space.

Art replacements must respect existing cover, route widths, visibility barriers and collision footprints. Do not remove intentional ambush cover or add new hiding spots. Compare foliage and silhouettes from both approaches to every mirrored encounter; equal collision does not guarantee equal visibility.

## 4. Fortress style

Both teams share the same weathered limestone and iron-reinforced timber kit. Square towers, deep gate frames, broad battlements and restrained dragon crests. Team identity comes from matched-size banners, shields and gate plaques: A uses a split upward chevron; B uses a three-bar crest. Crests must remain legible in monochrome.

Use equal banner area, height, luminance and placement on each side. Preserve existing gate openings, flank exits, king locations and spawn boundaries. Decorative portcullises must not imply blocked routes where gameplay allows passage.

## 5. Weapon style

Forged medieval swords, axes, hammers and shields with identifiable profiles and practical grips. Slightly broaden thin blades and simplify guards when needed, without changing gameplay reach. Steel edges are light enough to separate from dark armor; wood and leather handles stay matte. Restrained runes distinguish exceptional items; rarity never changes a weapon into an unreadable glow cloud. Do not add weapon families before checking the current item catalog.

## 6. Creature style

Wolves: low angular torso, clear muzzle and tail. Bears: broad torso, rounded shoulders and short muzzle. Neutral knights: upright armor mass and worn neutral heraldry. Keep these silhouettes distinct even with flat gray materials. Corruption is a small secondary feature, not a universal purple overlay. Final designs follow existing rig, scale and hitbox needs; visual enlargement is reviewed against damage expectations.

## 7. Material language

Follow the established URP Lit metallic pipeline; Blender previews do not establish Unity shader parity. Values below are starting ranges for authoring, not measured acceptance results.

| Material | Appearance | Metallic | Roughness |
| --- | --- | --- | --- |
| Limestone | Cool medium-light planes, darker sparse joints | 0 | 0.75–0.95 |
| Iron/steel | Dark body, broad light edge highlights | 0.85–1 | 0.35–0.65 |
| Timber | Brown-gray, large grain, limited knots | 0 | 0.7–0.9 |
| Leather | Warm dark brown, broad wear zones | 0 | 0.55–0.8 |
| Team cloth | Matte, clean heraldic fields, modest edge wear | 0 | 0.8–0.95 |
| Corrupted stone | Charcoal-gray with sparse ivory cracks | 0 | 0.65–0.9 |

Keep deep black to small joints. Avoid mirror armor and baked highlights in base color. Unity smoothness is 1 minus roughness. Prefer shared materials and trim sheets; no production texture sets are required for this milestone.

## 8. Color hierarchy

Hex colors are sRGB design swatches, not direct linear shader values. Maintain approximate perceived brightness across team treatments and test under the same lighting.

| Role | Starting swatch | Non-color cue |
| --- | --- | --- |
| Team A | Azure #478DD9 | Split upward chevron, A label |
| Team B | Vermilion #D96B52 | Three-bar crest, B label |
| Environment | Slate #626B70, limestone #97988B, timber #65594B | Construction and surface shape |
| Neutral actors/camps | Bone #C8B994 | Beast silhouette; stone cairn and tier marks |
| Interactable | Pale brass #D6B66F | Chest lid/lock profile and interaction glyph |
| Hostile warning | Orange #FF913D with ivory rim #FFE5BC | Jagged perimeter, danger glyph, directional shape |
| Corruption accent | Muted violet #776580 | Cracked stone and sparse angular growth |

Priority: imminent danger > actor identity/target state > interaction/objective > environment detail. Team identity and hostility are separate channels: a blue attack can still threaten the local player. Rarity colors are restricted to item presentation and cannot masquerade as ground danger. Saturated color and emission occupy small areas, never entire scenery surfaces.

## 9. VFX readability rules

Hostile areas use a continuous bright perimeter with dark backing, outward teeth and a danger glyph. Shape must represent the actual committed sector, circle or sweep. Keep centers mostly transparent so feet, weapons and overlapping warnings remain visible. The perimeter stays at the true danger boundary; decorative particles cannot imply additional reach.

Warn, commit and impact phases follow accepted gameplay state and timing. No new anticipation delay, hit window or damage area is introduced. Size pulses may occur inside the boundary only. Friendly effects use smooth segmented borders and a different glyph; team hue is secondary. Neutral boss warnings are hostile to both teams. If overlapping effects cannot all be legible, preserve every hostile boundary and suppress cosmetic particles first. Persistent dust, bloom and camera shake must not obscure warnings. Avoid strobe effects.

These are implementation requirements for later presentation work, not claims that current VFX already supply them. Test real remote replicas and the Stone Warden's cleave, sweep and stomp before accepting final effects.

## 10. Team readability rules

Always combine color with the crest and an explicit A/B identity marker on remote player UI. Repeat the crest on chest/tabard and back so front and rear approaches work. At distances where cloth details disappear, retain a legible overhead shape/letter. Do not rely on an equipment item that can be unequipped. Do not introduce through-wall markers or reveal hidden actors; use existing visibility policies.

Both teams use equally visible fields, equivalent armor silhouettes and identical marker screen area. Target lock, ally status and team identity must not share one ambiguous icon. These marker proposals require future integration; the existing full-body blue/red tint remains unchanged by M54.

## 11. Neutral enemy and camp readability

Neutral means unaffiliated, not harmless. Use bone/iron materials, beast or neutral-knight silhouettes, and a neutral diamond marker distinct from either team crest. Hostile action warnings still use the common danger language.

Camp landmarks use a recognizable broken stone cairn with one, two or three broad notches for existing tiers, plus a separate crown/warden glyph for the contested center. Tier may also appear as text/icon in the current label system. Distinguish inactive, occupied and cleared states using symbol/fill changes only when driven by existing authoritative lifecycle state. No permanent glow or geometry should falsely imply available rewards.

Landmarks must be recognizable from a clear approach at 25–35m, without revealing camps through existing cover. No decorative camp enclosure or camp-safe implication. Chests use a domed lid, large front lock and brass corner brackets; avoid similarly shaped decorative crates. Available interaction adds a glyph/prompt within the existing interaction rules. Open/claimed appearance must follow actual chest state. The benchmark's test crate is a scale reference, not the final chest.

## 12. Boss readability

Retain the existing neutral Stone Warden, not a replacement dragon or bear. A broad stone-armored knight, broken crown and heavy upper-body mass create a unique silhouette. Ivory face/shoulder planes separate it from ruins; feet and weapon remain visible. No team banners. Scale and crown height must be checked against current boss bounds before production approval.

The arena uses low-noise ground and no decorative circular marks resembling attacks. Keep warning outlines legible beneath the body, around walls and during all three existing attacks. Boss health bar includes name and a crown glyph. Do not alter attack parameters, HP, timers or arena access for visual reasons.

## 13. UI visual direction

Dark opaque or nearly opaque slate panels, warm ivory text, restrained brass framing, simple readable type and geometric icons. Medieval character comes from framing and icon silhouettes; avoid ornate body fonts, parchment noise and heavy filigree. Follow existing HUD locations and gameplay information architecture.

Health, stamina, equipment, interaction and danger each have consistent icons/text in addition to hue. Team glyphs accompany names. Boss and camp tier markers use the same symbols as world presentation. Check at 1280×720 and 1920×1080; target at least 4.5:1 contrast for ordinary text and 3:1 for large symbols against their panel. Measure UI separately from shaded world materials. No UI assets or runtime layout are replaced in M54.

## 14. Lighting rules

Use an overcast cool daylight baseline, broad directional key and soft ambient fill. Warm local lights identify architecture rather than outlining hidden players. Characters need visible head/torso/limb separation in shadow as well as sunlight. Keep exposure fixed for comparisons; no automatic adaptation that makes entering shade tactically unreadable. No team-dependent rim lights.

Fog may soften the far background, never the current encounter range or camp approach landmark. Avoid crushed blacks, heavy vignette, depth of field, strong motion blur, lens dirt and bloom halos on combat views. Weather and decorative torches cannot erase attack boundaries. Review both fortress orientations under identical settings; do not solve one team's readability with different lighting. Establish performance cost in Unity before approving extra real-time lights or transparent layers.

## Small visual benchmark

Offline fixture: `ArtSource/Benchmarks/VisualDirection/VisualDirectionBenchmark.blend`, generated by `Tools/ArtPipeline/create_visual_benchmark.py`. It appends existing prototype humanoid and test-crate meshes without overwriting their sources, uses only flat placeholder materials and primitives, and includes matched A/B figures and gate samples, a neutral, Stone Warden stand-in, chest silhouette, camp cairns and hostile sector/circle examples. It is a look-development board, not production art or a playable arena.

Named views: `Overview`, `Combat`, `Distance`. Combat uses perspective 60° horizontal framing at approximately the existing 5m follow distance. Overview is a presentation view, not a gameplay-camera proposal. Distance tests landmarks from approximately 30m. Save color and grayscale review images beside this document under `ArtDirection/`. Grayscale checks remove the primary color cue but do not replace color-vision-deficiency review.

Regenerate from the project root (adjust the executable path for the machine):

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/ArtPipeline/create_visual_benchmark.py -- .
```

The local tool is Blender 5.3.0 from Steam, which differs from the production export pipeline's pinned 4.5 LTS. This fixture performs no FBX export and does not upgrade that pipeline. Compatibility with the older Blender version is not yet verified.

Offline verification: the generator completed in Blender 5.3.0 with exit code 0, saved the approximately 214KB `.blend`, and produced the three named color/grayscale view pairs. Visual inspection of the overview and combat renders confirms source-mesh reuse, matched team plaques and distinct warning geometry. Initial distance inspection exposed a gate occluding the camp samples; those samples were moved to a separate clear sightline for the final fixture. This board intentionally retains T-pose mannequins and crude architectural blocks. It does not demonstrate finished character poses, environmental art, live hostile-state presentation or a passed distance-recognition test. Blender printed API deprecation notices and a denied user-extension-cache write; the benchmark and renders were saved inside the workspace.

### Unity handoff — required, not yet completed

Unity is not installed on the current machine and Unity MCP is unavailable. No `.unity`, prefab, material YAML or gameplay scene is manually authored. The Blender fixture does **not** satisfy the requested saved Unity benchmark scene or prove URP readability. M54 is partial until the following is completed on the main computer:

1. Through Unity MCP, create `Assets/Art/Environment/Scenes/VisualDirectionBenchmark.unity` as a separate scene, outside build scenes. Reuse `PV_PrototypeHumanoid`, `SM_TestCrate` and existing weapon visuals; create primitives for the same board layout. Use external placeholder URP Lit materials, preserving existing assets/metas.
2. Keep all networking, damage and gameplay scripts out of this static fixture. Configure the 60° camera at the existing 5m distance and a separate 30m landmark view. Match both gate samples, lighting and character coverage.
3. Review color, grayscale and common color-vision-deficiency simulations at 720p and 1080p. Review moving/overlapping hostile effects separately in the existing gameplay scene; static meshes cannot validate timing or remote state.
4. Save the scene and new assets through MCP; inspect Console and fix errors. Verify references, imported scale and required renderers. Enter Play Mode and capture evidence. Return to the previous working scene without modifying it.

### Acceptance gates

| Check | Target | Current status |
| --- | --- | --- |
| Team identity | A/B identified by crest or marker within one second at 5/15/25m, including grayscale and rear view | Pending human/Unity review |
| Hostile effect | Boundary and direction recognizable within half a second amid overlapping friendly VFX | Pending gameplay review |
| Chest | Distinct from ordinary crate at 10m; availability clear within interaction range | Pending Unity review |
| Camp | Tier/crown recognizable at 25–35m on clear approach | Pending Unity review |
| Actor visibility | Head/torso/feet separable against ground, gates and foliage in light and shade | Pending Unity review |
| Boss | Unique silhouette at approach; all three warning shapes visible at combat camera | Pending Unity review |
| Fairness | Both mirrored approaches comparable in landmark/actor visibility | Pending map review |

Targets are review criteria, not measured user-study results. Test local/remote players, death poses, equipment swaps and camera-wall cases before declaring a production look accepted.

## Risks and decisions

- Missing DESIGN.md may contain conflicting direction. Reconcile when available before expensive art production.
- Blender lighting and color management differ from URP. Approve materials only after the Unity handoff.
- Red team cloth and orange danger may compete. Glyphs, dark-backed boundaries and restricted cloth coverage are mandatory.
- Team crests and neutral/camp markers need later presentation integration; current tint alone does not meet this direction.
- Pale stone can swallow pale armor; separate through local value, roughness and silhouette before increasing saturation.
- Foliage, rubble, capes and decorative collision can change competitive visibility or imply false hitboxes. Review against existing footprints.
- Static benchmarks cannot validate latency, motion, effect density or six-player performance. Do not authorize a large asset batch from these renders alone.

## Asset priorities and first-biome inventory

Produce one representative asset per category and review it in-engine before expanding variants. Quantities below define a first-biome kit, not work completed in M54. Create only weapon and enemy families already used by the slice.

| Priority | Asset set | Initial inventory / acceptance purpose |
| --- | --- | --- |
| P0 | Hero visual | One compatible humanoid body/armor set, A/B cloth variants and two crest decals; silhouette and rig proof |
| P0 | Identity/interaction | A/B world/UI glyphs, neutral diamond, three tier glyphs, boss crown, danger glyph, interaction glyph; one chest with open/closed states |
| P0 | Combat weapon sample | One sword and shield, then one mesh per remaining equipped weapon family in the current catalog; grip/reach proof |
| P0 | Ground and lighting | One low-noise soil/stone surface set and shared look-development lighting preset |
| P1 | Fortress modules | Straight wall, corner, tower section, battlement, main gate frame/door, flank arch; shared A/B banner/crest variants |
| P1 | Neutral creatures | Wolf, bear and knight visuals compatible with current behaviors; no new enemy types |
| P1 | Boss | One Stone Warden visual, existing-action animation set and warning/impact VFX for cleave, sweep, stomp |
| P1 | Forest/rock kit | Two tree silhouettes, one stump, three rock masses, one sparse grass clump and one fern; camera/cover review |
| P1 | Ruins/camps | Broken wall, broken arch, column fragment; one modular cairn with tier treatments and contested crown variant |
| P2 | Accent kit | Corrupted stone variant, restrained crystal/root accent, torch/brazier, crate/barrel; decoration only after clarity passes |
| P2 | Shared surfaces/UI | Stone/wood/metal trim sheet, cloth/leather set, crest atlas, panel/button frames and existing HUD icon set |

All source models belong in `ArtSource/`; reviewed runtime exports go in `Assets/Art/` with Unity-generated metadata and follow ArtPipeline.md. Preserve current sources and GUIDs. No new runtime architecture, production asset library or gameplay changes are introduced by this milestone.
