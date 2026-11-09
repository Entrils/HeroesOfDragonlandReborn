using System;
using Fusion;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public struct NetworkLobbySlot : INetworkStruct
    {
        public PlayerRef Player;
        public TeamId Team;
        public NetworkBool HasAvatar;
    }

    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatch : NetworkBehaviour
    {
        [SerializeField] private MatchLifecycleConfig lifecycle;
        [SerializeField] private NetworkKingObjective teamAKingPrefab;
        [SerializeField] private NetworkKingObjective teamBKingPrefab;
        [SerializeField] private NetworkEnemy teamAGuardPrefab;
        [SerializeField] private NetworkEnemy teamBGuardPrefab;
        [SerializeField] private Vector3 teamAKingPosition = new Vector3(-14, 0, 3);
        [SerializeField] private Vector3 teamBKingPosition = new Vector3(14, 0, 3);
        [SerializeField] private Vector3 teamAGuardPosition = new Vector3(-10, 0, 3);
        [SerializeField] private Vector3 teamBGuardPosition = new Vector3(10, 0, 3);
        [Networked] public MatchPhase State { get; private set; }
        [Networked] public TickTimer PhaseTimer { get; private set; }
        [Networked] public int RoundNumber { get; private set; }
        [Networked] public float PlayingSeconds { get; private set; }
        [Networked] public int StateSequence { get; private set; }
        [Networked] public int RequiredPlayers { get; private set; }
        [Networked] public int TeamACount { get; private set; }
        [Networked] public int TeamBCount { get; private set; }
        [Networked] public NetworkString<_128> PreparationError { get; private set; }
        [Networked, Capacity(NetworkTeamRoster.PlayerCapacity)] public NetworkArray<NetworkLobbySlot> LobbyPlayers => default;
        [Networked] public NetworkBool MatchEnded { get; private set; }
        [Networked] public TeamId Winner { get; private set; }
        [Networked] public int FinishCount { get; private set; }
        [Networked] public NetworkId TeamAKingId { get; private set; }
        [Networked] public NetworkId TeamBKingId { get; private set; }
        private NetworkKingObjective kingA, kingB;
        private MultiplayerBootstrap bootstrap;
        private NetworkRoundReset reset;
        private int observedState = -1, observedFinishRound = -1;
        public MatchLifecycleConfig Configuration => lifecycle;
        public float SecondsRemaining => Object != null && Object.IsValid ? PhaseTimer.RemainingTime(Runner) ?? 0 : 0;
        public event Action<MatchPhase> StateChanged;
        public event Action<TeamId> Ended;
        public bool IsServerOwner => Object != null && Object.IsValid && Runner.IsServer
            && HasStateAuthority && Object.InputAuthority == PlayerRef.None;
        // Check deaths as well as the state: even an earlier death listener cannot
        // slip another hit/reward through before the match death callback runs.
        public bool CanPlay => isActiveAndEnabled && State == MatchPhase.Playing && !MatchEnded
            && (!IsServerOwner || (kingA != null && kingB != null && !kingA.Stats.IsDead && !kingB.Stats.IsDead));
        public static bool AllowsGameplay(NetworkRunner runner)
        {
            if (runner == null) return false;
            var fortress = runner.GetComponentInParent<NetworkFortressSpawner>();
            return fortress == null || (fortress.Match != null && fortress.Match.Object != null
                && fortress.Match.Object.IsValid && fortress.Match.CanPlay);
        }
        public override void Spawned()
        {
            bootstrap = Runner.GetComponentInParent<MultiplayerBootstrap>();
            reset = bootstrap.GetComponent<NetworkRoundReset>();
            Runner.GetComponentInParent<NetworkFortressSpawner>()?.Bind(this);
            if (IsServerOwner)
            {
                State = MatchPhase.Boot;
                RequiredPlayers = lifecycle != null ? lifecycle.MinimumPlayers : NetworkTeamRoster.PlayerCapacity;
            }
            NotifyObservers();
        }
        public override void FixedUpdateNetwork()
        {
            if (!IsServerOwner) return;
            RefreshLobby();
            switch (State)
            {
                case MatchPhase.Boot:
                    if (lifecycle == null || reset == null)
                        PreparationError = "Assign MatchLifecycleConfig and NetworkRoundReset before starting.";
                    SetPhase(MatchPhase.Lobby);
                    break;
                case MatchPhase.Lobby:
                    if (PreparationError.ToString().Length == 0 && EnoughPlayers) BeginRound();
                    break;
                case MatchPhase.Starting:
                    if (!EnoughConnections) BeginReturn();
                    else if (EnoughPlayers && PhaseTimer.Expired(Runner)) SetPhase(MatchPhase.Playing);
                    break;
                case MatchPhase.Playing:
                    PlayingSeconds += Runner.DeltaTime;
                    // An empty room is abandoned without inventing a winning team.
                    if (TeamACount + TeamBCount == 0) BeginReturn();
                    break;
                case MatchPhase.Finished:
                    if (PhaseTimer.Expired(Runner)) BeginReturn();
                    break;
                case MatchPhase.ReturningToLobby:
                    if (PhaseTimer.Expired(Runner))
                    {
                        MatchEnded = false; Winner = TeamId.Unassigned;
                        SetPhase(MatchPhase.Lobby);
                    }
                    break;
            }
            NotifyObservers();
        }

        private bool EnoughPlayers => MatchLifecycleRules.HasEnoughPlayers(TeamACount, TeamBCount, RequiredPlayers);
        private bool EnoughConnections
        {
            get
            {
                int a = 0, b = 0;
                for (int i = 0; i < NetworkTeamRoster.PlayerCapacity; i++)
                {
                    if (LobbyPlayers[i].Team == TeamId.TeamA) a++;
                    if (LobbyPlayers[i].Team == TeamId.TeamB) b++;
                }
                return MatchLifecycleRules.HasEnoughPlayers(a, b, RequiredPlayers);
            }
        }
        private void RefreshLobby()
        {
            int index = 0, a = 0, b = 0;
            foreach (var player in Runner.ActivePlayers)
            {
                if (index >= NetworkTeamRoster.PlayerCapacity) break;
                bootstrap.TryGetTeam(player, out var team);
                bool ready = Runner.TryGetPlayerObject(player, out var obj) && obj != null && obj.IsValid
                    && obj.GetComponent<NetworkTestPlayer>() != null;
                LobbyPlayers.Set(index++, new NetworkLobbySlot { Player = player, Team = team, HasAvatar = ready });
                if (ready && team == TeamId.TeamA) a++;
                if (ready && team == TeamId.TeamB) b++;
            }
            while (index < NetworkTeamRoster.PlayerCapacity) LobbyPlayers.Set(index++, default);
            TeamACount = a; TeamBCount = b;
        }

        private void SetPhase(MatchPhase next, float seconds = -1)
        {
            if (!MatchLifecycleRules.CanTransition(State, next))
                throw new InvalidOperationException("Invalid match transition: " + State + " -> " + next);
            State = next; StateSequence++;
            PhaseTimer = seconds < 0 ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, seconds);
            Debug.Log("[MatchLifecycle] round=" + RoundNumber + " state=" + State, this);
        }

        private void BeginRound()
        {
            PlayingSeconds = 0;
            RoundNumber++; FinishCount = 0; MatchEnded = false; Winner = TeamId.Unassigned;
            SetPhase(MatchPhase.Starting, lifecycle.StartingDelay);
            try
            {
                reset.ClearWorld(Runner);
                reset.ResetPlayers(Runner);
                SpawnObjectives();
                reset.SpawnWorld(Runner);
                RefreshLobby();
            }
            catch (Exception exception)
            {
                PreparationError = "Round preparation failed; see server Console. Retry after fixing setup.";
                Debug.LogException(exception, this);
                BeginReturn();
            }
        }

        private void BeginReturn()
        {
            SetPhase(MatchPhase.ReturningToLobby, lifecycle != null ? lifecycle.ReturningDelay : 1);
            try
            {
                reset.ClearWorld(Runner);
                kingA = null; kingB = null; TeamAKingId = default; TeamBKingId = default;
                reset.ResetPlayers(Runner);
                RefreshLobby();
            }
            catch (Exception exception)
            {
                PreparationError = "Round cleanup failed; see server Console. Retry after fixing setup.";
                Debug.LogException(exception, this);
            }
        }

        // Local server control only; clients cannot force transitions or clear errors.
        public bool RetryPreparation()
        {
            if (!IsServerOwner || State != MatchPhase.Lobby || lifecycle == null || reset == null) return false;
            PreparationError = "";
            return true;
        }

        private void SpawnObjectives()
        {
            if (teamAKingPrefab == null || teamBKingPrefab == null || teamAGuardPrefab == null || teamBGuardPrefab == null
                || teamAKingPrefab.Team != TeamId.TeamA || teamBKingPrefab.Team != TeamId.TeamB)
                throw new InvalidOperationException("Assign both fortress teams and their objectives.");
            if (!TeamSpawnService.AllowsNpcSpawn(Runner, teamAKingPrefab.gameObject, teamAKingPosition)
                || !TeamSpawnService.AllowsNpcSpawn(Runner, teamBKingPrefab.gameObject, teamBKingPosition)
                || !TeamSpawnService.AllowsNpcSpawn(Runner, teamAGuardPrefab.gameObject, teamAGuardPosition)
                || !TeamSpawnService.AllowsNpcSpawn(Runner, teamBGuardPrefab.gameObject, teamBGuardPosition))
                throw new InvalidOperationException("Fortress objective or guard spawn overlaps a protected opposing spawn zone.");
            kingA = Runner.Spawn(teamAKingPrefab, teamAKingPosition, Quaternion.identity, PlayerRef.None,
                (runner, obj) => obj.GetComponent<NetworkKingObjective>().Initialize(Object.Id));
            kingB = Runner.Spawn(teamBKingPrefab, teamBKingPosition, Quaternion.identity, PlayerRef.None,
                (runner, obj) => obj.GetComponent<NetworkKingObjective>().Initialize(Object.Id));
            TeamAKingId = kingA.Object.Id; TeamBKingId = kingB.Object.Id;
            // All round objects exist during the countdown, with gameplay gates closed.
            Runner.Spawn(teamAGuardPrefab, teamAGuardPosition, Quaternion.identity, PlayerRef.None);
            Runner.Spawn(teamBGuardPrefab, teamBGuardPosition, Quaternion.identity, PlayerRef.None);
        }
        internal bool Owns(NetworkKingObjective king)
            => king != null && king.Runner == Runner && (king == kingA || king == kingB);
        internal bool TryFinish(NetworkKingObjective defeated)
        {
            if (!IsServerOwner || State != MatchPhase.Playing || !Owns(defeated) || !defeated.Stats.IsDead) return false;
            Winner = defeated.Team == TeamId.TeamA ? TeamId.TeamB : TeamId.TeamA;
            FinishCount = 1;
            MatchEnded = true;
            SetPhase(MatchPhase.Finished, lifecycle.FinishedDelay);
            NotifyObservers();
            return true;
        }
        private void NotifyObservers()
        {
            if (observedState != StateSequence)
            {
                observedState = StateSequence;
                if (StateChanged != null) foreach (Action<MatchPhase> handler in StateChanged.GetInvocationList())
                    try { handler(State); } catch (Exception exception) { Debug.LogException(exception, this); }
            }
            if (!MatchEnded || observedFinishRound == RoundNumber) return;
            observedFinishRound = RoundNumber;
            if (Ended != null) foreach (Action<TeamId> handler in Ended.GetInvocationList())
                try { handler(Winner); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        public override void Render() => NotifyObservers();
        public override void Despawned(NetworkRunner runner, bool hasState)
            => runner.GetComponentInParent<NetworkFortressSpawner>()?.Unbind(this);
    }
}
