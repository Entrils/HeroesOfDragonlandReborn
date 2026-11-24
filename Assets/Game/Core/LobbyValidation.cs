#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public static class LobbyValidation
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("[LobbyValidation] FAIL: " + message);
            checks++; Debug.Log("[LobbyValidation] PASS: " + message);
        }
        private static async Task Wait(Func<bool> predicate)
        {
            for (int i = 0; i < 300; i++)
            {
                if (!Application.isPlaying) throw new Exception("Play Mode stopped");
                if (predicate()) return;
                await Task.Delay(100);
            }
            throw new TimeoutException("Lobby validation timed out");
        }
        public static async void Run()
        {
            checks = 0;
            var b = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            try
            {
                var bots = b.GetComponent<DeveloperBotService>();
                bots.SetBotCount(5);
                await b.StartSession(GameMode.Single, "m50-validation");
                var f = b.GetComponent<NetworkFortressSpawner>();
                await Wait(() => f.Match != null && f.Match.TeamACount + f.Match.TeamBCount == 6);
                var m = f.Match;
                Check(m.State == MatchPhase.Lobby, "six participants do not auto-start");
                Check(m.TeamACount == 3 && m.TeamBCount == 3, "balanced 3v3 roster");
                Check(!m.CanStartLobby && !m.TryStartLobby(), "unready host cannot start");
                Check(!b.ReserveBot(-999999, out _), "seventh participant denied");
                m.RPC_SetReady(true);
                await Wait(() => m.LocalReady);
                await Wait(() => m.CanStartLobby);
                Check(m.TryStartLobby(), "ready host can start");
                await Wait(() => m.State == MatchPhase.Playing);
                Check(m.CanPlay, "normal countdown reaches Playing");
                m.RPC_SetReady(false);
                await Task.Delay(300);
                Check(m.LocalReady, "readiness mutations denied during match");
                // Exercise existing return/cleanup path without waiting for a full King assault.
                typeof(NetworkMatch).GetMethod("BeginReturn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(m, null);
                await Wait(() => m.State == MatchPhase.Lobby);
                Check(!m.LocalReady && !m.CanStartLobby, "return resets readiness and prevents automatic rematch");
                bots.SetBotCount(1);
                await Wait(() => m.TeamACount + m.TeamBCount == 2);
                m.RPC_SetReady(true);
                await Wait(() => m.LocalReady);
                Check(!m.CanStartLobby, "production default requires six participants");
                Check(m.SetShortMatch(true), "development short-match option accepted");
                await Wait(() => !m.LocalReady);
                Check(!m.CanStartLobby, "changing start requirements clears readiness");
                m.RPC_SetReady(true);
                await Wait(() => m.CanStartLobby);
                Check(m.TryStartLobby(), "two-participant development match starts");
                Debug.Log("[LobbyValidation] PASS ALL " + checks);
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { if (b != null) await b.Disconnect(); }
        }
    }
}
#endif
