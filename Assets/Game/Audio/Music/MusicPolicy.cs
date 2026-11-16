using System;

namespace HeroesOfDragonland.Audio
{
    public enum MusicState { Lobby, MatchStart, Exploration, NeutralCampCombat, PvPCombat, BossEncounter, FortressAssault, KingDanger, Victory, Defeat }
    public enum MusicPhase { Lobby, Playing, Finished }
    public struct MusicContext
    {
        public int Session, Round;
        public MusicPhase Phase;
        public bool LocalActive, HasTeam, Won;
        public bool CampCombat, PvPCombat, Boss, Fortress, KingDanger;
    }
    [Serializable]
    public sealed class MusicTiming
    {
        public float matchStartSeconds = 3, campRelease = 4, pvpRelease = 6, bossRelease = 6, fortressRelease = 6, kingRelease = 8;
    }
    // Local presentation only. Lease expiry gives combat a release tail, not a new network state.
    public sealed class MusicPolicy
    {
        private readonly double[] until = new double[10];
        private static readonly MusicState[] CombatOrder = { MusicState.NeutralCampCombat, MusicState.PvPCombat, MusicState.FortressAssault, MusicState.BossEncounter, MusicState.KingDanger };
        private bool seeded;
        private int session, round;
        private MusicPhase phase;
        public void Reset() { seeded = false; Array.Clear(until, 0, until.Length); }
        public static int Priority(MusicState state)
        {
            switch (state)
            {
                case MusicState.KingDanger: return 100;
                case MusicState.BossEncounter: return 90;
                case MusicState.FortressAssault: return 80;
                case MusicState.PvPCombat: return 70;
                case MusicState.NeutralCampCombat: return 60;
                case MusicState.MatchStart: return 30;
                default: return 0;
            }
        }
        public void Signal(MusicState state, double now, double seconds)
        {
            if (state < MusicState.NeutralCampCombat || state > MusicState.KingDanger || double.IsNaN(now) || double.IsInfinity(now)) return;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            int i = (int)state; until[i] = Math.Max(until[i], now + Math.Max(0, Math.Min(30, seconds)));
        }
        public MusicState Evaluate(MusicContext context, double now, MusicTiming timing)
        {
            bool fresh = !seeded || session != context.Session;
            bool newRound = seeded && round != context.Round;
            bool intro = !fresh && context.Phase == MusicPhase.Playing && (phase != MusicPhase.Playing || newRound);
            if (fresh || newRound || context.Phase != MusicPhase.Playing) Array.Clear(until, 0, until.Length);
            session = context.Session; round = context.Round; phase = context.Phase; seeded = true;
            if (context.Phase == MusicPhase.Finished) return context.HasTeam ? (context.Won ? MusicState.Victory : MusicState.Defeat) : MusicState.Lobby;
            if (context.Phase != MusicPhase.Playing) return MusicState.Lobby;
            if (intro) until[(int)MusicState.MatchStart] = now + Math.Max(0, timing.matchStartSeconds);
            if (!context.LocalActive)
                for (int i = (int)MusicState.NeutralCampCombat; i <= (int)MusicState.FortressAssault; i++) until[i] = 0;
            else
            {
                if (context.CampCombat) Signal(MusicState.NeutralCampCombat, now, timing.campRelease);
                if (context.PvPCombat) Signal(MusicState.PvPCombat, now, timing.pvpRelease);
                if (context.Boss) Signal(MusicState.BossEncounter, now, timing.bossRelease);
                if (context.Fortress) Signal(MusicState.FortressAssault, now, timing.fortressRelease);
            }
            if (!context.HasTeam) until[(int)MusicState.KingDanger] = 0;
            else if (context.KingDanger) Signal(MusicState.KingDanger, now, timing.kingRelease);
            MusicState result = until[(int)MusicState.MatchStart] > now ? MusicState.MatchStart : MusicState.Exploration;
            foreach (var candidate in CombatOrder)
                if (until[(int)candidate] > now && Priority(candidate) > Priority(result)) result = candidate;
            return result;
        }
    }
}
