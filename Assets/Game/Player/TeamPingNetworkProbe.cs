#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Opt-in three-peer fixture. All requests use the normal owning-player RPC.
    public sealed class TeamPingNetworkProbe : MonoBehaviour
    {
        private readonly HashSet<int> hostPings = new();
        private MultiplayerBootstrap bootstrap;
        private float began = -1;
        private int step;
        private bool allySent, allyPingSeen, complete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hodrPingCheck") < 0) return;
            var owner = FindAnyObjectByType<MultiplayerBootstrap>();
            if (owner != null) owner.gameObject.AddComponent<TeamPingNetworkProbe>().bootstrap = owner;
        }

        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            if (complete || runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)
                || !runner.TryGetPlayerObject(runner.LocalPlayer, out var actor)) return;
            var match = NetworkMatchPacing.MatchFor(runner);
            NetworkTestPlayer host = null;
            int firstConnection = int.MaxValue;
            int players = 0;
            foreach (var connection in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(connection, out var member)) continue;
                players++;
                if (connection.RawEncoded >= firstConnection) continue;
                firstConnection = connection.RawEncoded;
                host = member.GetComponent<NetworkTestPlayer>();
            }
            if (players != 3 || host == null) return;
            var local = actor.GetComponent<NetworkTestPlayer>();
            bool allied = local.Team == host.Team;
            if (began < 0) began = Time.unscaledTime;
            float age = Time.unscaledTime - began;
            var pings = local.ReadTeamPings(match.RoundNumber);
            foreach (var ping in pings)
            {
                Check(allied, "an opposing connection received a ping");
                if (ping.Source == host.ParticipantNumber)
                {
                    Check(ping.Kind == TeamPingKind.Danger, "invalid, over-cap or cooldown ping was relayed");
                    hostPings.Add(ping.Expires);
                }
                else
                {
                    Check(ping.Kind == TeamPingKind.Retreat, "unexpected ally ping kind");
                    allyPingSeen = true;
                }
            }
            if (runner.IsServer)
            {
                if (step == 0 && age >= 1) {
                    Send(local, TeamPingKind.Danger);
                    Send(local, TeamPingKind.Retreat); // Same-tick cooldown.
                    step++;
                }
                if (step == 1 && age >= 2.35f) { Send(local, TeamPingKind.Danger); step++; }
                if (step == 2 && age >= 3.7f) { Send(local, TeamPingKind.Danger); step++; }
                if (step == 3 && age >= 5.05f) { Send(local, TeamPingKind.Retreat); step++; } // Active cap.
                if (step == 4 && age >= 7.6f) {
                    local.RequestTeamPing(TeamPingKind.Danger, new Vector3(float.NaN, 0, 0), default);
                    step++;
                }
                if (step == 5 && age >= 9) { Send(local, TeamPingKind.Danger); step++; }
            }
            else if (allied && !allySent && hostPings.Count > 0)
            {
                Send(local, TeamPingKind.Retreat);
                allySent = true;
            }
            if (age < 17) return;
            Check(pings.Count == 0, "expired pings remain visible");
            Check(allied ? hostPings.Count == 4 && allyPingSeen : hostPings.Count == 0, "team delivery, source or cap");
            complete = true;
            Debug.Log("[TeamPing] PASS server=" + runner.IsServer + " allied=" + allied + " hostPings=" + hostPings.Count);
        }

        private static void Send(NetworkTestPlayer player, TeamPingKind kind)
        {
            var scene = player.gameObject.scene.GetPhysicsScene();
            bool grounded = scene.Raycast(player.transform.position, Vector3.down, out var floor, 3,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Check(grounded && player.RequestTeamPing(kind, floor.point, default), "could not request a ground ping");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[TeamPing] FAIL " + message);
        }
    }
}
#endif
