using System;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.World.Camps;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Match Balance")]
    public sealed class MatchBalanceConfig : ScriptableObject
    {
        [Serializable]
        public struct CampPacing
        {
            [SerializeField] private CampDifficulty difficulty;
            [SerializeField, Min(0)] private int experience;
            [SerializeField, Min(0)] private float firstSpawnSeconds;
            [SerializeField, Min(0)] private float respawnSeconds;
            [SerializeField, Min(0)] private float lateRespawnSeconds;
            [SerializeField, Min(0)] private float rewardRadius;
            [SerializeField, Min(1)] private float lateExperienceMultiplier;
            public CampDifficulty Difficulty => difficulty;
            public int Experience(bool late) => Mathf.RoundToInt(experience * (late ? lateExperienceMultiplier : 1));
            public float FirstSpawnSeconds => firstSpawnSeconds;
            public float RespawnSeconds(bool late) => late ? lateRespawnSeconds : respawnSeconds;
            public float RewardRadius => rewardRadius;
            internal bool IsValid => experience >= 0 && Valid(firstSpawnSeconds) && Valid(respawnSeconds)
                && Valid(lateRespawnSeconds) && Valid(rewardRadius) && rewardRadius > 0
                && Valid(lateExperienceMultiplier) && lateExperienceMultiplier >= 1;
        }

        [SerializeField] private LevelProgressionDefinition progression;
        [Header("Respawn: match age and level, identical rules for both teams")]
        [SerializeField, Min(0)] private float earlyRespawnSeconds = 5;
        [SerializeField, Min(0)] private float lateRespawnSeconds = 18;
        [SerializeField, Min(0)] private float respawnRampStartSeconds = 300;
        [SerializeField, Min(1)] private float respawnRampEndSeconds = 1200;
        [SerializeField, Min(0)] private float respawnSecondsPerLevel = .5f;
        [SerializeField, Min(0)] private float maximumRespawnSeconds = 22;
        [Header("Death: only unspent progress toward the next level can be lost")]
        [SerializeField, Range(0, 1)] private float deathProgressLossFraction;
        [SerializeField, Min(0)] private int maximumDeathExperienceLoss = 50;
        [Header("Confirmed player combat only")]
        [SerializeField, Min(0)] private int killExperience = 60;
        [SerializeField, Min(0)] private int assistExperiencePool = 30;
        [SerializeField, Min(0)] private float assistWindowSeconds = 12;
        [SerializeField, Min(0)] private float minimumContributionDamage = 1;
        [SerializeField, Min(0)] private float repeatedVictimRewardCooldown = 60;
        [Header("Visible PvE catch-up; never applied to combat stats or PvP XP")]
        [SerializeField, Min(0)] private float comebackLevelGap = .75f;
        [SerializeField, Min(0)] private float comebackBonusPerLevel = .2f;
        [SerializeField, Min(1)] private float maximumComebackMultiplier = 1.5f;
        [Header("Public late-match opportunities")]
        [SerializeField, Min(0)] private float lateMatchStartsAtSeconds = 600;
        [SerializeField, Min(0)] private float campContributionWindowSeconds = 60;
        [SerializeField] private CampPacing[] camps = Array.Empty<CampPacing>();
        public LevelProgressionDefinition Progression => progression;
        public int KillExperience => killExperience;
        public int AssistExperiencePool => assistExperiencePool;
        public float AssistWindowSeconds => assistWindowSeconds;
        public float MinimumContributionDamage => minimumContributionDamage;
        public float RepeatedVictimRewardCooldown => repeatedVictimRewardCooldown;
        public float LateMatchStartsAtSeconds => lateMatchStartsAtSeconds;
        public float CampContributionWindowSeconds => campContributionWindowSeconds;
        public float DeathProgressLossPercent => deathProgressLossFraction * 100;
        public int MaximumDeathExperienceLoss => maximumDeathExperienceLoss;
        public bool IsLate(float elapsed) => elapsed >= lateMatchStartsAtSeconds;
        public float RespawnDelay(float elapsed, int level) => Mathf.Clamp(
            Mathf.Lerp(earlyRespawnSeconds, lateRespawnSeconds, Mathf.InverseLerp(respawnRampStartSeconds, respawnRampEndSeconds, elapsed))
            + Mathf.Max(0, level - 1) * respawnSecondsPerLevel, 0, maximumRespawnSeconds);
        public long DeathExperienceLoss(long current) => Math.Min(Math.Max(0, current),
            Math.Min(maximumDeathExperienceLoss, (long)Math.Floor(Math.Max(0, current) * (double)deathProgressLossFraction)));
        public float ComebackMultiplier(float ownLevel, float opposingLevel)
        {
            float gap = opposingLevel - ownLevel;
            return gap < comebackLevelGap ? 1 : Mathf.Min(maximumComebackMultiplier, 1 + gap * comebackBonusPerLevel);
        }
        public bool TryGetCamp(CampDifficulty difficulty, out CampPacing profile)
        {
            foreach (var entry in camps) if (entry.Difficulty == difficulty) { profile = entry; return true; }
            profile = default; return false;
        }
        private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        public bool TryValidate(out string reason)
        {
            reason = "Invalid match balance configuration.";
            if (progression == null || !progression.TryValidate(out _)
                || !Valid(earlyRespawnSeconds) || !Valid(lateRespawnSeconds) || !Valid(respawnRampStartSeconds)
                || !Valid(respawnRampEndSeconds) || respawnRampEndSeconds <= respawnRampStartSeconds
                || !Valid(respawnSecondsPerLevel) || !Valid(maximumRespawnSeconds) || maximumRespawnSeconds < lateRespawnSeconds
                || !Valid(deathProgressLossFraction) || deathProgressLossFraction > 1 || maximumDeathExperienceLoss < 0
                || killExperience < 0 || assistExperiencePool < 0 || !Valid(assistWindowSeconds)
                || !Valid(minimumContributionDamage) || !Valid(repeatedVictimRewardCooldown)
                || !Valid(comebackLevelGap) || !Valid(comebackBonusPerLevel) || !Valid(maximumComebackMultiplier)
                || maximumComebackMultiplier < 1 || !Valid(lateMatchStartsAtSeconds) || !Valid(campContributionWindowSeconds)
                || camps == null || camps.Length != Enum.GetValues(typeof(CampDifficulty)).Length) return false;
            var seen = new System.Collections.Generic.HashSet<CampDifficulty>();
            foreach (var camp in camps) if (!camp.IsValid || !Enum.IsDefined(typeof(CampDifficulty), camp.Difficulty) || !seen.Add(camp.Difficulty)) return false;
            reason = null; return true;
        }
    }
}
