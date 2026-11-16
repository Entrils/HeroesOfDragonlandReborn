using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class LocalMusicContext : MonoBehaviour
    {
        [SerializeField, Min(1)] private float combatRadius = 18;
        [SerializeField, Min(1)] private float bossRadius = 36;
        [SerializeField, Min(1)] private float fortressRadius = 24;
        [SerializeField, Range(.05f, .9f)] private float criticalKingHealth = .35f;
        private MultiplayerBootstrap bootstrap;
        private NetworkRunner previousRunner;
        private NetworkMatch previousMatch;
        private int session;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void OnEnable() { previousRunner = null; previousMatch = null; session++; }
        private static bool Engaged(NetworkEnemy enemy) => enemy.State == EnemyState.Alert || enemy.State == EnemyState.Chase || enemy.State == EnemyState.Attack;
        private static bool Hostile(TeamId other, TeamId local) => other != TeamId.Unassigned && local != TeamId.Unassigned && other != local;
        private static bool Near(Vector3 a, Vector3 b, float radius) { a.y = b.y = 0; return (a - b).sqrMagnitude <= radius * radius; }
        public MusicContext Capture(double now)
        {
            var runner = bootstrap.Runner;
            var match = runner != null && runner.IsRunning ? NetworkMatchPacing.MatchFor(runner) : null;
            if (match != null && (match.Object == null || !match.Object.IsValid)) match = null;
            if (!ReferenceEquals(previousRunner, runner) || !ReferenceEquals(previousMatch, match))
            { session++; previousRunner = runner; previousMatch = match; }
            var frame = new MusicContext { Session = session, Phase = MusicPhase.Lobby };
            if (match == null) return frame;
            frame.Round = match.RoundNumber;
            frame.Phase = match.State == MatchPhase.Playing ? MusicPhase.Playing : match.State == MatchPhase.Finished ? MusicPhase.Finished : MusicPhase.Lobby;
            NetworkObject local = null;
            if (runner.LocalPlayer != PlayerRef.None && runner.TryGetPlayerObject(runner.LocalPlayer, out var candidate) && candidate != null && candidate.IsValid) local = candidate;
            var team = local != null ? local.GetComponent<NetworkTestPlayer>() : null;
            TeamId localTeam = team != null ? team.Team : TeamId.Unassigned;
            frame.HasTeam = localTeam == TeamId.TeamA || localTeam == TeamId.TeamB;
            if (frame.Phase == MusicPhase.Finished && match.Winner == TeamId.Unassigned) frame.HasTeam = false;
            frame.Won = frame.HasTeam && match.Winner == localTeam;
            var localSubject = local != null ? local.GetComponent<MusicContextSubject>() : null;
            frame.LocalActive = frame.HasTeam && localSubject != null && localSubject.Alive;
            if (frame.Phase != MusicPhase.Playing) return frame;
            foreach (var subject in MusicContextSubject.Active)
                if (subject != null && subject.Valid && subject.Actor.Runner == runner) subject.SampleHealth(frame.Round, now);
            bool localAction = frame.LocalActive && ((localSubject.Combat != null && localSubject.Combat.AttackBusy) || now - localSubject.DamagedAt < 2);
            foreach (var subject in MusicContextSubject.Active)
            {
                if (subject == null || !subject.Alive || subject.Actor.Runner != runner || subject == localSubject) continue;
                if (subject.King != null && frame.HasTeam && subject.King.Team == localTeam)
                {
                    if (now - subject.DamagedAt < 2) frame.KingDanger = true;
                    if (subject.Stats.Health / subject.Stats.MaxHealth <= criticalKingHealth)
                        foreach (var attacker in MusicContextSubject.Active)
                            if (attacker != null && attacker.Alive && attacker.Actor.Runner == runner && attacker.Player != null
                                && Hostile(attacker.Team, localTeam) && attacker.Combat != null && attacker.Combat.AttackBusy
                                && Near(attacker.transform.position, subject.transform.position, combatRadius)) frame.KingDanger = true;
                }
                if (!frame.LocalActive) continue;
                bool nearby = Near(subject.transform.position, local.transform.position, combatRadius);
                if (subject.Player != null && Hostile(subject.Team, localTeam) && nearby
                    && (localAction || (subject.Combat != null && subject.Combat.AttackBusy))) frame.PvPCombat = true;
                if (subject.Enemy != null && !subject.Stats.Dead && subject.Team != localTeam)
                {
                    var enemy = subject.Enemy;
                    bool boss = enemy.Definition != null && enemy.Definition.Boss != null;
                    bool active = Engaged(enemy) || (boss && enemy.BossPhase == BossPhase.Transition);
                    bool targetLocal = enemy.TargetId == local.Id;
                    bool participating = targetLocal || (nearby && localAction);
                    if (boss && active && Near(subject.transform.position, local.transform.position, bossRadius)) frame.Boss = true;
                    else if (!boss && active && participating && nearby)
                    {
                        if (subject.Team == TeamId.Unassigned) frame.CampCombat = true;
                        else if (subject.Guard != null && Hostile(subject.Team, localTeam)) frame.Fortress = true;
                    }
                }
                // An actual assault needs recent objective damage near the local player, not mere fortress proximity.
                if ((subject.Gate != null || subject.King != null) && now - subject.DamagedAt < 2
                    && Near(subject.transform.position, local.transform.position, fortressRadius)) frame.Fortress = true;
            }
            return frame;
        }
    }
}
