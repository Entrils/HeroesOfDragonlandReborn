using System;
using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Profiles;

namespace HeroesOfDragonland.Platform
{
    public static class AchievementRules
    {
        public const string FirstVictory = "FIRST_VICTORY";
        public const string EnemyPlayerKill = "KILL_ENEMY_PLAYER";
        public const string TenCamps = "CLEAR_10_CAMPS";
        public const string NeutralBoss = "DEFEAT_NEUTRAL_BOSS";
        public const string EnemyKing = "KILL_ENEMY_KING";
        public const string DeathlessVictory = "WIN_WITHOUT_DYING";

        public static IReadOnlyList<(string Id, string Title)> Catalog { get; } = Array.AsReadOnly(new[]
        {
            (FirstVictory, "First Victory"),
            (EnemyPlayerKill, "Kill an Enemy Player"),
            (TenCamps, "Clear 10 Camps"),
            (NeutralBoss, "Defeat a Neutral Boss"),
            (EnemyKing, "Kill the Enemy King"),
            (DeathlessVictory, "Win Without Dying")
        });

        public static bool IsKnown(string id)
        {
            foreach (var entry in Catalog) if (entry.Id == id) return true;
            return false;
        }

        public static void RecordCompletedMatch(ICollection<string> unlocked, ProfileTotals totals, bool won, MatchStatistics stats)
        {
            Unlock(FirstVictory, won);
            Unlock(EnemyPlayerKill, stats.Kills >= 1);
            Unlock(TenCamps, totals.CampsCleared >= 10);
            Unlock(NeutralBoss, stats.BossesKilled >= 1);
            Unlock(EnemyKing, stats.KingsKilled >= 1);
            Unlock(DeathlessVictory, won && stats.Deaths == 0);

            void Unlock(string id, bool earned)
            {
                if (earned && !unlocked.Contains(id)) unlocked.Add(id);
            }
        }
    }
}
