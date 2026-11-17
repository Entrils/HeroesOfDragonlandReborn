using System;
using HeroesOfDragonland.Audio;

internal static class AudioPolicyTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    public static int Main()
    {
        Check(AudioMath.Decibels(0) == -80, "Zero slider mutes without log(0)");
        Check(AudioMath.Decibels(1) == 0, "Full slider has unity gain");
        Check(Math.Abs(AudioMath.Decibels(.5f) + 6.0206f) < .001f, "Half amplitude maps to decibels");
        Check(AudioMath.Decibels(-1) == -80, "Negative preference clamped");
        Check(AudioMath.Decibels(2) == 0, "Corrupt high preference clamped");
        Check(AudioMath.Decibels(float.NaN) == -80, "NaN preference cannot reach native mixer");
        Check(AudioMath.Decibels(float.PositiveInfinity) == -80, "Infinity preference cannot reach native mixer");
        var budget = new AudioVoiceBudget(2);
        int a, b, c;
        int first = budget.Acquire(1, 10, 10, 2, 1, .2, out a);
        Check(first >= 0 && budget.ActiveCount == 1, "First admitted");
        Check(budget.Acquire(1, 10, 10, 2, 1.1, .2, out b) < 0, "Duplicate cue for same actor cooled down");
        int second = budget.Acquire(1, 11, 10, 2, 1.1, .2, out b);
        Check(second >= 0 && second != first, "Different actor not globally silenced by cooldown");
        Check(budget.Acquire(1, 12, 100, 2, 1.2, .2, out c) < 0, "Per-cue cap enforced even with high priority");
        Check(budget.Acquire(2, 12, 10, 2, 1.2, .2, out c) < 0, "Equal priority cannot steal");
        Check(budget.Acquire(2, 12, 5, 2, 1.2, .2, out c) < 0, "Low priority cannot steal");
        int urgent = budget.Acquire(2, 12, 100, 1, 1.2, .2, out c);
        Check(urgent >= 0 && budget.ActiveCount == 2, "King warning can replace a quiet footstep within fixed cap");
        Check(!budget.IsAlive(first, a) || !budget.IsAlive(second, b), "Stolen lease invalidated");
        budget.Release(urgent, urgent == first ? a : b);
        Check(budget.IsAlive(urgent, c), "Stale lease cannot stop replacement");
        budget.Release(urgent, c);
        Check(!budget.IsAlive(urgent, c) && budget.ActiveCount == 1, "Current lease releases slot");
        budget.Release(urgent, c);
        Check(budget.ActiveCount == 1, "Double release harmless");
        Check(budget.Acquire(2, 12, 100, 1, 1.25, .2, out c) < 0, "Completion does not bypass cooldown");
        Check(budget.Acquire(2, 12, 100, 1, 1.5, .2, out c) >= 0, "Cue becomes available after cooldown");
        Check(!budget.IsAlive(-1, 0) && !budget.IsAlive(50, 0), "Invalid lease safe");
        var bounded = new AudioVoiceBudget(24);
        for (int i = 0; i < 6000; i++)
        {
            int generation;
            bounded.Acquire(i % 24, i, 50, 4, i / 100.0, .1, out generation);
            if (bounded.ActiveCount > 24) throw new Exception("6-player burst escaped cap");
        }
        Check(bounded.ActiveCount == 24, "Sustained multiplayer burst remains bounded");
        var identities = new AudioVoiceBudget(4);
        long owner64 = (1L << 32) | 10;
        Check(identities.Acquire(1, owner64, 10, 4, 1, .5, out a) >= 0, "64-bit actor admitted");
        Check(identities.Acquire(1, 10, 10, 4, 1.1, .5, out b) >= 0, "Actors sharing low 32 bits have independent cooldowns");
        Check(identities.Acquire(1, owner64, 10, 4, 1.1, .5, out c) < 0, "Full 64-bit actor identity retains cooldown");
        Console.WriteLine("PASS: " + checks + " audio policy checks (including 6000-request burst).");
        return 0;
    }
}
