using System;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.UI;
using HeroesOfDragonland.World.Camps;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class MatchPacingValidation
    {
        private static int checks;
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("[MatchPacingValidation] FAIL " + name); checks++; }
        [MenuItem("Heroes of Dragonland/Battlefield/Validate Match Pacing In Play Mode")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            checks = 0;
            var config = AssetDatabase.LoadAssetAtPath<MatchBalanceConfig>(MatchPacingSetup.ConfigPath);
            Check(config != null && config.TryValidate(out _), "valid centralized config");
            Check(config.RespawnDelay(0, 1) == 5, "early respawn");
            Check(config.RespawnDelay(300, 1) == 5, "ramp boundary");
            Check(config.RespawnDelay(750, 1) == 11.5f, "linear midpoint");
            Check(config.RespawnDelay(1200, 1) == 18, "late respawn");
            Check(config.RespawnDelay(9999, 999) == 22, "respawn cap");
            Check(config.RespawnDelay(0, 3) == 6, "level respawn scaling");
            Check(config.ComebackMultiplier(2, 2) == 1 && config.ComebackMultiplier(4, 1) == 1, "no bonus for parity/leader");
            Check(config.ComebackMultiplier(1, 1.5f) == 1, "gap threshold");
            Check(Mathf.Approximately(config.ComebackMultiplier(1, 2), 1.2f), "visible catchup formula");
            Check(config.ComebackMultiplier(1, 6) == 1.5f, "catchup cap");
            Check(config.DeathExperienceLoss(500) == 0, "default death XP retention");
            Check(!config.IsLate(599) && config.IsLate(600), "late opportunity boundary");
            foreach (CampDifficulty tier in Enum.GetValues(typeof(CampDifficulty)))
            {
                Check(config.TryGetCamp(tier, out var camp), "central profile " + tier);
                Check(camp.Experience(true) >= camp.Experience(false), "late XP opportunity " + tier);
                Check(camp.RespawnSeconds(true) <= camp.RespawnSeconds(false), "late turnover " + tier);
            }
            Check(config.TryGetCamp(CampDifficulty.Tier2, out var flank) && flank.Experience(true) == 120, "flank alternative value");
            Check(config.TryGetCamp(CampDifficulty.Contested, out var boss) && boss.Experience(false) == 360 && boss.Experience(true) == 540, "contested high value");
            var copy = UnityEngine.Object.Instantiate(config); var actor = new GameObject("M49 progression validation");
            try
            {
                var fields = new SerializedObject(copy); fields.FindProperty("deathProgressLossFraction").floatValue = .5f; fields.ApplyModifiedPropertiesWithoutUndo();
                Check(copy.DeathExperienceLoss(160) == 50 && copy.DeathExperienceLoss(20) == 10, "configurable capped death loss");
                var stats = actor.AddComponent<CharacterStats>(); var progression = actor.AddComponent<SessionProgression>();
                fields = new SerializedObject(progression); fields.FindProperty("definition").objectReferenceValue = config.Progression; fields.ApplyModifiedPropertiesWithoutUndo();
                Check(progression.TryBeginSession("M49", out _), "progression session");
                Check(progression.TryAddExperience(140, "M49", "seed", out _) && progression.Level == 2 && progression.CurrentXP == 40, "earned level");
                Check(progression.TryIncreaseAttribute(ItemStat.Strength, "M49", out _), "attribute allocation");
                float maxHealth = stats.MaxHealth;
                Check(progression.TryApplyDeathLoss(20, "M49", "death:1", out _) && progression.CurrentXP == 20, "death loss only current progress");
                Check(!progression.TryApplyDeathLoss(20, "M49", "death:1", out _) && progression.CurrentXP == 20, "death idempotence");
                Check(!progression.TryApplyDeathLoss(20, "old-session", "death:2", out _), "stale session denied");
                Check(progression.TryApplyDeathLoss(999, "M49", "death:2", out _) && progression.CurrentXP == 0 && progression.Level == 2, "no delevel");
                Check(progression.GetAllocatedPoints(ItemStat.Strength) == 1 && stats.MaxHealth == maxHealth, "no stat rubber band or attribute loss");
                progression.SetAuthority(false);
                Check(!progression.TryApplyDeathLoss(1, "M49", "death:3", out _), "replica death mutation denied");
                Check(!progression.TryAddExperience(10, "M49", "forged", out _), "replica XP denied");
                fields = new SerializedObject(copy); fields.FindProperty("respawnRampEndSeconds").floatValue = 0; fields.ApplyModifiedPropertiesWithoutUndo();
                Check(!copy.TryValidate(out _), "invalid pacing fails validation");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(copy); }
            var matchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/Battlefield/BattlefieldMatch.prefab");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab");
            Check(matchPrefab.GetComponent<NetworkMatch>().Balance == config && matchPrefab.GetComponent<NetworkMatchPacing>() != null, "match wiring");
            Check(playerPrefab.GetComponent<NetworkPlayerExperienceRewards>() != null, "player rewards wiring");
            Check(UnityEngine.Object.FindAnyObjectByType<MatchPacingPresenter>() != null, "public HUD wiring");
            Debug.Log("[MatchPacingValidation] PASS " + checks + " config, respawn, XP, comeback, late opportunities, authority and saved reference assertions.");
        }
    }
}
