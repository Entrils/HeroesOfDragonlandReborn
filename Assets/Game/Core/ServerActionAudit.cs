using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    internal enum ServerRejection
    {
        Sender, MovementVector, BowAim, InventorySlot, EquipmentSlot, SkillIndex,
        ChestReach, TrapPoint, PingPayload, AppearanceCode
    }

    // Diagnostic only. Bounded by fixed reason codes and admitted player identities.
    internal sealed class ServerActionAudit
    {
        private readonly Dictionary<(PlayerRef Player, ServerRejection Reason), double> next = new();
        private double windowEnd;
        private int written;

        internal bool ShouldRecord(PlayerRef player, ServerRejection reason, double now)
        {
            if (now >= windowEnd)
            {
                windowEnd = now + 10;
                written = 0;
                next.Clear();
            }
            if (written >= 16 || next.TryGetValue((player, reason), out double allowed) && now < allowed)
                return false;
            next[(player, reason)] = now + 2;
            written++;
            return true;
        }

        internal void Reset() { next.Clear(); windowEnd = 0; written = 0; }

        internal static void Record(NetworkRunner runner, PlayerRef player, ServerRejection reason)
        {
            if (runner == null || !runner.IsServer) return;
            var bootstrap = runner.GetComponentInParent<MultiplayerBootstrap>();
            if (bootstrap != null && bootstrap.ActionAudit.ShouldRecord(player, reason, Time.unscaledTimeAsDouble))
                Debug.LogWarning($"[ServerValidation] rejected {reason} player={player} tick={runner.Tick.Raw}", bootstrap);
        }
    }
}
