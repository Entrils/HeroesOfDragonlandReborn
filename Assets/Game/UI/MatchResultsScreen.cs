using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    internal sealed class MatchResultsScreen
    {
        private MatchResult result;
        private Vector2 scroll;
        private int playerIndex;
        public bool IsOpen { get; private set; }
        public bool HasResult => result != null;

        public void Observe(MatchResult next, bool show, int localParticipant)
        {
            if (next == null || result?.Id == next.Id) return;
            result = next;
            scroll = default;
            playerIndex = 0;
            for (int i = 0; i < result.Players.Count; i++)
                if (result.Players[i].ParticipantNumber == localParticipant) playerIndex = i;
            IsOpen = show;
        }

        public void Open() => IsOpen = HasResult;
        public void Close() => IsOpen = false;

        public void Draw()
        {
            var width = Mathf.Min(780, Screen.width - 28);
            var height = Mathf.Min(710, Screen.height - 32);
            var bounds = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            var tint = GUI.color;
            GUI.color = new Color(.05f, .07f, .09f, .98f);
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(bounds, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{TeamName(result.Winner)} wins  |  Round {result.RoundNumber}  |  {FormatDuration(result.DurationSeconds)}");
            if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);
            if (result.Rated) GUILayout.Label("Ranked result");
            GUILayout.Label("Team comparison");
            DrawTotals(result.TeamA, result.TeamB);

            if (result.Players.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("Individual statistics");
                var labels = new string[result.Players.Count];
                for (int i = 0; i < labels.Length; i++)
                {
                    var row = result.Players[i];
                    labels[i] = $"{TeamName(row.Team)} / {(row.IsBot ? "Bot" : "P")}{row.ParticipantNumber}";
                }
                playerIndex = GUILayout.SelectionGrid(playerIndex, labels, Mathf.Min(3, labels.Length));
                Row("Match duration", FormatDuration(result.DurationSeconds), "", false);
                DrawTotals(result.Players[playerIndex].Statistics, null);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string TeamName(TeamId team) => team == TeamId.TeamA ? "Team A" : "Team B";

        internal static string FormatDuration(double seconds)
        {
            long duration = (long)Math.Max(0, seconds);
            return $"{duration / 60}:{duration % 60:00}";
        }

        private static void DrawTotals(MatchStatistics a, MatchStatistics? other)
        {
            var b = other.GetValueOrDefault();
            bool compare = other.HasValue;
            if (compare) Row("", "Team A", "Team B", true);
            Row("Kills", a.Kills, b.Kills, compare);
            Row("Deaths", a.Deaths, b.Deaths, compare);
            Row("Assists", a.Assists, b.Assists, compare);
            Row("Player damage", a.PlayerDamage, b.PlayerDamage, compare);
            Row("Damage taken", a.DamageTaken, b.DamageTaken, compare);
            Row("Healing", a.Healing, b.Healing, compare);
            Row("Camps cleared", a.CampsCleared, b.CampsCleared, compare);
            Row("Boss encounters joined", a.BossParticipation, b.BossParticipation, compare);
            Row("Chests opened", a.ChestsOpened, b.ChestsOpened, compare);
            Row("XP earned", a.ExperienceEarned, b.ExperienceEarned, compare);
            Row("Highest level", a.HighestLevel, b.HighestLevel, compare);
            Row("King damage", a.KingDamage, b.KingDamage, compare);
            Row("Guards killed", a.GuardsKilled, b.GuardsKilled, compare);
            Row("Items acquired", a.ItemsAcquired, b.ItemsAcquired, compare);
        }

        private static void Row(string label, long a, long b, bool compare) => Row(label, a.ToString("N0"), b.ToString("N0"), compare);
        private static void Row(string label, float a, float b, bool compare) => Row(label, a.ToString("N1"), b.ToString("N1"), compare);

        private static void Row(string label, string a, string b, bool compare)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            GUILayout.Label(a, GUILayout.Width(100));
            if (compare) GUILayout.Label(b, GUILayout.Width(100));
            GUILayout.EndHorizontal();
        }
    }
}
