using Fusion;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.UI;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Presentation depends on the session API; future matchmaking can use that same API.
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class MultiplayerLobbyPanel : MonoBehaviour
    {
        [SerializeField] private string room = "dragonland";
        private MultiplayerBootstrap bootstrap;
        private Vector2 scroll;
        private GUIStyle messageStyle;
        private PlaytestSettings settings;
        private LocalGameplayInputGate inputGate;
        private bool settingsOpen, quitting;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PlaytestDiagnostics diagnostics;
#endif
        private void Awake()
        {
            bootstrap = GetComponent<MultiplayerBootstrap>();
            settings = GetComponent<PlaytestSettings>();
            inputGate = GetComponent<LocalGameplayInputGate>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            diagnostics = GetComponent<PlaytestDiagnostics>();
#endif
        }

        private void SetSettingsOpen(bool value)
        {
            settingsOpen = value;
            inputGate?.SetBlocked(this, value);
            if (!value) settings?.Save();
        }

        private async void Quit()
        {
            if (quitting) return;
            quitting = true;
            settings?.Save();
            try
            {
                if (bootstrap.Running || bootstrap.Busy) await bootstrap.Disconnect();
            }
            finally
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void OnDisable() => SetSettingsOpen(false);
        private void OnGUI()
        {
            if (Application.isBatchMode) return;
            var match = GetComponent<NetworkFortressSpawner>()?.Match;
            bool valid = match != null && match.Object != null && match.Object.IsValid;
            bool inMatch = bootstrap.Running && valid && match.State != MatchPhase.Lobby
                && bootstrap.ConnectionState == NetworkConnectionState.Connected;
            if (inMatch && !settingsOpen)
            {
                var camera = Camera.main;
                if (camera != null && camera.TryGetComponent<HeroesOfDragonland.World.BattlefieldCamera>(out var battlefield) && battlefield.IsCaptured)
                    return;
                GUILayout.BeginArea(new Rect(Screen.width - 224, 24, 208, 164), GUI.skin.box);
                if (match.CanPlay) GUILayout.Label("Press Enter to resume");
                if (settings != null && GUILayout.Button("Settings")) SetSettingsOpen(true);
                if (GUILayout.Button("Leave match") && !bootstrap.Busy) _ = bootstrap.Disconnect();
                if (GUILayout.Button("Quit game")) Quit();
                GUILayout.EndArea();
                return;
            }
            float width = Mathf.Min(520, Mathf.Max(1, Screen.width - 24));
            var bounds = new Rect((Screen.width - width) / 2, 24, width, Mathf.Min(620, Mathf.Max(1, Screen.height - 48)));
            var tint = GUI.color;
            GUI.color = new Color(.04f, .06f, .08f, .97f);
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(bounds, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            if (settingsOpen)
            {
                GUILayout.Label("Settings");
                settings.Draw();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (diagnostics != null)
                    diagnostics.SetRecording(GUILayout.Toggle(diagnostics.Recording, "Record playtest diagnostics"));
#endif
                if (GUILayout.Button("Back")) SetSettingsOpen(false);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label(bootstrap.Running ? "Multiplayer lobby" : "Heroes of Dragonland — Playtest Alpha");
            messageStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(NetworkFailureMessages.Title(bootstrap.ConnectionState));
            GUILayout.Label(bootstrap.NetworkMessage, messageStyle);
            if (!string.IsNullOrEmpty(bootstrap.RequestNotice)) GUILayout.Label(bootstrap.RequestNotice, messageStyle);
            if (bootstrap.ConnectionState == NetworkConnectionState.Reconnecting || bootstrap.ConnectionState == NetworkConnectionState.Interrupted || bootstrap.Busy)
            {
                if (GUILayout.Button("Cancel connection / Leave")) _ = bootstrap.Disconnect();
            }
            bool previous = GUI.enabled;
            GUI.enabled = !bootstrap.Busy;
            if (!bootstrap.Running)
            {
                GUILayout.Label("Lobby name / share this name with your friends");
                room = GUILayout.TextField(room, 64);
                if (bootstrap.CanReconnect && GUILayout.Button("Reconnect to previous match")) _ = bootstrap.Reconnect();
                if (GUILayout.Button("Create lobby")) _ = bootstrap.StartSession(GameMode.Host, room);
                if (GUILayout.Button("Join lobby")) _ = bootstrap.StartSession(GameMode.Client, room);
            }
            else
            {
                GUILayout.Label("Lobby: " + bootstrap.RoomName + " / maximum 6 players");
                if (valid)
                {
                    foreach (var team in new[] { HeroesOfDragonland.Teams.TeamId.TeamA, HeroesOfDragonland.Teams.TeamId.TeamB })
                    {
                        GUILayout.Label(team == HeroesOfDragonland.Teams.TeamId.TeamA ? "TEAM A" : "TEAM B");
                        int count = 0;
                        foreach (var slot in match.LobbyPlayers)
                        {
                            if (slot.Team != team) continue;
                            count++;
                            GUILayout.Label((slot.IsBot ? "Bot" : slot.Player.ToString())
                                + (slot.Player == bootstrap.Runner.LocalPlayer && !slot.IsBot ? " (you)" : "")
                                + " / " + (slot.Ready ? "Ready" : "Not Ready")
                                + (slot.HasAvatar ? "" : " / preparing avatar"));
                        }
                        for (; count < 3; count++) GUILayout.Label("Open slot");
                    }
                    if (GUILayout.Button(match.LocalReady ? "Set Not Ready" : "Set Ready")) match.RPC_SetReady(!match.LocalReady);
                    if (match.IsServerOwner)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        bool shortMatch = GUILayout.Toggle(match.AllowShortMatch, "Allow short match (ready players on both teams)");
                        if (shortMatch != match.AllowShortMatch) match.SetShortMatch(shortMatch);
                        var bots = GetComponent<DeveloperBotService>();
                        if (bots != null)
                        {
                            int humans = 0;
                            foreach (var player in bootstrap.Runner.ActivePlayers) humans++;
                            if (GUILayout.Button("Fill empty slots with bots")) bots.SetBotCount(Mathf.Clamp(6 - humans, 0, 5));
                            if (GUILayout.Button("Remove bots")) bots.SetBotCount(0);
                        }
#endif
                        GUILayout.Label("All humans must be ready. Both teams must have an avatar.");
                        GUI.enabled = !bootstrap.Busy && match.CanStartLobby;
                        if (GUILayout.Button("Start match")) match.TryStartLobby();
                        GUI.enabled = !bootstrap.Busy;
                        if (match.PreparationError.ToString().Length > 0 && GUILayout.Button("Retry preparation")) match.RetryPreparation();
                    }
                    else GUILayout.Label("Waiting for host to start.");
                    if (match.PreparationError.ToString().Length > 0) GUILayout.Label(match.PreparationError.ToString());
                }
                else GUILayout.Label("Preparing lobby...");
                if (GUILayout.Button("Leave lobby")) _ = bootstrap.Disconnect();
            }
            GUI.enabled = previous;
            if (settings != null && GUILayout.Button("Settings")) SetSettingsOpen(true);
            if (GUILayout.Button("Quit game")) Quit();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
