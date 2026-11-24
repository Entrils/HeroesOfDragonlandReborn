using System;
using HeroesOfDragonland.Profiles;
using UnityEngine;

namespace HeroesOfDragonland.Platform
{
    public readonly struct PlatformIdentity
    {
        public string AccountId { get; }
        public string DisplayName { get; }

        public PlatformIdentity(string accountId, string displayName)
        { AccountId = accountId; DisplayName = displayName; }
    }

    public enum PlatformActivity { Menu, Lobby, Playing, Results }
    public enum PlatformQueue { None, Unranked, Ranked, Private, Custom }

    public readonly struct PlatformPresence
    {
        public PlatformActivity Activity { get; }
        public PlatformQueue Queue { get; }

        public PlatformPresence(PlatformActivity activity, PlatformQueue queue)
        { Activity = activity; Queue = queue; }
    }

    public interface IGamePlatform
    {
        PlatformIdentity Identity { get; }
        bool SupportsInvites { get; }
        // Adapter dispatches accepted invites on the Unity main thread.
        event Action<string> PrivateRoomJoinRequested;
        bool TryShowInvite(string privateRoomCode);
        bool TryUnlockAchievement(string achievementId);
        void SetPresence(PlatformPresence presence);
        void ClearPresence();
    }

    public static class GamePlatform
    {
        private static readonly IGamePlatform Local = new LocalGamePlatform();
        public static IGamePlatform Current { get; private set; } = Local;
        public static string DisplayName
        {
            get
            {
                string name = Current.Identity.DisplayName;
                return string.IsNullOrWhiteSpace(name) ? LocalPlayerProfile.Current.Data.DisplayName : name;
            }
        }

        public static void Install(IGamePlatform provider) => Current = provider ?? Local;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Install(null);

        private sealed class LocalGamePlatform : IGamePlatform
        {
            public PlatformIdentity Identity => new(null, LocalPlayerProfile.Current.Data.DisplayName);
            public bool SupportsInvites => false;
            public event Action<string> PrivateRoomJoinRequested { add { } remove { } }
            public bool TryShowInvite(string privateRoomCode) => false;
            public bool TryUnlockAchievement(string achievementId) => false;
            public void SetPresence(PlatformPresence presence) { }
            public void ClearPresence() { }
        }
    }
}
