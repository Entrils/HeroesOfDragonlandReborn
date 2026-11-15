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
        private bool connectionLost;
        private readonly ReconnectReservations reconnects = new();
        private byte[] connectionToken;
        private bool clientSession, everConnected, intentionalDisconnect, cancelRequested, reconnectLoop;
        private int reconnectEpoch, observedServerTick;
        private float lastServerProgress;
        private TaskCompletionSource<bool> leaveAcknowledged;
        private System.Threading.CancellationTokenSource joinCancellation;
        internal void ConfirmLeave() => leaveAcknowledged?.TrySetResult(true);
        internal void ForfeitReconnect(PlayerRef player) => reconnects.Forget(player);
        public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Offline;
        public string NetworkMessage { get; private set; } = "Create or join a lobby.";
        public string RequestNotice { get; private set; }
        public event Action<NetworkConnectionState> ConnectionStateChanged;
        public int ReservedPlayerCount => reconnects.Count;
        public bool CanReconnect => clientSession && connectionToken != null && everConnected && !Running && !busy && !reconnectLoop;
        private void SetConnectionState(NetworkConnectionState state, string message = null)
        {
            ConnectionState = state;
            NetworkMessage = message ?? NetworkFailureMessages.For(state);
            Debug.Log("[NetworkState] " + state + ": " + NetworkMessage, this);
            if (ConnectionStateChanged != null) foreach (Action<NetworkConnectionState> handler in ConnectionStateChanged.GetInvocationList())
                try { handler(state); } catch (Exception e) { Debug.LogException(e, this); }
        }
        private int nextParticipant;
        private uint lightAttackPress, heavyAttackPress, parryPress;
        private bool lightAttackHeld, heavyAttackHeld, parryHeld, suppressAttacks;
        private int lastAttackCaptureFrame = -1;
        public bool Busy => busy || reconnectLoop;
        public bool Running => runner != null && runner.IsRunning;
        public string Status { get; private set; } = "Not connected";
        public event Action<string> StatusChanged;
        public event Action<NetworkRunner> SessionStarted;
        public NetworkRunner Runner => runner;
        public string SessionId { get; private set; }
        public string RoomName { get; private set; }
        internal NetworkObject PlayerPrefab => playerPrefab;
        internal int NextParticipantNumber() => ++nextParticipant;
        internal bool ReserveBot(int key, out HeroesOfDragonland.Teams.TeamId team) => roster.TryAssign(key, out team);
        internal void ReleaseBot(int key) => roster.Remove(key);
        public IEnumerable<NetworkObject> Avatars
        {
            get
            {
                foreach (var actor in players.Values) if (actor != null && actor.IsValid) yield return actor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var bots = GetComponent<DeveloperBotService>();
                if (bots != null) foreach (var actor in bots.Avatars) yield return actor;
#endif
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal Vector2? TestInput { get; set; }
        internal bool? TestAttack { get; set; }
        internal bool? TestDodge { get; set; }
        internal bool? TestHeavyAttack { get; set; }
        internal bool? TestBlock { get; set; }
        internal bool? TestParry { get; set; }
        internal Vector2? TestFacing { get; set; }
        internal bool? TestLockedOn {get;set;}
#endif

        private void OnDestroy()
        {
            intentionalDisconnect = true; reconnectEpoch++;
            joinCancellation?.Cancel(); joinCancellation?.Dispose();
        }

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

        private void CaptureAttackPresses()
        {
            if (lastAttackCaptureFrame == Time.frameCount) return;
            lastAttackCaptureFrame = Time.frameCount;
            var keys = Keyboard.current; var mouse = Mouse.current;
            bool light = (keys != null && keys.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);
            bool heavy = keys != null && keys.rKey.isPressed;
            bool lightTap = (keys != null && keys.spaceKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            bool heavyTap = keys != null && keys.rKey.wasPressedThisFrame;
            bool parry = keys != null && keys.cKey.isPressed;
            bool parryTap = keys != null && keys.cKey.wasPressedThisFrame;
            bool testing = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            testing = TestAttack.HasValue || TestHeavyAttack.HasValue || TestParry.HasValue;
            if (TestAttack.HasValue) { light = TestAttack.Value; lightTap = false; }
            if (TestHeavyAttack.HasValue) { heavy = TestHeavyAttack.Value; heavyTap = false; }
            if (TestParry.HasValue) { parry = TestParry.Value; parryTap = false; }
#endif
            suppressAttacks = !Running || (!Application.isFocused && !testing)
                || GetComponent<HeroesOfDragonland.Player.LocalGameplayInputGate>() is { IsBlocked: true };
            if (!suppressAttacks)
            {
                if (lightTap || (light && !lightAttackHeld)) lightAttackPress = unchecked(lightAttackPress + 1);
                if (heavyTap || (heavy && !heavyAttackHeld)) heavyAttackPress = unchecked(heavyAttackPress + 1);
                if (parryTap || (parry && !parryHeld)) parryPress = unchecked(parryPress + 1);
            }
            lightAttackHeld = light; heavyAttackHeld = heavy; parryHeld = parry;
        }

        private void SetStatus(string value)
        {
            Status = value;
            Debug.Log("[Multiplayer] " + value, this);
            if (StatusChanged != null) foreach (Action<string> handler in StatusChanged.GetInvocationList())
                try { handler(value); } catch (Exception e) { Debug.LogException(e, this); }
        }

        public Task StartSession(GameMode mode, string sessionName) => StartSessionCore(mode, sessionName, false);
        private async Task StartSessionCore(GameMode mode, string sessionName, bool resuming)
        {
            if (busy || runner != null || (reconnectLoop && !resuming))
            { RequestNotice = "A connection request is already active. Leave or cancel before joining again."; return; }
            RequestNotice = null;
            bool supported = mode == GameMode.Host || mode == GameMode.Server || mode == GameMode.Client;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            supported |= mode == GameMode.Single;
#endif
            if (!supported)
            { SetConnectionState(NetworkConnectionState.FailedJoin, "Unsupported session mode."); return; }
            if (playerPrefab == null || string.IsNullOrWhiteSpace(sessionName) || sessionName.Length > 64)
            { SetConnectionState(NetworkConnectionState.FailedJoin, "Enter a lobby name of 1-64 characters; a player prefab must be configured."); return; }
            busy = true;
            joinCancellation?.Dispose();
            joinCancellation = new System.Threading.CancellationTokenSource();
            cancelRequested = false;
            intentionalDisconnect = false;
            clientSession = mode == GameMode.Client;
            if (!resuming)
            {
                everConnected = false;
                connectionToken = clientSession ? new byte[32] : null;
                if (connectionToken != null) using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(connectionToken);
            }
            SetConnectionState(resuming ? NetworkConnectionState.Reconnecting : NetworkConnectionState.Connecting,
                resuming ? "Reconnecting to the same match..." : "Connecting to " + sessionName.Trim() + "...");
            connectionLost = false;
            try
            {
                Application.runInBackground = true;
                roster.Clear(); players.Clear(); pendingSpawns.Clear(); nextParticipant = 0;
                lightAttackPress = heavyAttackPress = parryPress = 0; lastAttackCaptureFrame = -1;
                RoomName = sessionName.Trim();
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
                    ConnectionToken = connectionToken,
                    StartGameCancellationToken = joinCancellation.Token,
                    IsVisible = false,
                    SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>()
                });
                if (!started.Ok)
                {
                    await ReleaseRunner();
                    SetConnectionState(NetworkFailureMessages.Classify(started.ShutdownReason));
                    SetStatus("Start failed: " + started.ShutdownReason);
                }
                else
                {
                    if (runner == null || !runner.IsRunning) throw new InvalidOperationException("Connection ended during startup.");
                    everConnected = true;
                    lastServerProgress = Time.realtimeSinceStartup;
                    observedServerTick = runner.LatestServerTick;
                    if (SessionStarted != null) foreach (Action<NetworkRunner> handler in SessionStarted.GetInvocationList())
                        try { handler(runner); } catch (Exception e) { Debug.LogException(e, this); throw; }
                    SetConnectionState(NetworkConnectionState.Connected, resuming ? "Reconnected to the match." : "Connected.");
                    SetStatus(mode + " connected / " + sessionName.Trim());
                }
            }
            catch (Exception exception)
            {
                await ReleaseRunner();
                SetConnectionState(NetworkConnectionState.FailedJoin);
                SetStatus("Start failed: " + exception.Message);
            }
            finally
            {
                if (cancelRequested)
                {
                    intentionalDisconnect = true;
                    await ReleaseRunner();
                    everConnected = false; connectionToken = null;
                    SetConnectionState(NetworkConnectionState.Offline, "Connection cancelled.");
                }
                busy = false;
            }
        }

        public async Task Disconnect()
        {
            intentionalDisconnect = true; connectionLost = false;
            reconnectEpoch++;
            everConnected = false; connectionToken = null;
            if (busy) { cancelRequested = true; joinCancellation?.Cancel(); return; }
            busy = true;
            SetConnectionState(NetworkConnectionState.Disconnecting, "Leaving session...");
            try
            {
                var match = GetComponent<NetworkFortressSpawner>()?.Match;
                if (Running && clientSession && match != null && match.Object != null && match.Object.IsValid)
                {
                    leaveAcknowledged = new TaskCompletionSource<bool>();
                    match.RPC_LeaveSession();
                    await Task.WhenAny(leaveAcknowledged.Task, Task.Delay(2000));
                    leaveAcknowledged = null;
                }
                await ReleaseRunner(); SetStatus("Disconnected"); SetConnectionState(NetworkConnectionState.Offline, "You left the session.");
            }
            finally { busy = false; }
        }

        private async Task ReleaseRunner()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetComponent<DeveloperBotService>()?.ReleaseSession();
#endif
            var previous = runner;
            if (previous != null && previous.IsServer) reconnects.Expire(previous, roster, true);
            reconnects.Clear();
            runner = null;
            if (previous != null)
            {
                previous.RemoveCallbacks(this);
                try { await previous.Shutdown(); }
                finally
                {
                    if (previous != null) { previous.RemoveCallbacks(this); Destroy(previous.gameObject); }
                    if (runner == previous) runner = null;
                }
            }
            players.Clear(); roster.Clear(); pendingSpawns.Clear();
        }

        public Task Reconnect() => CanReconnect ? RecoverConnection() : Task.CompletedTask;
        private async Task RecoverConnection()
        {
            if (reconnectLoop || intentionalDisconnect) return;
            reconnectLoop = true;
            int epoch = reconnectEpoch;
            try
            {
                busy = true;
                await ReleaseRunner();
                busy = false;
                for (int attempt = 0; attempt < 3 && epoch == reconnectEpoch && !intentionalDisconnect; attempt++)
                {
                    SetConnectionState(NetworkConnectionState.Reconnecting, "Reconnecting (" + (attempt + 1) + "/3). Cancel to leave.");
                    await Task.Delay((1 << attempt) * 2000);
                    if (this == null || !Application.isPlaying || epoch != reconnectEpoch || intentionalDisconnect) return;
                    await StartSessionCore(GameMode.Client, RoomName, true);
                    if (Running && ConnectionState == NetworkConnectionState.Connected) return;
                }
                if (epoch == reconnectEpoch && !intentionalDisconnect) SetConnectionState(NetworkConnectionState.ReconnectExpired);
            }
            catch (Exception e) { Debug.LogException(e, this); SetConnectionState(NetworkConnectionState.ConnectionLost); }
            finally
            {
                connectionLost = false;
                busy = false; reconnectLoop = false;
                if (intentionalDisconnect && !Running) SetConnectionState(NetworkConnectionState.Offline, "Connection cancelled.");
            }
        }

        private void Update()
        {
            // Shutdown outside Fusion callbacks, after an in-flight start/disconnect finishes.
            if (connectionLost && !busy)
            {
                connectionLost = false;
                if (clientSession && everConnected && !intentionalDisconnect) _ = RecoverConnection();
            }
            if (Running && clientSession && !busy && !connectionLost && !reconnectLoop)
            {
                int tick = runner.LatestServerTick;
                if (tick != observedServerTick)
                {
                    observedServerTick = tick; lastServerProgress = Time.realtimeSinceStartup;
                    if (ConnectionState == NetworkConnectionState.Interrupted) SetConnectionState(NetworkConnectionState.Connected, "Connection restored.");
                }
                else if (Time.realtimeSinceStartup - lastServerProgress > 3 && ConnectionState == NetworkConnectionState.Connected)
                    SetConnectionState(NetworkConnectionState.Interrupted);
            }
            GetComponent<HeroesOfDragonland.Player.LocalGameplayInputGate>()?.SetBlocked(this,
                ConnectionState == NetworkConnectionState.Interrupted || ConnectionState == NetworkConnectionState.Reconnecting);
            if (Running && runner.IsServer) reconnects.Expire(runner, roster);
            CaptureAttackPresses();
            if (runner == null || !runner.IsServer || pendingSpawns.Count == 0 || Time.unscaledTime < nextSpawnRetry) return;
            nextSpawnRetry = Time.unscaledTime + .25f;
            foreach (var player in new List<PlayerRef>(pendingSpawns)) OnPlayerJoined(runner, player);
        }

        internal bool TryGetTeam(PlayerRef player, out HeroesOfDragonland.Teams.TeamId team)
            => roster.TryGetTeam(player.RawEncoded, out team);

        internal void ResetRoundPlayers()
        {
            if (runner == null || !runner.IsServer) return;
            reconnects.Expire(runner, roster, true);
            // A new identity also invalidates old reward IDs and delayed avatar RPCs.
            SessionId = Guid.NewGuid().ToString("N");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetComponent<DeveloperBotService>()?.ClearAvatars();
#endif
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
            foreach (var player in runner.ActivePlayers) pendingSpawns.Add(player);
            foreach (var player in runner.ActivePlayers) OnPlayerJoined(runner, player);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetComponent<DeveloperBotService>()?.SpawnPending();
#endif
        }

        public void OnPlayerJoined(NetworkRunner source, PlayerRef player)
        {
            if (source != runner || !source.IsServer || players.ContainsKey(player)) return;
            bool active = false;
            foreach (var connected in source.ActivePlayers) if (connected == player) { active = true; break; }
            if (!active) { pendingSpawns.Remove(player); return; }
            byte[] token = source.GetPlayerConnectionToken(player);
            if (reconnects.IsDuplicate(token, player)) { source.Disconnect(player); return; }
            if (reconnects.TryResume(token, player, out var previousOwner, out var resumed))
            {
                if (!roster.Transfer(previousOwner.RawEncoded, player.RawEncoded))
                { reconnects.Forget(player); roster.Remove(previousOwner.RawEncoded); source.Despawn(resumed); source.Disconnect(player); return; }
                resumed.AssignInputAuthority(player);
                players[player] = resumed;
                source.SetPlayerObject(player, resumed);
                resumed.GetComponent<NetworkInventory>()?.ReplicateTo(player, true);
                resumed.GetComponent<NetworkPlayerResources>()?.ReplicateTo(player, true);
                resumed.GetComponent<NetworkPlayerCombat>()?.ResetConnectionInput();
                resumed.GetComponent<NetworkTestPlayer>().SetSuspended(false);
                pendingSpawns.Remove(player);
                Debug.Log("[Resilience] Resumed reserved avatar " + resumed.Id + " owner=" + player);
                return;
            }
            if (!pendingSpawns.Contains(player) && GetComponent<NetworkFortressSpawner>()?.Match is { } activeMatch
                && activeMatch.State != MatchPhase.Lobby && activeMatch.State != MatchPhase.Boot)
            { source.Disconnect(player); return; }
            if (!roster.TryAssign(player.RawEncoded, out var team))
            { source.Disconnect(player); SetStatus("Rejected join: six-player roster full."); return; }
            reconnects.Track(player, token);
            NetworkObject avatar = null;
            try
            {
                int lane = 0;
                for (; lane < NetworkTeamRoster.TeamCapacity; lane++)
                {
                    bool occupied = false;
                    foreach (var existing in Avatars)
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
                reconnects.Track(player, token);
                source.SetPlayerObject(player, avatar);
                pendingSpawns.Remove(player);
                SetStatus("Joined P" + number + " owner=" + player + " team=" + team
                    + " count=" + roster.Count + "/6");
            }
            catch (Exception exception)
            {
                if (avatar != null) source.Despawn(avatar);
                players.Remove(player); roster.Remove(player.RawEncoded); pendingSpawns.Remove(player); reconnects.Forget(player);
                source.Disconnect(player);
                SetStatus("Spawn failed: " + exception.Message);
            }
        }

        public void OnPlayerLeft(NetworkRunner source, PlayerRef player)
        {
            if (source != runner || !source.IsServer) return;
            if (!players.ContainsKey(player) && !pendingSpawns.Contains(player)) return;
            pendingSpawns.Remove(player);
            if (players.TryGetValue(player, out var avatar))
            {
                players.Remove(player);
                if (avatar != null && avatar.IsValid)
                {
                    var match = GetComponent<NetworkFortressSpawner>()?.Match;
                    if (match != null && match.State == MatchPhase.Playing && reconnects.Reserve(player, avatar))
                    {
                        // Deny admission first, then stop actions and remove the old owner's private replication.
                        avatar.GetComponent<NetworkPlayerCombat>()?.SuspendConnection();
                        avatar.GetComponent<NetworkPlayerDodge>()?.SuspendConnection();
                        avatar.GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>()?.Clear();
                        avatar.GetComponent<NetworkInventory>()?.ReplicateTo(player, false);
                        avatar.GetComponent<NetworkPlayerResources>()?.ReplicateTo(player, false);
                        avatar.RemoveInputAuthority();
                        avatar.GetComponent<NetworkTestPlayer>().SetSuspended(true);
                        Debug.Log("[Resilience] Reserved disconnected avatar " + avatar.Id + " for 90 seconds");
                        return;
                    }
                    foreach (var collider in avatar.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    source.Despawn(avatar);
                }
            }
            reconnects.Forget(player);
            roster.Remove(player.RawEncoded);
            SetStatus("Left owner=" + player + " count=" + roster.Count + "/6");
        }

        public void OnInput(NetworkRunner source, NetworkInput input)
        {
            CaptureAttackPresses();
            if (GetComponent<HeroesOfDragonland.Player.LocalGameplayInputGate>() is { IsBlocked: true })
            {
                input.Set(new NetworkPlayerInput { LightAttackPress = lightAttackPress, HeavyAttackPress = heavyAttackPress, ParryPress = parryPress, SuppressAttacks = true });
                return;
            }
            Vector2 move = Vector2.zero;
            bool attack = false;
            bool dodge = false;
            bool heavy = false, block = false;
            Vector2 facing = Vector2.zero;
            bool lockedOn=false;
            if (source.ProvideInput && Application.isFocused && Keyboard.current != null)
            {
                var keys = Keyboard.current;
                move.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                move.y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
                attack = keys.spaceKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed);
                dodge = keys.leftShiftKey.isPressed;
                heavy = keys.rKey.isPressed;
                block = Mouse.current != null && Mouse.current.rightButton.isPressed;
            }
            if (source.TryGetPlayerObject(source.LocalPlayer, out var local) && local != null)
            {
                var camera = Camera.main != null ? Camera.main.GetComponent<HeroesOfDragonland.World.BattlefieldCamera>() : null;
                if (camera != null && camera.HasLocalTarget) move = camera.ToWorldMovement(move);
                var targeting = local.GetComponent<NetworkLocalTargeting>();
                if (targeting != null) facing = targeting.FacingIntent;
                if(targeting!=null)lockedOn=targeting.IsLockedOn;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestInput.HasValue) move = TestInput.Value;
            if (TestAttack.HasValue) attack = TestAttack.Value;
            if (TestDodge.HasValue) dodge = TestDodge.Value;
            if (TestHeavyAttack.HasValue) heavy = TestHeavyAttack.Value;
            if (TestBlock.HasValue) block = TestBlock.Value;
            if (TestFacing.HasValue) facing = TestFacing.Value;
            if(TestLockedOn.HasValue)lockedOn=TestLockedOn.Value;
#endif
            input.Set(new NetworkPlayerInput { Move = Vector2.ClampMagnitude(move, 1), Attack = attack, HeavyAttack = heavy,
                LightAttackPress = lightAttackPress, HeavyAttackPress = heavyAttackPress, ParryPress = parryPress, SuppressAttacks = suppressAttacks,
                Block = block, Dodge = dodge, Facing = facing, LockedOn=lockedOn });
        }

        public void OnInputMissing(NetworkRunner source, PlayerRef player, NetworkInput input)
            => input.Set(new NetworkPlayerInput());

        public void OnConnectRequest(NetworkRunner source, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (source != runner || !source.IsServer || reconnects.IsDuplicate(token)) { request.Refuse(); return; }
            var match = GetComponent<NetworkFortressSpawner>()?.Match;
            if (match != null && match.State == MatchPhase.Playing && reconnects.CanResume(token)) { request.Accept(); return; }
            if (roster.Count >= NetworkTeamRoster.PlayerCapacity || (match != null && match.State != MatchPhase.Lobby)) request.Refuse();
            else request.Accept(); // Rechecked in OnPlayerJoined; reconnect credentials never override a live owner.
        }

        public void OnShutdown(NetworkRunner source, ShutdownReason reason)
        {
            if (source != runner) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetComponent<DeveloperBotService>()?.ReleaseSession();
#endif
            players.Clear(); roster.Clear(); pendingSpawns.Clear(); reconnects.Clear();
            if (!intentionalDisconnect && clientSession && everConnected)
            { connectionLost = !reconnectLoop; SetConnectionState(NetworkConnectionState.ConnectionLost); }
            else if (!busy) SetConnectionState(NetworkFailureMessages.Classify(reason));
            runner = null;
            source.RemoveCallbacks(this);
            Destroy(source.gameObject);
            SetStatus("Stopped: " + reason);
        }
        public void OnConnectedToServer(NetworkRunner source) { if (source == runner) SetStatus("Connected to server"); }
        public void OnDisconnectedFromServer(NetworkRunner source, NetDisconnectReason reason)
        {
            if (source != runner) return;
            SetStatus("Connection lost: " + reason);
            if (!intentionalDisconnect) { SetConnectionState(NetworkConnectionState.ConnectionLost); connectionLost = !reconnectLoop; }
        }
        public void OnConnectFailed(NetworkRunner source, NetAddress address, NetConnectFailedReason reason)
        {
            if (source != runner) return;
            SetConnectionState(reason == NetConnectFailedReason.ServerFull ? NetworkConnectionState.SessionFull : NetworkConnectionState.JoinRejected);
            SetStatus("Connection failed: " + reason);
        }
        public void OnHostMigration(NetworkRunner source, HostMigrationToken token)
        {
            if (source != runner) return;
            SetStatus("Host left; this match cannot migrate to another server.");
            SetConnectionState(NetworkConnectionState.ConnectionLost);
            connectionLost = !reconnectLoop;
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
