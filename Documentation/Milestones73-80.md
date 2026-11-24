# Milestones 73–80 — Progression, presentation, and match flow

The combined `m82` commit contains the following systems. The tracked history establishes their implementation by that snapshot. The individual milestone boundaries within this range are unverified.

## Session skill tree and build presets

The Battlefield configuration has 17 nodes across Warrior, Agility, Magic and Survival. Nodes can require a level, attribute, or prerequisite skill rank. For example, Quick Strikes requires Endurance rank 2. Players can invest in multiple branches. The server validates purchases and activations, pays costs, and applies the effects. Ranks and active state reset with the round. The Editor installer assigns the tree to the Battlefield and Multiplayer Fortress player prefabs.

Vanguard, Duelist, Battlemage and Ranger presets display recommendations for attributes, skills, quick-slot potions and weapons. The selected preset is saved in the local profile. Spending points and equipping gear still go through the normal player actions.

See [skill tree configuration](../Assets/Game/Art/Editor/SkillTreeMilestoneSetup.cs), [network skill requests](../Assets/Game/Skills/NetworkSessionSkills.cs), [preset definitions](../Assets/Game/Skills/SessionBuildPreset.cs), and [the preset picker](../Assets/Game/UI/BuildPresetPicker.cs).

## Character appearance and cosmetics

The profile menus provide a Border Scout preview and saved appearance selections. The appearance code contains body, face, hairstyle, hair color, skin tone, eye color, voice and emblem choices. The model applies morphs and material changes for the supported options. The cosmetic catalog adds armor and weapon finishes, character presets, banners, emblems, victory gestures and profile icons. The local collection tracks ownership and equipment. Network presentation replicates the appearance and equipped visual choices used by peers.

See [appearance encoding](../Assets/Game/Presentation/CharacterAppearance.cs), [Border Scout setup](../Assets/Game/Art/Editor/CharacterAppearanceSetup.cs), [cosmetic catalog](../Assets/Game/Presentation/CosmeticDefinition.cs), [local collection](../Assets/Game/Core/CosmeticCollection.cs), and [network appearance](../Assets/Game/Player/NetworkTestPlayer.Appearance.cs).

## Combat credit and death recap

The server records damage contributions for players, fortress guards and the neutral boss. It resolves kill and assist credit from eligible contributions. The defaults in the Battlefield asset are 60 kill XP and a 30 XP assist pool for players, 20 and 10 for guards, and 100 and 50 for the boss. The repeat-victim cooldown checks each PvP reward recipient independently. Camp XP and the catch-up multiplier have their own participation rules.

The dying player's HUD gets the source, final damage action, recent hits, and any credited assists. History retains up to four entries within ten seconds. The recap is sent only to the owner and expires after eight seconds or when the current death state ends.

See [combat attribution](../Assets/Game/Combat/MatchCombatAttribution.cs), [match balance asset](../Assets/Game/Data/Battlefield/MatchBalance.asset), [recap history](../Assets/Game/Combat/DeathRecapHistory.cs), and [respawn and recap delivery](../Assets/Game/Player/NetworkPlayerRespawn.cs).

## Allied spectator view and match results

During respawn, use **Page Up** and **Page Down** to cycle living, connected allies. The spectator camera draws the selected ally and public scenery. When the ally pool is empty it has no target, and at respawn or match end the normal camera returns. The spectator view excludes live enemy actors and their positional audio.

After the King falls, the server publishes a result with the winner, duration, team totals and individual statistics such as kills, assists, damage, healing, camps, chests, XP, and King damage. The lobby can reopen the last result. The server sends each eligible human a profile receipt, which the profile deduplicates by result ID.

See [spectator camera](../Assets/Game/World/BattlefieldSpectator.cs), [result schema](../Assets/Game/Core/MatchResult.cs), [result publication](../Assets/Game/Core/NetworkMatch.cs), and [results screen](../Assets/Game/UI/MatchResultsScreen.cs).

## Model-pack integration

The model sources arrived in `1f87734`. The combined snapshot includes weapon mounts, enemy and projectile presentation, fortress gate state, and training interactions. The [original model-pack handoff](ModelPackUnityPrompt.txt) lists the art targets. The tracked setup and probes provide a way to check the bindings.

- In a rendered Battlefield host/client session, check Iron Sword, Dragonblade, Greatsword, Spear, and the Player Staff visual. Run the opt-in [model-pack probe](../Assets/Game/Presentation/ModelPackAcceptanceProbe.cs) with `-hodrModelPackCheck` for standard weapons and enemy presentation, or `-hodrModelPackDragonblade` for the sword comparison. The staff has a legacy Intelligence requirement and separate visual coverage.
- Check both sets of fortress gates before and after a breach. The `-hodrModelPackLate` client probe checks breached visuals and disabled blockers after reconnect. The [Editor gate validation](../Assets/Game/Art/Editor/BattlefieldModelPlayValidation.cs) exercises both teams' gates and their map state.
- In Training Grounds, play through the dummy, sparring guard, lever, and chest. Check hit timing, the lever transition and the weapon visual from the player camera.

## Verification

```powershell
./Tests/Standalone/RunSessionSkills.ps1 -Executable $player -VerifyAppearance
./Tests/Standalone/RunPlaytestFeature.ps1 -Feature Pacing -Executable $player
./Tests/Standalone/RunGameplayHudRespawn.ps1 -Executable $player -SpectatorCheck
```

The skills harness exercises both peers, prerequisites, server authority, resource use, cooldowns, and distinct appearances across round reset. The pacing harness checks rewards, the private death recap, catch-up camp XP and round reset. The respawn harness checks switching allies, losing targets, camera restoration and match end. The scripts print their log directories under `Builds/`. For visual acceptance, review customization, spectator transitions, and the results panel in a rendered player.
