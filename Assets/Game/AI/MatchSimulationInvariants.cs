#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // A batch represents one real chest roll, not an item definition shared by many items.
    public sealed class MatchSimulationLootLedger
    {
        private readonly Dictionary<string, long> rolls = new();
        private readonly HashSet<string> claims = new();
        public bool RegisterRoll(string id, long amount) => amount >= 0 && rolls.TryAdd(id, amount);
        public bool Claim(string id, long amount) => rolls.TryGetValue(id, out long generated)
            && amount == generated && claims.Add(id);
        public void Clear() { rolls.Clear(); claims.Clear(); }
    }

    public sealed class MatchSimulationStall
    {
        private Vector3 anchor;
        private double lastProgress;
        private bool tracking, reported;
        public bool Observe(Vector3 position, bool eligible, double now, double threshold)
        {
            if (!eligible || !tracking || (position - anchor).sqrMagnitude > 1)
            { anchor = position; lastProgress = now; tracking = eligible; reported = false; return false; }
            if (reported || now - lastProgress < threshold) return false;
            reported = true; return true;
        }
    }

    // Only installed during an explicitly requested simulation. Main-thread loot boundary.
    public static class MatchSimulationLootAudit
    {
        public static event Action<string, long, string, bool> Recorded;
        internal static void Roll(string id, long count) => Recorded?.Invoke(id, count, null, false);
        internal static void Claim(string id, long count, string owner) => Recorded?.Invoke(id, count, owner, true);
    }
}
#endif
