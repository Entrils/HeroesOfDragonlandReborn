using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.Multiplayer
{
    // Opt-in six-peer fixture; never modifies a saved asset or runs in ordinary play.
    public sealed class MultiplayerTeamSpawnProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, host, released, checkedPlayers;
        private float readyAt, movingAt;
        private MultiplayerBootstrap bootstrap;
        private TeamSpawnService service;
        private readonly List<GameObject> blockers = new();
        private string originalRules;
        private NetworkTestPlayer mover;
        private Vector3 originalPosition, entryStart;
        private Quaternion originalRotation;
        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            active = args.Contains("-hodrTeamSpawns");
            if (!active) return;
            Application.SetStackTraceLogType(UnityEngine.LogType.Log, StackTraceLogType.None);
            bootstrap = GetComponent<MultiplayerBootstrap>(); service = GetComponent<TeamSpawnService>();
            host = args.Contains("host");
            if (host) Block(service.GetArea(TeamId.TeamB));
        }
        private void Block(TeamSpawnArea area)
        {
            for (int i = 0; i < area.PointCount; i++)
                if (area.TryGetPoint(i, out var point, out _))
                {
                    var blocker = new GameObject("Spawn acceptance obstruction");
                    blocker.transform.position = point;
                    blocker.AddComponent<BoxCollider>(); blockers.Add(blocker);
                }
            Physics.SyncTransforms();
        }
        private void Unblock()
        {
            foreach (var blocker in blockers) { blocker.SetActive(false); Destroy(blocker); }
            blockers.Clear(); Physics.SyncTransforms();
        }
        private void Check(bool value, string label)
        {
            if (!value) { active = false; throw new InvalidOperationException("[TeamSpawnCheck] FAIL " + label); }
            Debug.Log("[TeamSpawnCheck] PASS " + label);
        }
        private void Update()
        {
            if (!active || bootstrap.Runner == null || !bootstrap.Runner.IsRunning) return;
            var runner = bootstrap.Runner;
            if (runner.ActivePlayers.Count() != 6) return;
            var players = runner.ActivePlayers.Select(p => { runner.TryGetPlayerObject(p, out var obj); return obj; })
                .Where(p => p != null).Select(p => p.GetComponent<NetworkTestPlayer>()).ToArray();
            if (runner.IsServer && !released)
            {
                if (readyAt == 0) readyAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - readyAt < .5f) return;
                Check(players.Length == 3 && players.All(p => p.Team == TeamId.TeamA), "blocked-team-waits-without-wrong-spawn");
                Unblock(); released = true; readyAt = 0;
            }
            if (players.Length != 6) return;
            if (readyAt == 0) readyAt = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - readyAt < (runner.IsServer ? 5 : 1)) return;
            if (!checkedPlayers)
            {
                checkedPlayers = true;
                Check(players.Count(p => p.Team == TeamId.TeamA) == 3 && players.Count(p => p.Team == TeamId.TeamB) == 3, "three-players-per-team");
                foreach (var player in players)
                {
                    Check(service.GetArea(player.Team).IsSpawnPoint(player.transform.position), "initial-own-fortress-point");
                    foreach (var other in players) if (other != player)
                        Check(Vector3.Distance(player.transform.position, other.transform.position) > 1.1f, "no-stacked-players");
                }
                if (!runner.IsServer)
                {
                    Check(!service.TryChooseSpawn(runner, TeamId.TeamA, null, out _, out _), "client-selection-rejected");
                    Debug.Log("[TeamSpawnComplete] server=False"); active = false; return;
                }
                // Three points are physically occupied, three can be reserved in this tick.
                for (int i = 0; i < 3; i++) Check(service.TryChooseSpawn(runner, TeamId.TeamA, null, out _, out _), "choose-free-point");
                Check(!service.TryChooseSpawn(runner, TeamId.TeamA, null, out _, out _), "same-tick-reservations-prevent-stacking");
                var area = service.GetArea(TeamId.TeamB);
                originalRules = JsonUtility.ToJson(area.Rules);
                JsonUtility.FromJsonOverwrite("{\"protectedZone\":true,\"enemyPlayerEntry\":1}", area.Rules);
                var opposingGuard = FindObjectsByType<NetworkFortressGuard>().First(g => g.Team == TeamId.TeamA);
                var neutral = FindObjectsByType<NetworkEnemy>().First(e => e.GetComponent<NetworkFortressGuard>() == null);
                Check(!TeamSpawnService.AllowsNpcSpawn(runner, opposingGuard.gameObject, area.transform.position)
                    && !TeamSpawnService.AllowsNpcSpawn(runner, neutral.gameObject, area.transform.position), "enemy-and-neutral-spawn-blocked");
                mover = players.First(p => p.HasInputAuthority);
                originalPosition = mover.transform.position; originalRotation = mover.transform.rotation;
                entryStart = area.transform.position + Vector3.left * 3.65f;
                mover.GetComponent<NetworkTransform>().Teleport(entryStart, Quaternion.LookRotation(Vector3.right));
                bootstrap.TestInput = Vector2.right; bootstrap.TestDodge = true;
                movingAt = Time.realtimeSinceStartup;
            }
            if (runner.IsServer && Time.realtimeSinceStartup - movingAt > 1.5f)
            {
                Check(mover.transform.position.x <= service.GetArea(TeamId.TeamB).transform.position.x - 3.5f + .001f, "server-walk-and-dodge-entry-blocked");
                Check(mover.GetComponent<NetworkPlayerDodge>().DodgeSequence > 0, "production-dodge-tested");
                bootstrap.TestInput = Vector2.zero; bootstrap.TestDodge = false;
                JsonUtility.FromJsonOverwrite(originalRules, service.GetArea(TeamId.TeamB).Rules);
                mover.GetComponent<NetworkTransform>().Teleport(originalPosition, originalRotation);
                Debug.Log("[TeamSpawnComplete] server=True"); active = false;
            }
        }
#endif
    }
}
