#if UNITY_EDITOR || DEBUG
using System;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Drives the public ready/start flow for explicitly invoked build smoke fixtures.
    public sealed class PlaytestLobbyAutomation : MonoBehaviour
    {
        private NetworkFortressSpawner spawner;
        private int expectedPlayers;
        private float nextAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-hodrPlaytestReady");
            if (option < 0 || option + 1 >= args.Length || !int.TryParse(args[option + 1], out int count)
                || count < 2 || count > 6) return;
            var bootstrap = FindAnyObjectByType<MultiplayerBootstrap>();
            if (bootstrap == null || !bootstrap.TryGetComponent<NetworkFortressSpawner>(out var owner)) return;
            var automation = bootstrap.gameObject.AddComponent<PlaytestLobbyAutomation>();
            automation.spawner = owner;
            automation.expectedPlayers = count;
            if (Application.isBatchMode) Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + .25f;
            var match = spawner.Match;
            if (match == null || match.Object == null || !match.Object.IsValid || match.State != MatchPhase.Lobby
                || match.TeamACount == 0 || match.TeamBCount == 0 || match.TeamACount + match.TeamBCount != expectedPlayers) return;
            if (expectedPlayers < 6 && !match.AllowShortMatch)
            {
                if (match.IsServerOwner) match.SetShortMatch(true);
                return;
            }
            if (!match.LocalReady) match.RPC_SetReady(true);
            if (match.IsServerOwner && match.CanStartLobby) match.TryStartLobby();
        }
    }
}
#endif
