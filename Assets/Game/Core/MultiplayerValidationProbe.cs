using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.Linq;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Development build probe exercises the real input/transport path, never writes network state.
    public sealed class MultiplayerValidationProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MultiplayerBootstrap bootstrap;
        private bool active;
        private bool moving;
        private bool combatScenario;
        private float connectedAt = -1;
        private float nextReport;
        private float disconnectAfter;
        private float quitAfter;
        private float started;
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            var args = Environment.GetCommandLineArgs();
            active = args.Contains("-hodrProbe");
            moving = args.Contains("-hodrMove");
            combatScenario = args.Contains("-hodrCombat");
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-hodrDisconnect") float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out disconnectAfter);
                if (args[i] == "-hodrQuit") float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out quitAfter);
            }
            started = Time.realtimeSinceStartup;
        }
        private void Update()
        {
            if (!active || bootstrap == null) return;
            float now = Time.realtimeSinceStartup;
            if (quitAfter > 0 && now - started >= quitAfter) { Application.Quit(); return; }
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning) return;
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local) && local != null)
            {
                if (connectedAt < 0) connectedAt = now;
                // Two seconds of x movement (10 world units), then neutral input.
                if (!combatScenario)
                    bootstrap.TestInput = moving && now - connectedAt < 2f ? Vector2.right : Vector2.zero;
                if (disconnectAfter > 0 && now - connectedAt >= disconnectAfter)
                {
                    disconnectAfter = 0;
                    _ = bootstrap.Disconnect();
                    return;
                }
            }
            if (now < nextReport) return;
            nextReport = now + 2f;
            int count = 0, a = 0, b = 0;
            foreach (var player in runner.ActivePlayers.OrderBy(p => p.RawEncoded))
            {
                if (!runner.TryGetPlayerObject(player, out var obj) || obj == null) continue;
                var actor = obj.GetComponent<NetworkTestPlayer>();
                if (actor == null) continue;
                count++;
                if (actor.Team == Teams.TeamId.TeamA) a++;
                if (actor.Team == Teams.TeamId.TeamB) b++;
                var pos = obj.transform.position;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Probe] local={0} owner={1} id={2} participant={3} team={4} position={5:F3},{6:F3},{7:F3}",
                    runner.LocalPlayer, obj.InputAuthority, obj.Id, actor.ParticipantNumber, actor.Team, pos.x, pos.y, pos.z));
            }
            Debug.Log("[Probe] server=" + runner.IsServer + " players=" + count + " A=" + a + " B=" + b);
        }
#endif
    }
}
