using HeroesOfDragonland.Match;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(MatchManager))]
    public sealed class MatchStatusDisplay : MonoBehaviour
    {
        private MatchManager match;
        private void Awake() => match = GetComponent<MatchManager>();
        private void OnEnable() => match.MatchEnded += ReportWinner;
        private void OnDisable() => match.MatchEnded -= ReportWinner;
        private void ReportWinner(TeamId winner) => Debug.Log("Match finished: " + TeamName(winner) + " wins!", this);
        private static string TeamName(TeamId team) => team == TeamId.TeamA ? "Team A" : "Team B";
        private void OnGUI()
        {
            string message = match.State == MatchState.Finished ? TeamName(match.Winner) + " wins!"
                : match.State == MatchState.Playing ? "Destroy the enemy King" : "Waiting for match";
            GUI.Box(new Rect(Screen.width * 0.5f - 150, 15, 300, 40), message);
        }
    }
}
