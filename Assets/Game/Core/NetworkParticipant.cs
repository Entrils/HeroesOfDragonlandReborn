using Fusion;

namespace HeroesOfDragonland.Multiplayer
{
    // Server admission for real connections and explicitly registered developer avatars.
    public static class NetworkParticipant
    {
        public static bool IsAdmitted(NetworkObject actor)
        {
            if (actor == null || !actor.IsValid || !actor.Runner.IsServer || !actor.HasStateAuthority) return false;
            if (actor.InputAuthority != PlayerRef.None)
                return actor.Runner.TryGetPlayerObject(actor.InputAuthority, out var owner) && owner == actor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var bots = actor.Runner.GetComponentInParent<DeveloperBotService>();
            return bots != null && bots.Contains(actor);
#else
            return false;
#endif
        }
    }
}
