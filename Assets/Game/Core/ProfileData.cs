using System;
using System.Collections.Generic;

namespace HeroesOfDragonland.Profiles
{
    [Serializable]
    public sealed class ProfileData
    {
        public string DisplayName = "Player";
        public ProfileSettings Settings = new();
        public bool TutorialCompleted;
        public bool TutorialSkipped;
        public ProfileTotals Totals = new();
        public CosmeticSelections Cosmetics = new();
        internal const int ReceiptLimit = 128;
        [UnityEngine.SerializeField] internal List<string> RecordedRounds = new();
    }

    [Serializable]
    public sealed class ProfileSettings
    {
        public float MasterVolume = .8f, MusicVolume = .65f, EffectsVolume = .85f;
        public float MouseSensitivity = 1;
        public bool Fullscreen, VSync, ReducedResolution;
        public float Volume(int index) => index == 0 ? MasterVolume : index == 1 ? MusicVolume : EffectsVolume;
        internal void SetVolume(int index, float value)
        {
            if (index == 0) MasterVolume = value;
            else if (index == 1) MusicVolume = value;
            else EffectsVolume = value;
        }
    }

    [Serializable]
    public sealed class ProfileTotals
    {
        public long Matches, Wins, Losses, Kills, Deaths, CampsCleared, KingsKilled, BossesKilled;
    }

    [Serializable]
    public sealed class CosmeticSelections
    {
        public string BannerId = "";
        public string AppearanceId = "";
    }
}
