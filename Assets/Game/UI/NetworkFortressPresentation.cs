using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkFortressSpawner))]
    public sealed class NetworkFortressPresentation : MonoBehaviour
    {
        private NetworkFortressSpawner fortress;
        private void Awake() => fortress = GetComponent<NetworkFortressSpawner>();
        private void OnGUI()
        {
            if (Application.isBatchMode) return;
            var match = fortress.Match;
            if (match == null || match.Object == null || !match.Object.IsValid) return;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 220, 12, 440, 340), GUI.skin.box);
            GUILayout.Label(match.MatchEnded ? match.Winner + " wins!" : "FORTRESS BATTLE / " + match.State);
            GUILayout.Label("Round " + match.RoundNumber + " | Team A: " + match.TeamACount + " | Team B: " + match.TeamBCount);
            if (match.State == MatchPhase.Lobby)
                GUILayout.Label("Waiting for " + match.RequiredPlayers + " players, with both teams represented.");
            if (match.State == MatchPhase.Starting)
                GUILayout.Label(match.SecondsRemaining > 0 ? "Battle starts in " + Mathf.CeilToInt(match.SecondsRemaining)
                    : "Waiting for free team spawn points...");
            if (match.State == MatchPhase.Finished)
                GUILayout.Label("Returning to lobby in " + Mathf.CeilToInt(match.SecondsRemaining));
            if (match.State == MatchPhase.ReturningToLobby) GUILayout.Label("Resetting round and returning to lobby...");
            for (int i = 0; i < NetworkTeamRoster.PlayerCapacity; i++)
            {
                var slot = match.LobbyPlayers[i];
                if (slot.Player == Fusion.PlayerRef.None) continue;
                GUILayout.Label(slot.Player + " / " + slot.Team + (slot.HasAvatar ? "" : " / waiting for spawn")
                    + (slot.Player == match.Runner.LocalPlayer ? " (you)" : ""));
            }
            if (match.PreparationError.ToString().Length > 0)
            {
                GUILayout.Label(match.PreparationError.ToString());
                if (match.IsServerOwner && match.State == MatchPhase.Lobby && GUILayout.Button("Retry preparation"))
                    match.RetryPreparation();
            }
            foreach (var id in new[] { match.TeamAKingId, match.TeamBKingId })
                if (match.Runner.TryFindObject(id, out var king))
                {
                    var stats = king.GetComponent<NetworkCharacterStats>();
                    GUILayout.Label(king.GetComponent<NetworkKingObjective>().Team + " King: "
                        + Mathf.CeilToInt(stats.Health) + " / " + Mathf.CeilToInt(stats.MaxHealth));
                }
            if (match.MatchEnded) GUILayout.Label("Combat and progression are closed for this match.");
            GUILayout.EndArea();
        }
    }
}
