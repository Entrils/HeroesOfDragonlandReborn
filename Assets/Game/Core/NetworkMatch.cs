using System;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Profiles;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public struct NetworkLobbySlot : INetworkStruct
    {
        public PlayerRef Player;
        public TeamId Team;
        public NetworkBool HasAvatar;
        public NetworkBool IsBot;
        public NetworkBool Ready;
    }

    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatch : NetworkBehaviour
    {
        [SerializeField] private MatchLifecycleConfig lifecycle;
        [SerializeField] private MatchBalanceConfig balance;
        public MatchBalanceConfig Balance => balance;
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
        private readonly OnlineLobbyState lobby = new();
        private readonly MatchSessionData profileRound = new();
        private readonly MatchCombatAttribution attribution = new();
        private bool pendingResults;
        private int rankedStartHumans;
        [Networked] private NetworkBool DetailedResultRated { get; set; }
        [Networked] private NetworkString<_32> DetailedResultId { get; set; }
        [Networked] private int DetailedResultRound { get; set; }
        [Networked] private TeamId DetailedResultWinner { get; set; }
        [Networked] private float DetailedResultDuration { get; set; }
        [Networked] private int DetailedResultCount { get; set; }
        [Networked, Capacity(NetworkTeamRoster.PlayerCapacity)] private NetworkArray<MatchPlayerResult> DetailedResultRows => default;
        public MatchResult LatestResult { get; private set; }
        public event Action<MatchResult> DetailedResultPublished;
        [Networked] public NetworkBool AllowShortMatch { get; private set; }
        public bool LocalReady
        {
            get { foreach (var slot in LobbyPlayers) if (!slot.IsBot && slot.Player == Runner.LocalPlayer) return slot.Ready; return false; }
        }
        public bool CanStartLobby
        {
            get
            {
                if (State != MatchPhase.Lobby || PreparationError.ToString().Length != 0) return false;
                int a = 0, b = 0, humans = 0;
                foreach (var slot in LobbyPlayers)
                {
                    if (slot.Team == TeamId.Unassigned) continue;
                    if (!slot.HasAvatar || (!slot.IsBot && !slot.Ready)) return false;
                    if (!slot.IsBot) humans++;
                    if (slot.Team == TeamId.TeamA) a++; else if (slot.Team == TeamId.TeamB) b++;
                }
                if (bootstrap.IsRankedQueue && (humans != NetworkTeamRoster.PlayerCapacity || Balance == null || !Balance.RankedRating.IsValid)) return false;
                bool participantsAllowed = humans > 0;
#if UNITY_EDITOR || DEBUG
                participantsAllowed |= bootstrap != null && bootstrap.SimulationSpectator;
#endif
                return participantsAllowed && MatchLifecycleRules.HasEnoughPlayers(a, b, AllowShortMatch ? 2 : 6);
            }
        }
        // Sender identity comes from Fusion, never from a client-supplied player ID.
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_SetReady(bool ready, RpcInfo info = default)
        {
            if (!IsServerOwner || State != MatchPhase.Lobby) return;
            if (!NetworkParticipant.TryResolveSender(Runner, info.Source, out _))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.Sender);
                return;
            }
            lobby.SetReady(info.Source, ready);
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_LeaveSession(RpcInfo info = default)
        {
            if (!IsServerOwner) return;
            if (!NetworkParticipant.TryResolveSender(Runner, info.Source, out _))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.Sender);
                return;
            }
            bootstrap.ForfeitReconnect(info.Source);
            RPC_LeaveAcknowledged(info.Source);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_LeaveAcknowledged([RpcTarget] PlayerRef player) => bootstrap.ConfirmLeave();

        public bool SetShortMatch(bool allow)
        {
            if (!IsServerOwner || State != MatchPhase.Lobby || bootstrap.IsRankedQueue) return false;
#if UNITY_EDITOR || DEBUG
            if (AllowShortMatch != allow) lobby.ClearReady();
            AllowShortMatch = allow;
            RequiredPlayers = allow ? 2 : 6;
            return true;
#else
            return false;
#endif
        }
        public bool TryStartLobby()
        {
            if (!IsServerOwner) return false;
            RefreshLobby();
            if (!CanStartLobby) return false;
            BeginRound();
            return State == MatchPhase.Starting;
        }
        private NetworkKingObjective kingA, kingB;
        private MultiplayerBootstrap bootstrap;
        private NetworkRoundReset reset;
        private int observedState = -1, observedFinishRound = -1;
        public MatchLifecycleConfig Configuration => lifecycle;
        public float SecondsRemaining => Object != null && Object.IsValid ? PhaseTimer.RemainingTime(Runner) ?? 0 : 0;
        public event Action<MatchPhase> StateChanged;
        public event Action<TeamId> Ended;
        public event Action<OnlineMatchResult> ResultPublished;
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
                RequiredPlayers = NetworkTeamRoster.PlayerCapacity;
            }
            NotifyObservers();
        }
        public override void FixedUpdateNetwork()
        {
            if (!IsServerOwner) return;
            if (pendingResults) PublishDetailedResult();
            RefreshLobby();
            switch (State)
            {
                case MatchPhase.Boot:
                    if (lifecycle == null || reset == null)
                        PreparationError = "Assign MatchLifecycleConfig and NetworkRoundReset before starting.";
                    SetPhase(MatchPhase.Lobby);
                    break;
                case MatchPhase.Lobby:
                    // Starting requires an explicit server/host action after all humans ready.
                    break;
                case MatchPhase.Starting:
                    if (!EnoughConnections) BeginReturn();
                    else if (EnoughPlayers && PhaseTimer.Expired(Runner))
                    {
                        foreach (var avatar in bootstrap.Avatars) profileRound.Include(avatar);
                        var (a, b) = CountRankedHumans();
                        rankedStartHumans = a == NetworkTeamRoster.TeamCapacity && b == NetworkTeamRoster.TeamCapacity ? a + b : 0;
                        if (bootstrap.IsRankedQueue && rankedStartHumans != NetworkTeamRoster.PlayerCapacity)
                        {
                            BeginReturn();
                            break;
                        }
                        SetPhase(MatchPhase.Playing);
                    }
                    break;
                case MatchPhase.Playing:
                    PlayingSeconds += Runner.DeltaTime;
                    // An empty room is abandoned without inventing a winning team.
                    if (TeamACount + TeamBCount == 0 && bootstrap.ReservedPlayerCount == 0) BeginReturn();
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
            int botCount = 0;
#if UNITY_EDITOR || DEBUG
            botCount = bootstrap.GetComponent<DeveloperBotService>()?.SpawnedCount ?? 0;
#endif
            lobby.Observe(Runner.ActivePlayers, botCount);
            int index = 0, a = 0, b = 0;
            foreach (var player in Runner.ActivePlayers)
            {
#if UNITY_EDITOR || DEBUG
                if (bootstrap.SimulationSpectator && player == Runner.LocalPlayer) continue;
#endif
                if (index >= NetworkTeamRoster.PlayerCapacity) break;
                bootstrap.TryGetTeam(player, out var team);
                bool ready = Runner.TryGetPlayerObject(player, out var obj) && obj != null && obj.IsValid
                    && obj.GetComponent<NetworkTestPlayer>() != null;
                LobbyPlayers.Set(index++, new NetworkLobbySlot { Player = player, Team = team, HasAvatar = ready, Ready = lobby.IsReady(player) });
                if (ready && team == TeamId.TeamA) a++;
                if (ready && team == TeamId.TeamB) b++;
            }
#if UNITY_EDITOR || DEBUG
            var bots = bootstrap.GetComponent<DeveloperBotService>();
            if (bots != null) foreach (var avatar in bots.Avatars)
            {
                if (index >= NetworkTeamRoster.PlayerCapacity) break;
                var bot = avatar.GetComponent<NetworkTestPlayer>();
                LobbyPlayers.Set(index++, new NetworkLobbySlot { Player = PlayerRef.None, Team = bot.Team, HasAvatar = true, IsBot = true, Ready = true });
                if (bot.Team == TeamId.TeamA) a++; else if (bot.Team == TeamId.TeamB) b++;
            }
#endif
            while (index < NetworkTeamRoster.PlayerCapacity) LobbyPlayers.Set(index++, default);
            TeamACount = a; TeamBCount = b;
        }

        private void SetPhase(MatchPhase next, float seconds = -1)
        {
            if (!MatchLifecycleRules.CanTransition(State, next))
                throw new InvalidOperationException("Invalid match transition: " + State + " -> " + next);
            State = next; StateSequence++;
            // Keep transport admission open for authenticated reconnects; bootstrap gates new players.
            bootstrap.SetLobbyVisibility(next == MatchPhase.Lobby);
            if (next == MatchPhase.ReturningToLobby) lobby.ClearReady();
            PhaseTimer = seconds < 0 ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, seconds);
            Debug.Log("[MatchLifecycle] round=" + RoundNumber + " state=" + State, this);
        }

        private void BeginRound()
        {
            if (balance != null && !balance.TryValidate(out var balanceError))
            { PreparationError = balanceError; return; }
            PlayingSeconds = 0;
            rankedStartHumans = 0;
            profileRound.Begin(Guid.NewGuid().ToString("N"));
            attribution.Clear();
            pendingResults = false;
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
            // DamageTaken follows the King's death callback. Publish on the next tick
            // so the fatal hit can still credit its attacker.
            pendingResults = true;
            SetPhase(MatchPhase.Finished, lifecycle.FinishedDelay);
            NotifyObservers();
            return true;
        }
        internal void RecordCombatDamage(NetworkObject victim, GameObject source, string action, float amount, bool lethal)
        {
            if (IsServerOwner && CanPlay && victim != null && victim.IsValid && victim.HasStateAuthority && victim.Runner == Runner)
            {
                attribution.Record(victim, source, amount, lethal, this, out var killer, out var assistants);
                if (victim.TryGetComponent<NetworkPlayerRespawn>(out var respawn) && NetworkParticipant.IsAdmitted(victim))
                    respawn.RecordRecapHit(source, action, amount, lethal, killer, assistants);
            }
        }

        internal void ForgetCombatTarget(NetworkId victim)
        {
            if (IsServerOwner) attribution.Forget(victim);
        }

        internal void CreditMatch(NetworkObject avatar, MatchStatistics delta)
        {
            if (!IsServerOwner || profileRound.RoundId == null || avatar.Runner != Runner
                || !NetworkParticipant.IsAdmitted(avatar)
                || (State != MatchPhase.Playing && !(pendingResults && delta.KingDamage > 0f))) return;
            profileRound.Add(avatar, delta);
        }

        private (int A, int B) CountRankedHumans()
        {
            if (!bootstrap.IsRankedQueue) return default;
            int a = 0, b = 0;
            foreach (var owner in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(owner, out var avatar) || !NetworkParticipant.IsAdmitted(avatar)
                    || profileRound.For(avatar.Id).ParticipantNumber == 0
                    || !avatar.TryGetComponent<NetworkTestPlayer>(out var player) || player.IsBot) continue;
                if (player.Team == TeamId.TeamA) a++;
                if (player.Team == TeamId.TeamB) b++;
            }
            return (a, b);
        }

        private void PublishDetailedResult()
        {
            pendingResults = false;
            var rows = profileRound.Snapshot();
            DetailedResultCount = rows.Length;
            for (int i = 0; i < DetailedResultCount; i++) DetailedResultRows.Set(i, rows[i]);
            DetailedResultRound = RoundNumber;
            DetailedResultWinner = Winner;
            DetailedResultDuration = PlayingSeconds;
            var (a, b) = CountRankedHumans();
            DetailedResultRated = balance != null && balance.RankedRating.IsValid
                && RankedMatchRules.IsRated(bootstrap.IsRankedQueue, MatchEnded && FinishCount == 1
                    && (Winner is TeamId.TeamA or TeamId.TeamB), rankedStartHumans, a, b);
            DetailedResultId = profileRound.RoundId;

            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out var avatar)) SendResultReceipt(avatar);
            }
        }

        internal void SendResultReceipt(NetworkObject avatar)
        {
            if (!IsServerOwner || pendingResults || State != MatchPhase.Finished || !NetworkParticipant.IsAdmitted(avatar)
                || avatar.InputAuthority == PlayerRef.None) return;
            var row = profileRound.For(avatar.Id);
            if (row.ParticipantNumber == 0) return;
            var rating = DetailedResultRated ? balance.RankedRating : default;
            RPC_ProfileResult(avatar.InputAuthority, DetailedResultId, row.Team == DetailedResultWinner, DetailedResultDuration, row.Statistics,
                DetailedResultRated, rating.StartingRating, rating.WinPoints, rating.LossPoints, rating.ProvisionalMatches);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ProfileResult([RpcTarget] PlayerRef recipient, NetworkString<_32> id, bool won,
            float duration, MatchStatistics statistics, bool rated, int startingRating, int winPoints, int lossPoints, int provisionalMatches)
        {
            if (recipient == PlayerRef.None || recipient != Runner.LocalPlayer || !LocalPlayerProfile.WriteMatchTotals) return;
            var profile = LocalPlayerProfile.Current;
            RankedRatingSettings? settings = rated && bootstrap.IsRankedQueue
                ? new RankedRatingSettings(startingRating, winPoints, lossPoints, provisionalMatches) : null;
            if (!profile.RecordRound(id.ToString(), won, duration, statistics, settings)) return;
#if UNITY_EDITOR || DEBUG
            if (LocalPlayerProfile.IsTestProfile)
            {
                Debug.Log("[ProfileCheck] Recorded matches=" + profile.Data.Totals.Matches);
                var ranked = profile.Data.Ranked;
                Debug.Log($"[RankedCheck] rated={rated} rating={ranked.Rating} matches={ranked.MatchesPlayed} wins={ranked.Wins} losses={ranked.Losses}");
            }
#endif
        }
        private void ObserveDetailedResult()
        {
            string id = DetailedResultId.ToString();
            if (id.Length == 0 || LatestResult?.Id == id) return;
            var rows = new MatchPlayerResult[DetailedResultCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = DetailedResultRows[i];
            LatestResult = new MatchResult(id, DetailedResultRound, DetailedResultWinner, DetailedResultDuration, rows, DetailedResultRated);
            if (DetailedResultPublished != null)
                foreach (Action<MatchResult> handler in DetailedResultPublished.GetInvocationList())
                    try { handler(LatestResult); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void NotifyObservers()
        {
            ObserveDetailedResult();
            if (observedState != StateSequence)
            {
                observedState = StateSequence;
                if (StateChanged != null) foreach (Action<MatchPhase> handler in StateChanged.GetInvocationList())
                    try { handler(State); } catch (Exception exception) { Debug.LogException(exception, this); }
            }
            if (!MatchEnded || observedFinishRound == RoundNumber) return;
            observedFinishRound = RoundNumber;
            var result = new OnlineMatchResult(bootstrap != null ? bootstrap.RoomName : null, RoundNumber, Winner, PlayingSeconds);
            if (ResultPublished != null) foreach (Action<OnlineMatchResult> handler in ResultPublished.GetInvocationList())
                try { handler(result); } catch (Exception exception) { Debug.LogException(exception, this); }
            if (Ended != null) foreach (Action<TeamId> handler in Ended.GetInvocationList())
                try { handler(Winner); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        public override void Render() => NotifyObservers();
        public override void Despawned(NetworkRunner runner, bool hasState)
            => runner.GetComponentInParent<NetworkFortressSpawner>()?.Unbind(this);
    }
}
