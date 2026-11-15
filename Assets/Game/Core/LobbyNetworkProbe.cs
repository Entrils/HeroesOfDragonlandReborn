using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
#endif
namespace HeroesOfDragonland.Multiplayer
{
    // Opt-in real host/client acceptance; inert in ordinary play and release builds.
    public sealed class LobbyNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, readied, started, killed, sawFinished, sawReturn, done;
        private float playingAt, deadline;
        private void Awake()
        {
            active = Environment.GetCommandLineArgs().Contains("-hodrLobbyCheck");
            deadline = Time.realtimeSinceStartup + 150;
        }
        private void Require(bool ok, string label)
        {
            if (!ok) { active = false; throw new InvalidOperationException("[LobbyNetwork] FAIL " + label); }
            Debug.Log("[LobbyNetwork] PASS " + label);
        }
        private void Update()
        {
            if (!active || done) return;
            if (Time.realtimeSinceStartup > deadline) { Require(false, "timeout"); return; }
            var b = GetComponent<MultiplayerBootstrap>();
            var m = GetComponent<NetworkFortressSpawner>().Match;
            if (!b.Running || m == null || m.Object == null || !m.Object.IsValid) return;
            if (m.State == MatchPhase.Finished) sawFinished = true;
            if (m.State == MatchPhase.ReturningToLobby) sawReturn = true;
            if (m.State == MatchPhase.Lobby && m.RoundNumber == 1)
            {
                if (!sawFinished || !sawReturn) return;
                Require(!m.LocalReady && !m.CanStartLobby, "returned lobby clears ready and prevents rematch");
                Require(m.TeamACount + m.TeamBCount == 2, "connected roster retained");
                done = true;
                Debug.Log("[LobbyNetwork] COMPLETE server=" + b.Runner.IsServer);
                return;
            }
            if (m.State == MatchPhase.Lobby && !readied && m.TeamACount + m.TeamBCount == 2)
            {
                if (b.Runner.IsServer) m.SetShortMatch(true);
                if (!m.AllowShortMatch) return;
                Require(!m.CanStartLobby && !m.TryStartLobby(), "unready start denied");
                Require(m.TeamACount == 1 && m.TeamBCount == 1, "replicated teams and player list");
                readied = true; m.RPC_SetReady(true);
            }
            if (!b.Runner.IsServer && m.CanStartLobby && !started)
            { started = true; Require(!m.TryStartLobby(), "client cannot start ready lobby"); }
            if (b.Runner.IsServer && m.CanStartLobby && !started)
            { started = true; Require(m.TryStartLobby(), "host starts ready lobby"); }
            if (m.State == MatchPhase.Playing)
            {
                if (playingAt == 0) { playingAt = Time.realtimeSinceStartup; Require(m.CanPlay, "replicated Playing state"); }
                if (b.Runner.IsServer && !killed && Time.realtimeSinceStartup - playingAt > 4)
                {
                    killed = true;
                    var king = FindObjectsByType<NetworkKingObjective>(FindObjectsSortMode.None).First(k => k.Runner == b.Runner);
                    king.Stats.ApplyDamage(999999);
                }
            }
        }
#endif
    }
}
