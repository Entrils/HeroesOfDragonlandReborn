using System;
using HeroesOfDragonland.Presentation.Vfx;

internal static class VfxPolicyValidation
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    public static int Main()
    {
        var budget = new VfxBudget(2);
        VfxTicket a, b, c, retired;
        Check(!budget.IsLive(new VfxTicket()), "Default lease is invalid");
        Check(budget.TryAcquire(0, 0, 1, out a, out retired), "First effect admitted");
        Check(!budget.TryAcquire(0, 0, 1, out c, out retired), "Per-kind cap enforced");
        Check(budget.ActiveCount == 1, "Rejected request cannot leak a slot");
        Check(budget.TryAcquire(1, 1, 2, out b, out retired), "Second effect admitted");
        Check(!budget.TryAcquire(2, 0, 2, out c, out retired), "Cosmetics cannot evict feedback");
        Check(budget.TryAcquire(2, 3, 2, out c, out retired), "Important effect preempts lower priority");
        Check(retired.Index == a.Index && retired.Generation == a.Generation, "Oldest lowest priority effect chosen");
        Check(!budget.IsLive(a) && budget.IsLive(b) && budget.IsLive(c), "Ticket ownership after preemption");
        Check(!budget.Release(a) && budget.IsLive(c), "Stale lease cannot stop reused slot");
        Check(budget.Release(c) && !budget.Release(c), "Release is idempotent");
        Check(budget.TryAcquire(3, 1, 1, out c, out retired), "Freed slot reused");
        Check(!budget.TryAcquire(4, 1, 1, out a, out retired), "Equal priority effects are protected");
        Check(budget.Release(b) && budget.Release(c) && budget.ActiveCount == 0, "Full release conserves capacity");
        Check(!budget.TryAcquire(-1, 1, 1, out a, out retired), "Invalid kind rejected");
        Check(!budget.TryAcquire(1, 4, 1, out a, out retired), "Invalid priority rejected");
        Check(!budget.TryAcquire(1, 1, 0, out a, out retired), "Invalid per-kind cap rejected");
        var sequence = new VfxSequence();
        Check(!sequence.Observe(28), "Late join seeds without replay");
        Check(!sequence.Observe(28), "Repeated snapshot is silent");
        Check(sequence.Observe(29), "New sequence emits once");
        Check(!sequence.Observe(27) && !sequence.Observe(29), "Stale samples do not rewind the baseline");
        Check(sequence.Observe(34) && !sequence.Observe(34), "Coalesced snapshots emit one bounded burst");
        sequence.Reset();
        Check(!sequence.Observe(0) && sequence.Observe(1), "Explicit round/lifecycle reset");
        Check(!sequence.Observe(-1), "Invalid negative sequence ignored");
        Console.WriteLine("PASS " + checks + " VFX budget/lease/sequence policy checks. Unity and network integration not exercised.");
        return 0;
    }
}
