#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using HeroesOfDragonland.World;
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
            if (Array.IndexOf(args, "-hodrProfileSeed") >= 0)
            {
                profile.Rename("QA Knight");
                profile.Data.Settings.MouseSensitivity = 1.5f;
                profile.Data.Settings.MusicVolume = .25f;
                profile.Data.Cosmetics.BannerId = "qa-banner";
                TutorialProgress.Complete();
            }
            if (Array.IndexOf(args, "-hodrProfileVerify") < 0) return;
            var data = profile.Data;
            bool valid = data.DisplayName == "QA Knight" && TutorialProgress.TutorialCompleted
                && data.Settings.MouseSensitivity == 1.5f && data.Settings.MusicVolume == .25f
                && data.Cosmetics.BannerId == "qa-banner" && data.Totals.Matches == 2
                && data.Totals.Wins == 1 && data.Totals.Losses == 1 && data.RecordedRounds.Count == 2
                && !profile.RecordRound(data.RecordedRounds[0], true, 8, 8, 8, 8, 8);
            if (!valid) { Debug.LogError("[ProfileCheck] FAIL persisted settings, results or round deduplication"); Application.Quit(1); }
            else { Debug.Log("[ProfileCheck] PASS settings, tutorial, cosmetics, wins/losses and deduplication after restart"); Application.Quit(); }
        }
    }
}
#endif
