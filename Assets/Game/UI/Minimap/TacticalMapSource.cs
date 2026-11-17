using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.UI.Minimap
{
    [DefaultExecutionOrder(260), DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap), typeof(TemporaryEnemyReveals))]
    public sealed class TacticalMapSource : MonoBehaviour
    {
        [SerializeField] private TacticalMapDefinition definition;
        private MultiplayerBootstrap bootstrap;
        private TemporaryEnemyReveals reveals;
        private readonly List<MapMarker> markers = new();
        private float nextRefresh;
        public TacticalMapDefinition Definition => definition;
        public IReadOnlyList<MapMarker> Markers => markers;
        public bool Visible { get; private set; }
        public float Heading { get; private set; }
        public int Revision { get; private set; }
        public static MapTeam Team(TeamId team) => team == TeamId.TeamA ? MapTeam.A : team == TeamId.TeamB ? MapTeam.B : MapTeam.None;
        private static MapPoint Point(Vector3 point) => new MapPoint(point.x, point.y, point.z);
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>(); reveals = GetComponent<TemporaryEnemyReveals>();
            if (definition == null || !definition.Validate(out _))
            { Debug.LogError("[M62] Complete and validate the public tactical map definition before enabling the source.", this); enabled = false; }
        }
        private void OnEnable() => nextRefresh = 0;
        private void OnDisable() { markers.Clear(); Visible = false; Revision++; if (reveals != null) reveals.Bind(null, MapTeam.None); }
        private void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f; Refresh();
        }
        public void Refresh()
        {
            markers.Clear(); Visible = false; Revision++;
            var runner = bootstrap.Runner;
            var match = runner != null && runner.IsRunning ? NetworkMatchPacing.MatchFor(runner) : null;
            NetworkObject local = null;
            if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var candidate) && candidate != null && candidate.IsValid && candidate.HasInputAuthority) local = candidate;
            var player = local != null ? local.GetComponent<NetworkTestPlayer>() : null;
            var viewer = player != null ? Team(player.Team) : MapTeam.None;
            if (definition == null || match == null || match.Object == null || !match.Object.IsValid
                || (match.State != MatchPhase.Playing && match.State != MatchPhase.Finished) || viewer == MapTeam.None || player.IsSuspended)
            { reveals.Bind(null, MapTeam.None); return; }
            reveals.Bind(runner.GetEntityId() + ":" + match.Object.Id + ":" + match.RoundNumber + ":" + local.Id, viewer);
            Visible = true; Heading = local.transform.eulerAngles.y;
            foreach (var landmark in definition.Landmarks)
                markers.Add(new MapMarker { Id = "landmark:" + landmark.id, Label = landmark.label, Kind = landmark.kind,
                    Team = landmark.team, Position = Point(landmark.position), State = PublicState(landmark, runner, match) });
            foreach (var subject in TacticalMapSubject.Active)
            {
                if (subject == null || !subject.Valid || subject.Actor.Runner != runner || subject.Player == null) continue;
                // Team is public; do not read an opposing player's coordinates/heading/health.
                var subjectTeam = Team(subject.Player.Team);
                if (subjectTeam != viewer) continue;
                bool dead = subject.Stats == null || subject.Stats.MaxHealth <= 0 || subject.Stats.Dead;
                bool self = subject.Actor == local;
                if (!TacticalMapRules.TryPlayer(viewer, subjectTeam, self, subject.Player.IsSuspended, dead,
                    () => Point(subject.transform.position), out var position)) continue;
                markers.Add(new MapMarker { Id = "ally:" + subject.Actor.Id, Kind = self ? MapMarkerKind.LocalPlayer : MapMarkerKind.Ally,
                    Label = self ? "You" : "Ally", Team = viewer, State = MapMarkerState.Active, Position = position, Heading = self ? Heading : 0 });
            }
            King(match.TeamAKingId, MapTeam.A, runner); King(match.TeamBKingId, MapTeam.B, runner);
            reveals.Append(markers);
        }
        private MapMarkerState PublicState(TacticalMapDefinition.Landmark landmark, NetworkRunner runner, NetworkMatch match)
        {
            if (landmark.kind == MapMarkerKind.Fortress) return MapMarkerState.Active;
            TacticalMapSubject found = null;
            foreach (var subject in TacticalMapSubject.Active)
            {
                if (subject == null || !subject.Valid || subject.Actor.Runner != runner) continue;
                bool camp = landmark.kind == MapMarkerKind.Camp || landmark.kind == MapMarkerKind.BossCamp;
                if (camp && subject.Camp == null) continue;
                if (!camp && (subject.Gate == null || subject.Gate.MatchId != match.Object.Id)) continue;
                // Only public camp/gate positions, never an arbitrary actor's transform.
                if ((subject.transform.position - landmark.position).sqrMagnitude > 1) continue;
                if (found != null) return MapMarkerState.Unknown; // Ambiguous public anchors fail closed.
                found = subject;
            }
            if (found == null) return MapMarkerState.Unknown;
            if (found.Gate != null && landmark.kind == MapMarkerKind.Objective) return found.Gate.IsOpen ? MapMarkerState.Cleared : MapMarkerState.Active;
            return found.Camp.State == NetworkCampState.Cleared ? MapMarkerState.Cleared
                : found.Camp.State == NetworkCampState.Active ? MapMarkerState.Active
                : found.Camp.State == NetworkCampState.Faulted ? MapMarkerState.Unavailable : MapMarkerState.Unknown;
        }
        private void King(NetworkId id, MapTeam team, NetworkRunner runner)
        {
            if (id == default || !runner.TryFindObject(id, out var actor) || actor == null || !actor.IsValid) return;
            var king = actor.GetComponent<NetworkKingObjective>(); var stats = actor.GetComponent<NetworkCharacterStats>();
            if (king == null || Team(king.Team) != team) return;
            markers.Add(new MapMarker { Id = "king:" + team, Label = "King " + team, Kind = MapMarkerKind.King, Team = team,
                Position = Point(actor.transform.position), State = stats == null || stats.MaxHealth <= 0 ? MapMarkerState.Unknown
                    : stats.Dead ? MapMarkerState.Cleared : MapMarkerState.Active });
        }
    }
}
