using System;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.Match
{
    [DisallowMultipleComponent]
    public sealed class MatchManager : MonoBehaviour
    {
        [SerializeField] private KingObjective teamAKing;
        [SerializeField] private KingObjective teamBKing;
        [SerializeField] private bool startAutomatically = true;
        private readonly MatchStateMachine state = new MatchStateMachine();
        private KingObjective boundA;
        private KingObjective boundB;
        public MatchState State => state.State;
        public TeamId Winner => state.Winner;
        public event Action<MatchState> StateChanged;
        public event Action<TeamId> MatchEnded;

        private void Start()
        {
            if (startAutomatically && State == MatchState.Waiting && !TryStartMatch(out string reason))
                Debug.LogError(reason, this);
        }

        public bool TryStartMatch(out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled || State != MatchState.Waiting)
                reason = "Only an active Waiting match can start.";
            else if (teamAKing == null || teamBKing == null || teamAKing == teamBKing)
                reason = "Assign one distinct King for each team.";
            else if (!teamAKing.CanBind(this, TeamId.TeamA, out reason)
                || !teamBKing.CanBind(this, TeamId.TeamB, out reason)) return false;
            if (reason != null) return false;
            // Bind only after the complete roster has passed validation.
            boundA = teamAKing; boundB = teamBKing;
            boundA.Bind(this, TeamId.TeamA); boundB.Bind(this, TeamId.TeamB);
            boundA.Defeated += OnKingDefeated; boundB.Defeated += OnKingDefeated;
            state.TryStart();
            NotifyState(MatchState.Playing);
            return true;
        }

        internal bool AllowsObjectiveDamage(KingObjective king)
        {
            if (!isActiveAndEnabled || State != MatchState.Playing || (king != boundA && king != boundB)) return false;
            // A reentrant hit from an earlier death listener cannot kill the other King
            // before this match's own death callback is reached.
            if (boundA != null && boundA.IsDefeated) OnKingDefeated(boundA);
            else if (boundB != null && boundB.IsDefeated) OnKingDefeated(boundB);
            return State == MatchState.Playing;
        }

        private void OnKingDefeated(KingObjective king)
        {
            if (king == null || !king.IsDefeated) return;
            TeamId defeated = king == boundA ? TeamId.TeamA : king == boundB ? TeamId.TeamB : TeamId.Unassigned;
            if (!state.TryFinish(defeated)) return;
            // Winner/Finished and both objective gates are committed before any observer.
            NotifyState(MatchState.Finished);
            if (MatchEnded == null) return;
            foreach (Action<TeamId> handler in MatchEnded.GetInvocationList())
                try { handler(Winner); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void NotifyState(MatchState value)
        {
            if (StateChanged == null) return;
            foreach (Action<MatchState> handler in StateChanged.GetInvocationList())
            {
                if (State != value) break; // A Playing listener may end the match synchronously.
                try { handler(value); } catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
        private void OnDestroy()
        {
            if (boundA != null) { boundA.Defeated -= OnKingDefeated; boundA.Unbind(this); }
            if (boundB != null) { boundB.Defeated -= OnKingDefeated; boundB.Unbind(this); }
        }
    }
}
