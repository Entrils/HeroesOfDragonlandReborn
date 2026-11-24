using System;
using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Platform;
using HeroesOfDragonland.Profiles;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var achievements = new List<string>();
        var totals = new ProfileTotals { CampsCleared = 9 };
        var stats = new MatchStatistics { Kills = 1, Deaths = 1, BossParticipation = 1 };
        AchievementRules.RecordCompletedMatch(achievements, totals, true, stats);
        if (achievements.Count != 2 || !achievements.Contains(AchievementRules.FirstVictory)
            || !achievements.Contains(AchievementRules.EnemyPlayerKill))
            throw new InvalidOperationException("Win, player kill, and participation conditions were misclassified.");
        totals.CampsCleared++;
        stats = new MatchStatistics { BossesKilled = 1, KingsKilled = 1 };
        AchievementRules.RecordCompletedMatch(achievements, totals, false, stats);
        if (achievements.Count != 5 || achievements.Contains(AchievementRules.DeathlessVictory))
            throw new InvalidOperationException("Lifetime threshold, objective kills, or deathless loss misclassified.");
        AchievementRules.RecordCompletedMatch(achievements, totals, true, stats);
        AchievementRules.RecordCompletedMatch(achievements, totals, true, stats);
        foreach (var entry in AchievementRules.Catalog)
            if (!achievements.Contains(entry.Id)) throw new InvalidOperationException("Missing achievement " + entry.Id);
        if (achievements.Count != 6) throw new InvalidOperationException("Duplicate achievement.");
        var initial = GamePlatform.Current;
        try
        {
            GamePlatform.Install(new TestPlatform());
            if (GamePlatform.DisplayName != "Example platform player") throw new InvalidOperationException("Display name did not resolve.");
            GamePlatform.Install(null);
            if (!string.IsNullOrEmpty(GamePlatform.Current.Identity.AccountId) || GamePlatform.Current.SupportsInvites
                || GamePlatform.Current.TryShowInvite("ABCDEFGHJK") || string.IsNullOrEmpty(GamePlatform.DisplayName))
                throw new InvalidOperationException("Offline provider did not use the local fallback.");
        }
        finally { GamePlatform.Install(initial); }
        result.Log("PASS: all six award rules, duplicate unlock prevention, and platform identity fallback.");
    }
}

internal sealed class TestPlatform : IGamePlatform
{
    public PlatformIdentity Identity => new("example-id", "Example platform player");
    public bool SupportsInvites => true;
    public event Action<string> PrivateRoomJoinRequested { add { } remove { } }
    public bool TryShowInvite(string code) => true;
    public bool TryUnlockAchievement(string id) => true;
    public void SetPresence(PlatformPresence presence) { }
    public void ClearPresence() { }
}
