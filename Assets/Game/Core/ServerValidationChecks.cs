#if UNITY_EDITOR || DEBUG
using System;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public static class ServerValidationChecks
    {
        public static void Run()
        {
            Check(NetworkTestPlayer.ValidDirection(Vector2.one.normalized), "legal unit movement");
            Check(!NetworkTestPlayer.ValidDirection(new Vector2(2, 0)), "overspeed input");
            Check(!NetworkTestPlayer.ValidDirection(new Vector2(float.NaN, 0)), "nonfinite movement");
            Check(!NetworkTestPlayer.ValidDirection(new Vector2(float.MaxValue, 0)), "overflowing movement");
            Check(NetworkPlayerCombat.ValidBowIntent(Vector3.zero) && NetworkPlayerCombat.ValidBowIntent(Vector3.forward), "valid aim");
            Check(!NetworkPlayerCombat.ValidBowIntent(new Vector3(float.PositiveInfinity, 0, 0)), "nonfinite aim");
            Check(!NetworkPlayerCombat.ValidBowIntent(new Vector3(100, 0, 0)), "unbounded aim");

            var audit = new ServerActionAudit();
            Check(audit.ShouldRecord(PlayerRef.None, ServerRejection.Sender, 1), "initial rejection");
            Check(!audit.ShouldRecord(PlayerRef.None, ServerRejection.Sender, 2), "duplicate suppression");
            Check(audit.ShouldRecord(PlayerRef.None, ServerRejection.Sender, 3), "diagnostic can resume");
            for (int i = 0; i < 14; i++)
                Check(audit.ShouldRecord(PlayerRef.FromIndex(i), ServerRejection.BowAim, 3), "shared budget");
            Check(!audit.ShouldRecord(PlayerRef.None, ServerRejection.BowAim, 3), "global cap");
            Check(audit.ShouldRecord(PlayerRef.None, ServerRejection.BowAim, 11), "new log window");
            Debug.Log("[M87] PASS input bounds and diagnostic budget");
        }

        public static void RunLive(NetworkObject source, NetworkObject target)
        {
            var runner = source.Runner;
            Check(NetworkParticipant.TryResolveSender(runner, source.InputAuthority, out var actor) && actor == source,
                "resolve the authenticated connection avatar");
            Check(!NetworkParticipant.TryResolveSender(runner, PlayerRef.None, out _)
                && !NetworkParticipant.OwnsRequest(target, source.InputAuthority), "missing sender and non-owner denied");

            var receiver = target.GetComponent<DamageReceiver>();
            var stats = target.GetComponent<CharacterStats>();
            var initial = stats.CurrentHealth;
            Check(!source.GetComponent<NetworkPlayerCombat>().AllowsMeleeDamage(runner)
                && receiver.TakeDamage(1, source.gameObject) == 0
                && receiver.TakeDamage(float.NaN, source.gameObject) == 0
                && stats.CurrentHealth == initial, "unresolved damage cannot spend health");
            Debug.Log("[M87] PASS authenticated sender and live damage authorization");
        }

        private static void Check(bool accepted, string label)
        {
            if (!accepted) throw new InvalidOperationException("[M87] " + label);
        }
    }
}
#endif
