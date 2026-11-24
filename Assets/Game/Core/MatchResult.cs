using System;
using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Multiplayer
{
    public struct MatchStatistics : INetworkStruct
    {
        public int Kills, Deaths, Assists, CampsCleared, BossParticipation, ChestsOpened, HighestLevel, GuardsKilled, KingsKilled, BossesKilled;
        public long ExperienceEarned, ItemsAcquired;
        public float PlayerDamage, DamageTaken, Healing, KingDamage;

        internal void Add(MatchStatistics other)
        {
            Kills += other.Kills;
            Deaths += other.Deaths;
            Assists += other.Assists;
            CampsCleared += other.CampsCleared;
            BossParticipation += other.BossParticipation;
            ChestsOpened += other.ChestsOpened;
            ExperienceEarned = AddUnits(ExperienceEarned, other.ExperienceEarned);
            HighestLevel = Math.Max(HighestLevel, other.HighestLevel);
            GuardsKilled += other.GuardsKilled;
            KingsKilled += other.KingsKilled;
            BossesKilled += other.BossesKilled;
            ItemsAcquired = AddUnits(ItemsAcquired, other.ItemsAcquired);
            PlayerDamage = AddAmount(PlayerDamage, other.PlayerDamage);
            DamageTaken = AddAmount(DamageTaken, other.DamageTaken);
            Healing = AddAmount(Healing, other.Healing);
            KingDamage = AddAmount(KingDamage, other.KingDamage);
        }

        internal static long AddUnits(long current, long added) => Math.Min(current, long.MaxValue - added) + added;
        private static float AddAmount(float current, float added) => (float)Math.Min(float.MaxValue, (double)current + added);

        internal bool IsValid() => Kills >= 0 && Deaths >= 0 && Assists >= 0 && CampsCleared >= 0
            && BossParticipation >= 0 && ChestsOpened >= 0 && HighestLevel >= 1 && GuardsKilled >= 0 && KingsKilled >= 0
            && BossesKilled >= 0 && ExperienceEarned >= 0 && ItemsAcquired >= 0 && ValidAmount(PlayerDamage) && ValidAmount(DamageTaken)
            && ValidAmount(Healing) && ValidAmount(KingDamage);
        private static bool ValidAmount(float value) => float.IsFinite(value) && value >= 0;
    }

    public struct MatchPlayerResult : INetworkStruct
    {
        public int ParticipantNumber;
        public TeamId Team;
        public NetworkBool IsBot;
        public MatchStatistics Statistics;
    }

    public sealed class MatchResult
    {
        public string Id { get; }
        public int RoundNumber { get; }
        public bool Rated { get; }
        public TeamId Winner { get; }
        public float DurationSeconds { get; }
        public IReadOnlyList<MatchPlayerResult> Players { get; }
        public MatchStatistics TeamA { get; }
        public MatchStatistics TeamB { get; }

        internal MatchResult(string id, int round, TeamId winner, float duration, MatchPlayerResult[] players, bool rated = false)
        {
            Id = id;
            RoundNumber = round;
            Winner = winner;
            DurationSeconds = duration;
            Rated = rated;
            Players = Array.AsReadOnly(players);
            MatchStatistics a = default, b = default;
            foreach (var row in players)
            {
                if (row.Team == TeamId.TeamA) a.Add(row.Statistics);
                if (row.Team == TeamId.TeamB) b.Add(row.Statistics);
            }
            TeamA = a;
            TeamB = b;
        }
    }
}
