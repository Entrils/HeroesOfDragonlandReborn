using System;
using System.Linq;
using HeroesOfDragonland.Combat;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool ok, string context)
    {
        if (!ok) throw new InvalidOperationException("Combat attribution: " + context);
        checks++;
    }

    public void Execute(ExecutionResult result)
    {
        var history = new CombatContributionHistory<int>();
        bool Valid(int id) => id > 0 && id != 9;
        bool Allied(int a, int b) => a / 10 == b / 10;

        history.Record(1, 55, 0, 10);
        history.Record(2, 15, 7, 10);
        history.Record(3, 5, 7, 10);
        history.Record(3, 5, 8, 10);
        history.Record(4, 1, 10, 10);
        Check(history.Resolve(4, true, 10, 10, 10, Valid, Allied, out var killer, out var assists)
            && killer == 4 && assists.SequenceEqual(new[] { 1, 2, 3 }), "last hit keeps multiple meaningful assists, inclusive window");
        Check(history.Resolve(0, false, 10.01, 10, 10, Valid, Allied, out killer, out assists)
            && killer == 2 && assists.SequenceEqual(new[] { 3 }), "AI or environmental death uses strongest recent player");
        Check(history.Resolve(-1, false, 10.01, 10, 10, Valid, Allied, out killer, out assists)
            && killer == 2, "self-death preserves eligible enemy credit");
        Check(!history.Resolve(-1, false, 21, 10, 10, Valid, Allied, out _, out assists) && assists.Count == 0,
            "no self-death credit without recent hostile damage");

        history.Clear();
        history.Record(1, 100, 0, 10);
        history.Record(1, 1, 11, 10);
        Check(!history.Resolve(0, false, 11, 10, 2, Valid, Allied, out _, out _),
            "a recent scratch does not extend old damage");
        Check(history.Resolve(1, true, 11, 0, 2, Valid, Allied, out killer, out assists)
            && killer == 1 && assists.Count == 0, "confirmed low-damage finishing hit counts with zero window");

        history.Clear();
        history.Record(9, 100, 1, 10);
        history.Record(1, 15, 1, 10);
        history.Record(2, 15, 2, 10);
        history.Record(11, 12, 2, 10);
        Check(history.Resolve(9, true, 2, 10, 1, Valid, Allied, out killer, out assists)
            && killer == 2 && assists.SequenceEqual(new[] { 1 }), "excluded actor and contested neutral rival receive no credit; tie prefers latest hit");
        Check(history.Resolve(11, true, 2, 10, 1, Valid, Allied, out killer, out assists)
            && killer == 11 && assists.Count == 0, "opposing teams never share assists on a contested objective");
        history.Clear();
        history.Record(1, float.NaN, 3, 10);
        history.Record(2, -5, 3, 10);
        history.Record(3, 0, 3, 10);
        Check(!history.Resolve(0, false, 3, 10, 0, Valid, Allied, out _, out _),
            "leash, death or round reset clears past hits; invalid damage never contributes");
        result.Log($"PASS: {checks} automated combat attribution edge cases.");
    }
}
