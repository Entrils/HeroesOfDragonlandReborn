namespace HeroesOfDragonland.Progression
{
    public readonly struct LevelUpEvent
    {
        public int PreviousLevel { get; }
        public int Level { get; }
        public int PointsAwarded { get; }
        public long TotalProgressionPoints { get; }
        public LevelUpEvent(int level, int points, long total)
        { PreviousLevel = level - 1; Level = level; PointsAwarded = points; TotalProgressionPoints = total; }
    }
}
