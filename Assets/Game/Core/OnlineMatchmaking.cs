using System.Collections.Generic;
using System.Security.Cryptography;
using Fusion;

namespace HeroesOfDragonland.Multiplayer
{
    public enum OnlineSessionKind { CustomLobby, QuickPlay, PrivateMatch, Ranked }

    // This request represents one local entrant. Party membership can be added before
    // creating a request, independently from lobby readiness or match team placement.
    public readonly struct OnlineParty
    {
        public static OnlineParty Solo => new OnlineParty(1);
        public int Size { get; }
        private OnlineParty(int size) => Size = size;
    }

    internal static class OnlineMatchmaking
    {
        private const string PrivatePrefix = "pm69-";
        private const string CustomPrefix = "custom69-";
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 10;
        // Change the compatibility revision whenever builds cannot play together.
        internal const int CompatibilityRevision = 3;
        internal static bool IsQueue(OnlineSessionKind kind) => kind is OnlineSessionKind.QuickPlay or OnlineSessionKind.Ranked;

        internal static string QueueName(OnlineSessionKind kind)
        {
            string queue = (kind == OnlineSessionKind.Ranked ? "hodr-ranked" : "hodr-quickplay") + "-3v3-v" + CompatibilityRevision;
#if UNITY_EDITOR || DEBUG
            var args = System.Environment.GetCommandLineArgs();
            int option = System.Array.IndexOf(args, "-hodrQuickQueue");
            if (option >= 0 && option + 1 < args.Length && System.Guid.TryParse(args[option + 1], out var id))
                return queue + "-" + id.ToString("N");
#endif
            return queue;
        }

        internal static Dictionary<string, SessionProperty> QueueProperties(OnlineSessionKind kind) =>
            new() { ["v"] = CompatibilityRevision, ["m"] = "battlefield-3v3", ["q"] = kind == OnlineSessionKind.Ranked ? 2 : 1 };

        internal static string NewRoomCode()
        {
            var code = new char[CodeLength];
            for (int i = 0; i < code.Length; i++) code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            return new string(code);
        }

        internal static bool TryPrivateRoom(string input, out string name)
        {
            name = null;
            string code = (input ?? "").Trim().ToUpperInvariant();
            if (code.Length != CodeLength) return false;
            foreach (char c in code) if (Alphabet.IndexOf(c) < 0) return false;
            name = PrivatePrefix + code;
            return true;
        }

        internal static string CustomRoom(string input) => string.IsNullOrWhiteSpace(input) || input.Trim().Length > 48
            ? null : CustomPrefix + input.Trim();

        internal static string DisplayName(OnlineSessionKind kind, string room) =>
            kind == OnlineSessionKind.PrivateMatch && room.StartsWith(PrivatePrefix, System.StringComparison.Ordinal)
                ? room.Substring(PrivatePrefix.Length)
            : kind == OnlineSessionKind.CustomLobby && room.StartsWith(CustomPrefix, System.StringComparison.Ordinal)
                ? room.Substring(CustomPrefix.Length) : room;
    }
}
