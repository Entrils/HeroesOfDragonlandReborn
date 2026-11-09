using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(GameplayHudView))]
    public sealed class GameplayHudPresenter : MonoBehaviour
    {
        [SerializeField] private MultiplayerBootstrap bootstrap;
        private GameplayHudView view;
        private NetworkObject actor;
        private CharacterStats stats, targetStats, kingA, kingB;
        private SessionProgression progression;
        private TargetLock targeting;
        private Targetable target;
        private NetworkPlayerRespawn respawn;
        private NetworkMatch match;
        private TeamId localTeam;
        public NetworkObject LocalActor => actor;
        public GameplayHudView View => view;
        private void Awake() => view = GetComponent<GameplayHudView>();
        private void OnEnable()
        {
            if (bootstrap != null) bootstrap.StatusChanged += OnConnectionChanged;
        }
        private void OnDisable()
        {
            if (bootstrap != null) bootstrap.StatusChanged -= OnConnectionChanged;
            BindPlayer(null); BindMatch(null);
        }
        private void OnConnectionChanged(string _) { if (bootstrap == null || !bootstrap.Running) { BindPlayer(null); BindMatch(null); } }
        private void LateUpdate()
        {
            var runner = bootstrap != null ? bootstrap.Runner : null;
            NetworkObject local = null;
            if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var candidate)
                && candidate != null && candidate.IsValid && candidate.HasInputAuthority) local = candidate;
            // Discovery only: lifecycle changes replace avatars and objective IDs. Values use events.
            if (!ReferenceEquals(local, actor)) BindPlayer(local);
            var fortress = bootstrap != null ? bootstrap.GetComponent<NetworkFortressSpawner>() : null;
            var current = fortress != null ? fortress.Match : null;
            if (current != null && (current.Object == null || !current.Object.IsValid)) current = null;
            if (!ReferenceEquals(current, match)) BindMatch(current);
            if (match != null)
            {
                BindKing(true, FindStats(match.TeamAKingId));
                BindKing(false, FindStats(match.TeamBKingId));
            }
            RefreshMatch(); // Replicated timers/roster have no change events; read their current snapshot.
            RefreshRespawn();
            if (!ReferenceEquals(target, null) && (target == null || !target.IsValid)) OnTargetChanged(null);
            if (target != null) view.PlaceTarget(Camera.main, target.AimPosition);
        }
        private CharacterStats FindStats(NetworkId id)
            => match.Runner.TryFindObject(id, out var obj) && obj != null ? obj.GetComponent<CharacterStats>() : null;
        private void BindPlayer(NetworkObject value)
        {
            if (stats != null) { stats.HealthChanged -= view.SetHealth; stats.StaminaChanged -= view.SetStamina; stats.ManaChanged -= view.SetMana; }
            if (progression != null) progression.Changed -= RefreshProgression;
            if (targeting != null) targeting.TargetChanged -= OnTargetChanged;
            if (respawn != null) { respawn.Died -= RefreshRespawn; respawn.Respawned -= RefreshRespawn; respawn.RespawnCountdownChanged -= OnCountdown; }
            OnTargetChanged(null);
            actor = value; stats = null; progression = null; targeting = null; respawn = null;
            localTeam = actor != null ? actor.GetComponent<NetworkTestPlayer>().Team : TeamId.Unassigned;
            view.SetLocal(actor != null);
            if (actor == null) return;
            stats = actor.GetComponent<CharacterStats>();
            progression = actor.GetComponent<SessionProgression>();
            targeting = actor.GetComponent<TargetLock>();
            respawn = actor.GetComponent<NetworkPlayerRespawn>();
            if (stats != null)
            {
                stats.HealthChanged += view.SetHealth; stats.StaminaChanged += view.SetStamina; stats.ManaChanged += view.SetMana;
                view.SetHealth(stats.CurrentHealth, stats.MaxHealth);
                view.SetStamina(stats.CurrentStamina, stats.MaxStamina);
                view.SetMana(stats.CurrentMana, stats.MaxMana);
            }
            if (progression != null) progression.Changed += RefreshProgression;
            RefreshProgression();
            if (targeting != null) { targeting.TargetChanged += OnTargetChanged; OnTargetChanged(targeting.CurrentTarget); }
            if (respawn != null) { respawn.Died += RefreshRespawn; respawn.Respawned += RefreshRespawn; respawn.RespawnCountdownChanged += OnCountdown; }
            RefreshRespawn();
        }
        private void RefreshProgression()
        {
            if (actor == null || !actor.IsValid) return;
            view.SetIdentity(TeamName(localTeam) + "  ·  Level " + (progression != null ? progression.Level : 1));
            view.SetExperience(progression != null ? progression.CurrentXP : 0, progression != null ? progression.XPRequiredForNextLevel : 0, progression != null && progression.IsMaximumLevel);
        }
        private void OnCountdown(float _) => RefreshRespawn();
        private void RefreshRespawn()
        {
            string text = "";
            if (respawn != null && respawn.Object != null && respawn.Object.IsValid && stats != null && stats.IsDead)
                text = respawn.WaitingForRespawn ? respawn.SecondsUntilRespawn > 0 ? "Respawning in " + Mathf.CeilToInt(respawn.SecondsUntilRespawn) + "s" : "Waiting for a free team spawn point…"
                    : match != null && match.MatchEnded ? "Round over" : "You are down";
            view.SetRespawn(text);
        }
        private void OnTargetChanged(Targetable value)
        {
            if (targetStats != null) targetStats.HealthChanged -= view.SetTargetHealth;
            target = value; targetStats = null;
            // Only the local lock's explicitly selected, same-session target exposes health.
            var networkTarget = value != null ? value.GetComponentInParent<NetworkObject>() : null;
            if (actor == null || networkTarget == null || !networkTarget.IsValid || networkTarget.Runner != actor.Runner) target = null;
            view.SetTarget(target != null);
            if (target == null) return;
            view.SetTargetName(networkTarget.name.Replace("(Clone)", ""));
            targetStats = target.GetComponentInParent<CharacterStats>();
            if (targetStats != null) { targetStats.HealthChanged += view.SetTargetHealth; view.SetTargetHealth(targetStats.CurrentHealth, targetStats.MaxHealth); }
            else view.SetTargetHealth(0, 0);
        }
        private void BindMatch(NetworkMatch value)
        {
            if (match != null) { match.StateChanged -= OnPhase; match.Ended -= OnEnded; }
            BindKing(true, null); BindKing(false, null);
            match = value;
            if (match != null) { match.StateChanged += OnPhase; match.Ended += OnEnded; }
            RefreshMatch();
        }
        private void OnPhase(MatchPhase _) { RefreshMatch(); RefreshRespawn(); }
        private void OnEnded(TeamId _) => RefreshMatch();
        private void BindKing(bool isA, CharacterStats value)
        {
            var previous = isA ? kingA : kingB;
            if (ReferenceEquals(previous, value)) return;
            if (previous != null) { if (isA) previous.HealthChanged -= OnKingA; else previous.HealthChanged -= OnKingB; }
            if (isA) kingA = value; else kingB = value;
            if (value != null) { if (isA) value.HealthChanged += OnKingA; else value.HealthChanged += OnKingB; }
            view.SetKing(isA, value != null ? value.CurrentHealth : 0, value != null ? value.MaxHealth : 0);
        }
        private void OnKingA(float hp, float max) => view.SetKing(true, hp, max);
        private void OnKingB(float hp, float max) => view.SetKing(false, hp, max);
        private static string TeamName(TeamId team) => team == TeamId.TeamA ? "Team A" : team == TeamId.TeamB ? "Team B" : "Unassigned";
        private void RefreshMatch()
        {
            if (match == null || match.Object == null || !match.Object.IsValid)
            { view.SetMatch("Not connected", "Join a match to see objectives.", ""); return; }
            string state = "Round " + match.RoundNumber + " · " + match.State;
            string goal = "Protect your King. Defeat the enemy King.";
            if (match.State == MatchPhase.Lobby) goal = "Waiting for players: " + (match.TeamACount + match.TeamBCount) + " / " + match.RequiredPlayers + " · Both teams required";
            if (match.State == MatchPhase.Starting) goal = match.SecondsRemaining > 0 ? "Starting in " + Mathf.CeilToInt(match.SecondsRemaining) + "s" : "Waiting for team spawn points…";
            if (match.State == MatchPhase.Finished) goal = "Returning to lobby in " + Mathf.CeilToInt(match.SecondsRemaining) + "s";
            if (match.State == MatchPhase.ReturningToLobby) goal = "Resetting round…";
            if (match.PreparationError.ToString().Length > 0) goal = match.PreparationError.ToString();
            string outcome = "";
            if (match.MatchEnded)
            {
                // Phase callbacks can run after despawn, before LateUpdate rebinds the avatar.
                outcome = localTeam != TeamId.Unassigned ? (localTeam == match.Winner ? "VICTORY" : "DEFEAT") + " · " + TeamName(match.Winner) : TeamName(match.Winner) + " wins";
            }
            view.SetMatch(state, goal, outcome);
        }
    }
}
