using System;
using HeroesOfDragonland.Audio;

internal static class MusicPolicyTests
{
    private static int checks;
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    public static int Main()
    {
        var policy = new MusicPolicy(); var timing = new MusicTiming();
        var frame = new MusicContext { Session = 1, Round = 0, Phase = MusicPhase.Lobby, HasTeam = true, LocalActive = true };
        Check(policy.Evaluate(frame, 1, timing) == MusicState.Lobby, "Initial lobby");
        frame.Round = 1; frame.Phase = MusicPhase.Playing;
        Check(policy.Evaluate(frame, 2, timing) == MusicState.MatchStart, "Live round transition starts intro");
        Check(policy.Evaluate(frame, 2.1, timing) == MusicState.MatchStart, "Intro not restarted/extended by sampling");
        Check(policy.Evaluate(frame, 5, timing) == MusicState.Exploration, "Intro expires exactly once");
        frame.CampCombat = true;
        Check(policy.Evaluate(frame, 6, timing) == MusicState.NeutralCampCombat, "Camp combat");
        frame.PvPCombat = true;
        Check(policy.Evaluate(frame, 7, timing) == MusicState.PvPCombat, "PvP over camp");
        frame.Fortress = true;
        Check(policy.Evaluate(frame, 8, timing) == MusicState.FortressAssault, "Fortress over ordinary PvP");
        frame.Boss = true;
        Check(policy.Evaluate(frame, 9, timing) == MusicState.BossEncounter, "Boss over fortress/PvP");
        frame.KingDanger = true;
        Check(policy.Evaluate(frame, 10, timing) == MusicState.KingDanger, "King over boss");
        frame.KingDanger = frame.Boss = frame.Fortress = frame.PvPCombat = frame.CampCombat = false;
        Check(policy.Evaluate(frame, 11, timing) == MusicState.KingDanger, "Brief signal loss does not flap music");
        Check(policy.Evaluate(frame, 18, timing) == MusicState.Exploration, "All combat tails expire");
        policy.Signal(MusicState.PvPCombat, 20, 2);
        Check(policy.Evaluate(frame, 20.1, timing) == MusicState.PvPCombat, "Future local hook");
        frame.LocalActive = false;
        Check(policy.Evaluate(frame, 20.2, timing) == MusicState.Exploration, "Death/suspension clears local combat");
        frame.KingDanger = true;
        Check(policy.Evaluate(frame, 21, timing) == MusicState.KingDanger, "Own King warning remains relevant during respawn");
        frame.HasTeam = false;
        Check(policy.Evaluate(frame, 21.1, timing) == MusicState.Exploration, "Unassigned spectator has no King danger");
        frame.HasTeam = true; frame.Phase = MusicPhase.Finished; frame.Won = true;
        Check(policy.Evaluate(frame, 22, timing) == MusicState.Victory, "Result overrides danger");
        frame.Won = false;
        Check(policy.Evaluate(frame, 22.1, timing) == MusicState.Defeat, "Defender losing result");
        frame.HasTeam = false;
        Check(policy.Evaluate(frame, 22.2, timing) == MusicState.Lobby, "No false defeat for spectator");
        frame.Phase = MusicPhase.Playing; frame.HasTeam = frame.LocalActive = true; frame.KingDanger = false; frame.Round = 2;
        Check(policy.Evaluate(frame, 23, timing) == MusicState.MatchStart, "Rematch clears previous result");
        frame.Session = 2;
        Check(policy.Evaluate(frame, 23.1, timing) == MusicState.Exploration, "Reconnect into active match has no historical intro");
        frame.Round++;
        Check(policy.Evaluate(frame, 24, timing) == MusicState.MatchStart, "New round even if intermediate lobby snapshots coalesced");
        policy.Signal(MusicState.KingDanger, 24, double.NaN);
        Check(policy.Evaluate(frame, 24.1, timing) == MusicState.MatchStart, "Invalid future hook ignored");
        frame.Session++; frame.Boss = true;
        Check(policy.Evaluate(frame, 25, timing) == MusicState.BossEncounter, "Late join can resolve current boss context");

        var decks = new MusicDecks<object>(); var a = new object(); var b = new object(); var c = new object(); var d = new object();
        decks.Request(a, true, 1); int revisionA = decks.Revision(0);
        decks.Tick(.5f);
        Check(Math.Abs(decks.Gain(0) - .5f) < .001, "Initial fade from silence");
        decks.Request(a, true, 1); decks.Tick(.5f);
        Check(!decks.Fading && decks.Revision(0) == revisionA && decks.Gain(0) == 1, "Identical state does not restart track or fade");
        decks.Request(b, true, 1); decks.Tick(.5f); int revisionB = decks.Revision(1);
        Check(Math.Abs(decks.Gain(0) + decks.Gain(1) - 1) < .001, "Crossfade bounded at unity combined gain");
        decks.Request(a, true, 1);
        Check(decks.Gain(0) == .5f && decks.Gain(1) == .5f, "Reversal preserves instantaneous gain");
        decks.Tick(1);
        Check(ReferenceEquals(decks.Clip(0), a) && decks.Clip(1) == null && decks.Revision(0) == revisionA, "Reversal resumes existing track without play restart");
        decks.Request(b, true, 1); decks.Tick(.25f);
        decks.Request(c, true, 1); decks.Request(d, true, 1);
        Check(ReferenceEquals(decks.Clip(0), a) && ReferenceEquals(decks.Clip(1), b), "Third request never replaces an audible deck");
        decks.Tick(.75f);
        Check(ReferenceEquals(decks.Clip(0), d) && decks.Gain(0) == 0 && decks.Gain(1) == 1, "Latest pending track starts only on silent deck");
        decks.Tick(1);
        Check(decks.Clip(1) == null && ReferenceEquals(decks.Clip(0), d), "Pending transition completes");
        int revisionD = decks.Revision(0);
        decks.Request(d, false, 1); decks.Tick(1);
        Check(!decks.Loop(0) && decks.Revision(0) == revisionD, "Loop flag adjustment does not restart same clip");
        decks.Request(null, false, 1); decks.Tick(.5f);
        Check(decks.Gain(0) == .5f, "Graceful stop");
        decks.Tick(.5f);
        Check(decks.Clip(0) == null && decks.Clip(1) == null, "Stop releases both decks");
        decks.Request(a, true, 1); decks.Tick(.25f); decks.Request(b, true, 1); decks.Request(a, true, 1); decks.Tick(1);
        Check(ReferenceEquals(decks.Clip(0), a) && decks.Clip(1) == null, "Returning to current target cancels obsolete queued request");
        decks.Reset();
        Check(!decks.Fading && decks.Clip(0) == null && decks.Gain(0) == 0, "Lifecycle reset clears pending music");
        decks.Request(a, true, .2f); decks.Tick(.2f);
        decks.Request(b, true, 1); decks.Tick(.2f); decks.Request(c, true, 1);
        decks.Request(null, false, .2f); decks.Tick(.2f); decks.Tick(5);
        Check(decks.Clip(0) == null && decks.Clip(1) == null, "Stop cancels queued track as well as active fade");
        var choices = new[] { a, b, c, d };
        for (int i = 0; i < 10000; i++)
        {
            decks.Request(choices[i % choices.Length], true, .2f); decks.Tick(.01f);
            Check(decks.Gain(0) >= 0 && decks.Gain(1) >= 0 && decks.Gain(0) + decks.Gain(1) <= 1.0001f, "Rapid state changes retain bounded continuous mix");
        }
        decks.Request(d, true, .2f); decks.Tick(.3f); decks.Tick(.3f);
        Check((ReferenceEquals(decks.Clip(0), d) && decks.Gain(0) == 1) || (ReferenceEquals(decks.Clip(1), d) && decks.Gain(1) == 1), "Burst settles on latest context");
        Console.WriteLine("PASS: " + checks + " music policy/crossfade checks."); return 0;
    }
}
