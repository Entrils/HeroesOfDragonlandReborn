namespace HeroesOfDragonland.World.Camps
{
    // Server-side reward notification. A future XP service chooses recipients and
    // deduplicates by match/session ID + CampId; no client-supplied reward amount.
    public readonly struct CampExperienceReward
    {
        public string CampId { get; }
        public int Amount { get; }
        public CampExperienceReward(string campId, int amount)
        {
            CampId = campId;
            Amount = amount;
        }
    }
}
