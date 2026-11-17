using System;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class MatchSimulationValidation
    {
        public static int Validate()
        {
            int checks = 0;
            void Check(bool ok, string label) { if (!ok) throw new Exception("M52 invariant: " + label); checks++; }
            var ledger = new MatchSimulationLootLedger();
            Check(ledger.RegisterRoll("chest-a", 3), "first generation");
            Check(!ledger.RegisterRoll("chest-a", 3), "duplicate generation");
            Check(!ledger.Claim("unknown", 3), "unissued loot");
            Check(!ledger.Claim("chest-a", 4), "quantity inflation");
            Check(ledger.Claim("chest-a", 3), "legitimate transfer");
            Check(!ledger.Claim("chest-a", 3), "duplicate transfer");
            Check(ledger.RegisterRoll("chest-b", 3) && ledger.Claim("chest-b", 3), "same item/count distinct batch");
            ledger.Clear();
            Check(!ledger.Claim("chest-a", 3), "cross-round stale claim");
            Check(ledger.RegisterRoll("chest-a", 0) && ledger.Claim("chest-a", 0), "empty legitimate roll");
            var stall = new MatchSimulationStall();
            Check(!stall.Observe(Vector3.zero, true, 0, 10), "initial anchor");
            Check(!stall.Observe(Vector3.zero, true, 9, 10), "below threshold");
            Check(stall.Observe(Vector3.zero, true, 10, 10), "stuck threshold");
            Check(!stall.Observe(Vector3.zero, true, 20, 10), "one report per episode");
            Check(!stall.Observe(Vector3.zero, false, 40, 10), "legitimate attack/death/idle excluded");
            Check(!stall.Observe(Vector3.zero, true, 50, 10), "restart after idle");
            Check(!stall.Observe(Vector3.right * 2, true, 59, 10), "movement resets progress");
            Check(stall.Observe(Vector3.right * 2, true, 69, 10), "second distinct stall");
            new MatchSimulationOptions().Validate(); checks++;
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, -1f })
            {
                bool rejected = false;
                try { new MatchSimulationOptions { durationSeconds = bad }.Validate(); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "invalid wall limit");
            }
            Debug.Log("[M52] PASS " + checks + " loot/stall/configuration invariant checks");
            return checks;
        }
    }
}
