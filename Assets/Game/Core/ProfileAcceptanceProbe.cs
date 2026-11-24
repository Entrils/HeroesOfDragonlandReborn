#if UNITY_EDITOR || DEBUG
using System;
using HeroesOfDragonland.World;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Platform;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using UnityEngine;

namespace HeroesOfDragonland.Profiles
{
    internal static class ProfileAcceptanceProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (!LocalPlayerProfile.IsTestProfile) return;
            var args = Environment.GetCommandLineArgs();
            var profile = LocalPlayerProfile.Current;
            if (Array.IndexOf(args, "-hodrAchievementSeed") >= 0)
            {
                string id = Guid.NewGuid().ToString("N");
                var stats = new MatchStatistics { HighestLevel = 1, Kills = 1, CampsCleared = 10, KingsKilled = 1, BossesKilled = 1 };
                bool accepted = profile.RecordRound(id, true, 15f, stats) && !profile.RecordRound(id, true, 15f, stats);
                if (accepted) Debug.Log("[AchievementCheck] PASS seed and duplicate rejection");
                Application.Quit(accepted ? 0 : 1);
                return;
            }
            if (Array.IndexOf(args, "-hodrAchievementVerify") >= 0)
            {
                bool validAchievements = profile.Data.Totals.Matches == 1 && profile.Data.Totals.CampsCleared == 10
                    && profile.AchievementIds.Count == AchievementRules.Catalog.Count;
                foreach (var definition in AchievementRules.Catalog) validAchievements &= profile.HasAchievement(definition.Id);
                if (validAchievements) Debug.Log("[AchievementCheck] PASS all six achievements after restart");
                Application.Quit(validAchievements ? 0 : 1);
                return;
            }
            if (Array.IndexOf(args, "-hodrProfileSeed") >= 0)
            {
                profile.Rename("QA Knight");
                profile.Data.Settings.MouseSensitivity = 1.5f;
                profile.Data.Settings.MusicVolume = .25f;
                profile.Data.Cosmetics.BannerId = "qa-banner";
                profile.SetAppearance(FixtureAppearance());
                var collection = profile.CosmeticItems;
                if (collection.Owns("icon.crown") || collection.Equip("icon.crown") || !collection.Grant("icon.crown")
                    || !collection.Equip("icon.crown") || !collection.Equip("weapon.ember") || !collection.Equip("weapon.frost"))
                    throw new InvalidOperationException("[ProfileCheck] FAIL cosmetic ownership or equipping");
                collection.Equip("banner.dawn");
                collection.Unequip(CosmeticCategory.Banner);
                TutorialProgress.Complete();
            }
            if (Array.IndexOf(args, "-hodrProfileVerify") < 0) return;
            var data = profile.Data;
            bool valid = data.DisplayName == "QA Knight" && TutorialProgress.TutorialCompleted
                && data.Settings.MouseSensitivity == 1.5f && data.Settings.MusicVolume == .25f
                && profile.Appearance.Code == FixtureAppearance().Code && data.Cosmetics.BannerId == "qa-banner" && data.Totals.Matches == 2
                && profile.CosmeticItems.Owns("icon.crown") && profile.CosmeticItems.Equipped(CosmeticCategory.ProfileIcon)?.Id == "icon.crown"
                && profile.CosmeticItems.Owns("weapon.ember") && profile.CosmeticItems.Equipped(CosmeticCategory.WeaponSkin)?.Id == "weapon.frost"
                && profile.CosmeticItems.Equipped(CosmeticCategory.Banner) == null
                && data.Totals.ExperienceEarned == 1000 && data.Totals.ItemsAcquired >= 2
                && data.Totals.DamageTaken >= 20 && data.Totals.HighestLevel > 1 && data.Totals.MatchSeconds >= 6
                && profile.HasAchievement(AchievementRules.FirstVictory) && profile.HasAchievement(AchievementRules.DeathlessVictory)
                && profile.HasAchievement(AchievementRules.EnemyKing) == (data.Totals.KingsKilled > 0)
                && !profile.HasAchievement(AchievementRules.EnemyPlayerKill) && !profile.HasAchievement(AchievementRules.TenCamps)
                && data.Totals.Wins == 1 && data.Totals.Losses == 1 && data.RecordedRounds.Count == 2
                && !profile.RecordRound(data.RecordedRounds[0], true, 8, new MatchStatistics { Kills = 8, HighestLevel = 1 });
            if (!valid) { Debug.LogError("[ProfileCheck] FAIL persisted cosmetics, settings, results or round deduplication"); Application.Quit(1); }
            else { Debug.Log("[ProfileCheck] PASS settings, tutorial, cosmetic selections, achievements, wins/losses and deduplication after restart"); Application.Quit(); }
        }

        private static CharacterAppearance FixtureAppearance() => default(CharacterAppearance)
            .With(AppearanceSlot.Body, 2).With(AppearanceSlot.Face, 1).With(AppearanceSlot.Hairstyle, 1)
            .With(AppearanceSlot.HairColor, 3).With(AppearanceSlot.SkinTone, 2).With(AppearanceSlot.EyeColor, 1)
            .With(AppearanceSlot.Voice, 2).With(AppearanceSlot.Emblem, 3);
    }
}
#endif
