using System;
using System.Linq;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Platform;
using System.Collections.Generic;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Profiles
{
    public interface IProfileStore
    {
        ProfileData Load();
        void Save(ProfileData profile);
    }

    internal sealed class PlayerPrefsProfileStore : IProfileStore
    {
        private readonly string key;
        internal PlayerPrefsProfileStore(string key) => this.key = key;
        public ProfileData Load()
        {
            string json = PlayerPrefs.GetString(key, "");
            if (json.Length == 0) return null;
            try { return JsonUtility.FromJson<ProfileData>(json); }
            catch (ArgumentException) { Debug.LogWarning("Local profile could not be read; loading defaults."); return null; }
        }
        public void Save(ProfileData profile)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }
    }

    public sealed class PlayerProfile
    {
        private readonly IProfileStore store;
        private readonly IRankedRatingModel ratingModel;
        public ProfileData Data { get; }
        public IReadOnlyList<string> AchievementIds { get; }
        public bool HasAchievement(string id) => Data.UnlockedAchievements.Contains(id);
        public CosmeticCollection CosmeticItems { get; }
        public PlayerProfile(IProfileStore store, Func<ProfileData> defaults = null, IRankedRatingModel ratingModel = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.ratingModel = ratingModel ?? new FixedStepRankedRating();
            Data = store.Load() ?? defaults?.Invoke() ?? new ProfileData();
            if (Data.Ranked.MatchesPlayed == 0) Data.Ranked = RankedPlayerData.New;
            Data.UnlockedAchievements ??= new List<string>();
            AchievementIds = Data.UnlockedAchievements.AsReadOnly();
            Data.Settings ??= new ProfileSettings();
            Data.Totals ??= new ProfileTotals();
            Data.Cosmetics ??= new CosmeticSelections();
            Data.CosmeticItems ??= new CosmeticCollectionData();
            CosmeticItems = new CosmeticCollection(Data.CosmeticItems, Save);
            Data.BuildPresets ??= new BuildPresetPreferences();
            Data.RecordedRounds ??= new System.Collections.Generic.List<string>();
            Data.DisplayName = CleanName(Data.DisplayName);
            var settings = Data.Settings;
            settings.MouseSensitivity = Clamp(settings.MouseSensitivity, .25f, 3, 1);
            for (int i = 0; i < 3; i++) settings.SetVolume(i, Clamp(settings.Volume(i), 0, 1, .8f));
        }
        public void Rename(string value) { Data.DisplayName = CleanName(value); Save(); }
        public SessionBuildPreset BuildPreset => SessionBuildPreset.Find(Data.BuildPresets.SelectedPresetId);

        public void SelectBuildPreset(SessionBuildPreset preset)
        {
            string id = preset?.Id ?? "";
            if (Data.BuildPresets.SelectedPresetId == id) return;
            Data.BuildPresets.SelectedPresetId = id;
            Save();
        }

        public CharacterAppearance Appearance
        {
            get
            {
                CharacterAppearance.TryDecode(Data.Cosmetics.AppearanceCode, out var appearance);
                return appearance;
            }
        }

        public CharacterAppearance EffectiveAppearance =>
            CosmeticItems.Equipped(CosmeticCategory.CharacterAppearance)?.Appearance ?? Appearance;

        public void SetAppearance(CharacterAppearance appearance)
        {
            if (Data.Cosmetics.AppearanceCode == appearance.Code) return;
            Data.Cosmetics.AppearanceCode = appearance.Code;
            Save();
        }

        public void Save() => store.Save(Data);
        internal bool RecordRound(string id, bool won, float duration, MatchStatistics statistics, RankedRatingSettings? ranked = null)
        {
            if (!Guid.TryParseExact(id, "N", out _) || Data.RecordedRounds.Contains(id)
                || !float.IsFinite(duration) || duration < 0 || !statistics.IsValid()) return false;
            if (ranked is { IsValid: true } settings) Data.Ranked = ratingModel.Apply(Data.Ranked, won, settings);
            var totals = Data.Totals;
            totals.Matches = Add(totals.Matches, 1);
            if (won) totals.Wins = Add(totals.Wins, 1);
            else totals.Losses = Add(totals.Losses, 1);
            totals.Kills = Add(totals.Kills, statistics.Kills);
            totals.Deaths = Add(totals.Deaths, statistics.Deaths);
            totals.Assists = Add(totals.Assists, statistics.Assists);
            totals.CampsCleared = Add(totals.CampsCleared, statistics.CampsCleared);
            totals.KingsKilled = Add(totals.KingsKilled, statistics.KingsKilled);
            totals.BossesKilled = Add(totals.BossesKilled, statistics.BossesKilled);
            totals.BossParticipation = Add(totals.BossParticipation, statistics.BossParticipation);
            totals.ChestsOpened = Add(totals.ChestsOpened, statistics.ChestsOpened);
            totals.GuardsKilled = Add(totals.GuardsKilled, statistics.GuardsKilled);
            totals.ExperienceEarned = Add(totals.ExperienceEarned, statistics.ExperienceEarned);
            totals.ItemsAcquired = Add(totals.ItemsAcquired, statistics.ItemsAcquired);
            totals.HighestLevel = Math.Max(totals.HighestLevel, statistics.HighestLevel);
            totals.PlayerDamage += statistics.PlayerDamage;
            totals.DamageTaken += statistics.DamageTaken;
            totals.Healing += statistics.Healing;
            totals.KingDamage += statistics.KingDamage;
            totals.MatchSeconds += duration;
            AchievementRules.RecordCompletedMatch(Data.UnlockedAchievements, totals, won, statistics);
            Data.RecordedRounds.Add(id);
            if (Data.RecordedRounds.Count > ProfileData.ReceiptLimit)
                Data.RecordedRounds.RemoveRange(0, Data.RecordedRounds.Count - ProfileData.ReceiptLimit);
            Save();
            return true;
        }
        private static long Add(long current, long amount) => Math.Min(Math.Max(0, current), long.MaxValue - amount) + amount;
        private static string CleanName(string value)
        {
            string name = new string((value ?? "").Where(c => !char.IsControl(c)).Take(28).ToArray()).Trim();
            return name.Length > 0 ? name : "Player";
        }
        internal static float Clamp(float value, float min, float max, float fallback) =>
            float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }

    public static class LocalPlayerProfile
    {
        private static PlayerProfile profile;
        private static readonly string TestId = GetTestId();
        internal static bool WriteMatchTotals { get; } = TestId != null || !Application.isBatchMode;
        internal static bool IsTestProfile => TestId != null;
        public static PlayerProfile Current => profile ??= new PlayerProfile(
            new PlayerPrefsProfileStore(TestId == null ? "HOD.Profile.v1" : "HOD.Profile.Test." + TestId),
            TestId == null ? ImportLegacy : null);
        private static string GetTestId()
        {
#if UNITY_EDITOR || DEBUG
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-hodrProfileTest");
            if (index >= 0 && index + 1 < args.Length && Guid.TryParse(args[index + 1], out var id)) return id.ToString("N");
#endif
            return null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => profile = null;
        private static ProfileData ImportLegacy() => new ProfileData
        {
            TutorialCompleted = PlayerPrefs.GetInt("HOD.TutorialCompleted") == 1,
            TutorialSkipped = PlayerPrefs.GetInt("HOD.TutorialSkipped") == 1,
            Settings = new ProfileSettings
            {
                MouseSensitivity = PlayerPrefs.GetFloat("HOD.Playtest.MouseMultiplier", 1),
                Fullscreen = PlayerPrefs.GetInt("HOD.Playtest.Fullscreen", Screen.fullScreen ? 1 : 0) != 0,
                VSync = PlayerPrefs.GetInt("HOD.Playtest.VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 0,
                ReducedResolution = PlayerPrefs.GetInt("HOD.Playtest.ReducedResolution") != 0,
                MasterVolume = PlayerPrefs.GetFloat("HOD.Audio.MasterVolumeDb", .8f),
                MusicVolume = PlayerPrefs.GetFloat("HOD.Audio.MusicVolumeDb", .65f),
                EffectsVolume = PlayerPrefs.GetFloat("HOD.Audio.SfxVolumeDb", .85f)
            }
        };
    }
}
