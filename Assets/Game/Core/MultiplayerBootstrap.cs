using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkObject playerPrefab;
        private readonly NetworkTeamRoster roster = new NetworkTeamRoster();
        private readonly Dictionary<PlayerRef, NetworkObject> players = new Dictionary<PlayerRef, NetworkObject>();
        private readonly HashSet<PlayerRef> pendingSpawns = new HashSet<PlayerRef>();
        private float nextSpawnRetry;
        private NetworkRunner runner;
        private bool busy;
        private int nextParticipant;
        public bool Busy => busy;
        public bool Running => runner != null && runner.IsRunning;
        public string Status { get; private set; } = "Not connected";
        public event Action<string> StatusChanged;
        public event Action<NetworkRunner> SessionStarted;
        public NetworkRunner Runner => runner;
        public string SessionId { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal Vector2? TestInput { get; set; }
        internal bool? TestAttack { get; set; }
        internal bool? TestDodge { get; set; }
        internal Vector2? TestFacing { get; set; }
#endif

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string mode = null, room = "dragonland-m1";
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-hodrMode") mode = args[i + 1];
                if (args[i] == "-hodrRoom") room = args[i + 1];
            }
            if (mode == "host") _ = StartSession(GameMode.Host, room);
            else if (mode == "server") _ = StartSession(GameMode.Server, room);
            else if (mode == "client") _ = StartSession(GameMode.Client, room);
        }

        private void SetStatus(string value)
        {
            Status = value;
            Debug.Log("[Multiplayer] " + value, this);
            StatusChanged?.Invoke(value);
        }

        public async Task StartSession(GameMode mode, string sessionName)
        {
            if (busy || runner != null) return;
            if (mode != GameMode.Host && mode != GameMode.Server && mode != GameMode.Client)
            { SetStatus("Only Host, Server and Client modes are supported."); return; }
            if (playerPrefab == null || string.IsNullOrWhiteSpace(sessionName) || sessionName.Length > 64)
            { SetStatus("Assign a player prefab and a room name of 1-64 characters."); return; }
            busy = true;
            try
            {
                Application.runInBackground = true;
                roster.Clear(); players.Clear(); pendingSpawns.Clear(); nextParticipant = 0;
                SessionId = mode == GameMode.Client ? null : Guid.NewGuid().ToString("N");
                var runnerObject = new GameObject("Multiplayer Runner");
                runnerObject.transform.SetParent(transform);
                runner = runnerObject.AddComponent<NetworkRunner>();
                runner.ProvideInput = mode != GameMode.Server;
                runner.AddCallbacks(this);
                SetStatus("Starting " + mode + " / " + sessionName.Trim());
                // Test scene has no scene NetworkObjects. Each process loads the same test arena.
                var started = await runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName.Trim(),
                    PlayerCount = NetworkTeamRoster.PlayerCapacity,
                    EnableClientSessionCreation = false,
                    IsVisible = false,
                    SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>()
                });
                if (!started.Ok)
                {
                    SetStatus("Start failed: " + started.ShutdownReason);
                    await ReleaseRunner();
                }
                else
                {
                    SessionStarted?.Invoke(runner);
                    SetStatus(mode + " connected / " + sessionName.Trim());
                }
            }
            catch (Exception exception)
            {
                SetStatus("Start failed: " + exception.Message);
                await ReleaseRunner();
            }
            finally { busy = false; }
        }

        public async Task Disconnect()
        {
            if (busy) return;
            busy = true;
            try { await ReleaseRunner(); SetStatus("Disconnected"); }
            finally { busy = false; }
        }

        private async Task ReleaseRunner()
        {
            var previous = runner;
            if (previous != null)
            {
                try { await previous.Shutdown(); }
                finally
                {
                    if (previous != null) { previous.RemoveCallbacks(this); Destroy(previous.gameObject); }
                    if (runner == previous) runner = null;
                }
            }
            players.Clear(); roster.Clear(); pendingSpawns.Clear();
        }

        private void Update()
        {
            if (runner == null || !runner.IsServer || pendingSpawns.Count == 0 || Time.unscaledTime < nextSpawnRetry) return;
            nextSpawnRetry = Time.unscaledTime + .25f;
            foreach (var player in new List<PlayerRef>(pendingSpawns)) OnPlayerJoined(runner, player);
        }

        internal bool TryGetTeam(PlayerRef player, out HeroesOfDragonland.Teams.TeamId team)
            => roster.TryGetTeam(player.RawEncoded, out team);

        internal void ResetRoundPlayers()
        {
            if (runner == null || !runner.IsServer) return;
            // A new identity also invalidates old reward IDs and delayed avatar RPCs.
            SessionId = Guid.NewGuid().ToString("N");
            var previous = new List<NetworkObject>(players.Values);
            players.Clear(); pendingSpawns.Clear();
            foreach (var avatar in previous)
                if (avatar != null && avatar.IsValid)
                {
                    // Unity destroys the old object at frame end; release its spawn
                    // obstruction now, before choosing poses for the new avatars.
                    foreach (var collider in avatar.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    runner.Despawn(avatar);
                }
            GetComponent<TeamSpawnService>()?.ResetReservations();
            foreach (var player in runner.ActivePlayers) OnPlayerJoined(runner, player);
        }

        public void OnPlayerJoined(NetworkRunner source, PlayerRef player)
        {
            if (!source.IsServer || players.ContainsKey(player)) return;
            if (!roster.TryAssign(player.RawEncoded, out var team))
            { source.Disconnect(player); SetStatus("Rejected join: six-player roster full."); return; }
            NetworkObject avatar = null;
            try
            {
                int lane = 0;
                for (; lane < NetworkTeamRoster.TeamCapacity; lane++)
                {
                    bool occupied = false;
                    foreach (var existing in players.Values)
                    {
                        var actor = existing.GetComponent<NetworkTestPlayer>();
                        if (actor.Team == team && actor.SpawnLane == lane) { occupied = true; break; }
                    }
                    if (!occupied) break;
                }
                var position = new Vector3(team == HeroesOfDragonland.Teams.TeamId.TeamA ? -8 : 8, 1, (lane - 1) * 4);
                var rotation = Quaternion.identity;
                var spawnService = GetComponent<TeamSpawnService>();
                if (spawnService != null || GetComponent<NetworkFortressSpawner>() != null)
                {
                    if (spawnService == null || !spawnService.TryChooseSpawn(source, team, null, out position, out rotation))
                    {
                        if (pendingSpawns.Add(player)) SetStatus("Waiting for a free " + team + " fortress spawn point.");
                        return;
                    }
                }
                int number = ++nextParticipant;
                avatar = source.Spawn(playerPrefab, position, rotation, player,
                    (r, obj) => obj.GetComponent<NetworkTestPlayer>().Initialize(team, number, lane));
                if (avatar == null) throw new InvalidOperationException("Player spawn returned null.");
                players.Add(player, avatar);
                source.SetPlayerObject(player, avatar);
                pendingSpawns.Remove(player);
                SetStatus("Joined P" + number + " owner=" + player + " team=" + team
                    + " count=" + roster.Count + "/6");
            }
            catch (Exception exception)
            {
                if (avatar != null) source.Despawn(avatar);
                players.Remove(player); roster.Remove(player.RawEncoded); pendingSpawns.Remove(player);
                source.Disconnect(player);
                SetStatus("Spawn failed: " + exception.Message);
            }
        }

        public void OnPlayerLeft(NetworkRunner source, PlayerRef player)
        {
            if (!source.IsServer) return;
            pendingSpawns.Remove(player);
            if (players.TryGetValue(player, out var avatar))
            {
                players.Remove(player);
                if (avatar != null && avatar.IsValid) source.Despawn(avatar);
            }
            roster.Remove(player.RawEncoded);
            SetStatus("Left owner=" + player + " count=" + roster.Count + "/6");
        }

        public void OnInput(NetworkRunner source, NetworkInput input)
        {
            if (GetComponent<HeroesOfDragonland.Player.LocalGameplayInputGate>() is { IsBlocked: true })
            {
                input.Set(new NetworkPlayerInput());
                return;
            }
            Vector2 move = Vector2.zero;
            bool attack = false;
            bool dodge = false;
            Vector2 facing = Vector2.zero;
            if (source.ProvideInput && Application.isFocused && Keyboard.current != null)
            {
                var keys = Keyboard.current;
                move.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                move.y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
                attack = keys.spaceKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed);
                dodge = keys.leftShiftKey.isPressed;
            }
            if (source.TryGetPlayerObject(source.LocalPlayer, out var local) && local != null)
            {
                var targeting = local.GetComponent<NetworkLocalTargeting>();
                if (targeting != null) facing = targeting.FacingIntent;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestInput.HasValue) move = TestInput.Value;
            if (TestAttack.HasValue) attack = TestAttack.Value;
            if (TestDodge.HasValue) dodge = TestDodge.Value;
            if (TestFacing.HasValue) facing = TestFacing.Value;
#endif
            input.Set(new NetworkPlayerInput { Move = Vector2.ClampMagnitude(move, 1), Attack = attack, Dodge = dodge, Facing = facing });
        }

        public void OnInputMissing(NetworkRunner source, PlayerRef player, NetworkInput input)
            => input.Set(new NetworkPlayerInput());

        public void OnConnectRequest(NetworkRunner source, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (!source.IsServer || roster.Count >= NetworkTeamRoster.PlayerCapacity) request.Refuse();
            else request.Accept(); // Final admission and team capacity rechecked at spawn.
        }

        public void OnShutdown(NetworkRunner source, ShutdownReason reason)
        {
            if (source != runner) return;
            players.Clear(); roster.Clear(); pendingSpawns.Clear();
            runner = null;
            source.RemoveCallbacks(this);
            Destroy(source.gameObject);
            SetStatus("Stopped: " + reason);
        }
        public void OnConnectedToServer(NetworkRunner source) => SetStatus("Connected to server");
        public void OnDisconnectedFromServer(NetworkRunner source, NetDisconnectReason reason)
            => SetStatus("Connection lost: " + reason);
        public void OnConnectFailed(NetworkRunner source, NetAddress address, NetConnectFailedReason reason)
            => SetStatus("Connection failed: " + reason);
        public void OnHostMigration(NetworkRunner source, HostMigrationToken token)
        {
            SetStatus("Host left; host migration is not supported in this milestone.");
            _ = Disconnect();
        }
        public void OnSceneLoadDone(NetworkRunner source) { }
        public void OnSceneLoadStart(NetworkRunner source) { }
        public void OnSessionListUpdated(NetworkRunner source, List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner source, Dictionary<string, object> data) { }
        public void OnUserSimulationMessage(NetworkRunner source, SimulationMessagePtr message) { }
        public void OnObjectExitAOI(NetworkRunner source, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner source, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner source, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner source, PlayerRef player, ReliableKey key, float progress) { }
    }
}
