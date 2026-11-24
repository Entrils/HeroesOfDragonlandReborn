using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Profiles;
using UnityEngine;

namespace HeroesOfDragonland.Platform
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    internal sealed class PlatformSessionBridge : MonoBehaviour
    {
        private MultiplayerBootstrap bootstrap;
        private NetworkFortressSpawner fortress;
        private IGamePlatform platform;
        private readonly HashSet<string> acknowledged = new();
        private PlatformActivity? activity;
        private PlatformQueue queue;
        private float nextSync;

        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            fortress = GetComponent<NetworkFortressSpawner>();
        }

        private void Update()
        {
            if (platform != GamePlatform.Current)
            {
                Unbind();
                platform = GamePlatform.Current;
                platform.PrivateRoomJoinRequested += OnJoinRequested;
                nextSync = 0;
            }

            var nextActivity = Activity();
            var nextQueue = nextActivity == PlatformActivity.Menu ? PlatformQueue.None : bootstrap.SessionKind switch
            {
                OnlineSessionKind.QuickPlay => PlatformQueue.Unranked,
                OnlineSessionKind.Ranked => PlatformQueue.Ranked,
                OnlineSessionKind.PrivateMatch => PlatformQueue.Private,
                _ => PlatformQueue.Custom
            };
            if (activity != nextActivity || queue != nextQueue)
            {
                activity = nextActivity;
                queue = nextQueue;
                platform.SetPresence(new PlatformPresence(nextActivity, nextQueue));
            }

            if (Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + 5f;
            foreach (var id in LocalPlayerProfile.Current.AchievementIds)
                if (!acknowledged.Contains(id) && AchievementRules.IsKnown(id) && platform.TryUnlockAchievement(id))
                    acknowledged.Add(id);
        }

        private PlatformActivity Activity()
        {
            if (!bootstrap.Running) return PlatformActivity.Menu;
            var match = fortress != null ? fortress.Match : null;
            if (match == null || match.Object == null || !match.Object.IsValid) return PlatformActivity.Lobby;
            return match.State switch
            {
                MatchPhase.Playing => PlatformActivity.Playing,
                MatchPhase.Finished => PlatformActivity.Results,
                _ => PlatformActivity.Lobby
            };
        }

        private void OnJoinRequested(string code)
        {
            if (bootstrap.Busy || bootstrap.Running || !OnlineMatchmaking.TryPrivateRoom(code, out _)) return;
            _ = bootstrap.JoinPrivateMatch(code);
        }

        private void Unbind()
        {
            if (platform == null) return;
            platform.PrivateRoomJoinRequested -= OnJoinRequested;
            platform.ClearPresence();
            platform = null;
            activity = null;
            acknowledged.Clear();
        }

        private void OnDisable() => Unbind();
    }
}
