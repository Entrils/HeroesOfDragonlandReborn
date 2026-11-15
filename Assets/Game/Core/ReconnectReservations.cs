using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Opaque per-process bearer credentials. Never serialized, logged or accepted as account identity.
    internal sealed class ReconnectReservations
    {
        internal const float GraceSeconds = 90;
        private sealed class Entry
        {
            internal PlayerRef PreviousOwner;
            internal NetworkObject Avatar;
            internal float Expires;
        }
        private readonly Dictionary<PlayerRef, string> connected = new();
        private readonly Dictionary<string, Entry> reserved = new();
        internal int Count => reserved.Count;
        private static string Key(byte[] token) => token != null && token.Length == 32 ? Convert.ToBase64String(token) : null;
        internal bool IsDuplicate(byte[] token, PlayerRef except = default)
        {
            string key = Key(token);
            if (key == null) return false;
            foreach (var pair in connected) if (pair.Key != except && pair.Value == key) return true;
            return false;
        }
        internal bool CanResume(byte[] token)
        {
            string key = Key(token);
            return key != null && reserved.TryGetValue(key, out var entry) && entry.Expires > Time.realtimeSinceStartup
                && entry.Avatar != null && entry.Avatar.IsValid;
        }
        internal void Track(PlayerRef player, byte[] token)
        {
            string key = Key(token);
            if (key != null) connected[player] = key;
        }
        internal bool Reserve(PlayerRef player, NetworkObject avatar)
        {
            if (!connected.Remove(player, out var key) || avatar == null || !avatar.IsValid) return false;
            reserved[key] = new Entry { PreviousOwner = player, Avatar = avatar, Expires = Time.realtimeSinceStartup + GraceSeconds };
            return true;
        }
        internal bool TryResume(byte[] token, PlayerRef player, out PlayerRef previous, out NetworkObject avatar)
        {
            previous = default; avatar = null;
            string key = Key(token);
            if (!CanResume(token) || !reserved.Remove(key, out var entry)) return false;
            previous = entry.PreviousOwner; avatar = entry.Avatar;
            connected[player] = key;
            return true;
        }
        internal void Forget(PlayerRef player) => connected.Remove(player);
        internal void Expire(NetworkRunner runner, NetworkTeamRoster roster, bool all = false)
        {
            foreach (var pair in new List<KeyValuePair<string, Entry>>(reserved))
            {
                if (!all && pair.Value.Expires > Time.realtimeSinceStartup) continue;
                reserved.Remove(pair.Key);
                roster.Remove(pair.Value.PreviousOwner.RawEncoded);
                var actor = pair.Value.Avatar;
                if (actor != null && actor.IsValid && runner != null && runner.IsRunning && runner.IsServer) runner.Despawn(actor);
                Debug.Log("[Resilience] Released disconnected reservation");
            }
        }
        internal void Clear() { connected.Clear(); reserved.Clear(); }
    }
}
