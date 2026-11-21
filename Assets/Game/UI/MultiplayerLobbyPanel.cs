using Fusion;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.UI;
using HeroesOfDragonland.World;
using HeroesOfDragonland.Profiles;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Presentation depends on the session API; future matchmaking can use that same API.
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class MultiplayerLobbyPanel : MonoBehaviour
    {
        [SerializeField] private string room = "dragonland";
        private static readonly string[] OnlineTabs = { "Quick Play", "Private Match", "Custom Lobby" };
        private int onlineTab;
        private string roomCode = "";
        private MultiplayerBootstrap bootstrap;
        private Vector2 scroll;
        private GUIStyle messageStyle;
        private PlaytestSettings settings;
        private LocalGameplayInputGate inputGate;
        private bool settingsOpen, quitting;
        private bool profileOpen;
        private string profileName;
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
            if (profileOpen)
            {
                DrawProfile();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
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
                if (GUILayout.Button("Profile · " + LocalPlayerProfile.Current.Data.DisplayName))
                {
                    profileName = LocalPlayerProfile.Current.Data.DisplayName;
                    profileOpen = true;
                }
                if (Application.CanStreamedLevelBeLoaded(TutorialProgress.ScenePath))
                {
                    if (TutorialProgress.RecommendTutorial) GUILayout.Label("New player? Try the offline training grounds.");
                    if (GUILayout.Button(TutorialProgress.TutorialCompleted ? "Replay training" : "Training grounds · Offline"))
                        TutorialProgress.Open();
                    if (TutorialProgress.RecommendTutorial && GUILayout.Button("Skip training")) TutorialProgress.Skip();
                    GUILayout.Space(8);
                }
                if (bootstrap.CanReconnect && GUILayout.Button("Reconnect to previous match")) _ = bootstrap.Reconnect();
                onlineTab = GUILayout.Toolbar(onlineTab, OnlineTabs);
                switch (onlineTab)
                {
                    case 0:
                        GUILayout.Label("Find or start an open 3v3 lobby.");
                        if (GUILayout.Button("Find match")) _ = bootstrap.QuickPlay();
                        break;
                    case 1:
                        if (GUILayout.Button("Create private match")) _ = bootstrap.CreatePrivateMatch();
                        GUILayout.Label("Room code from the host");
                        roomCode = GUILayout.TextField(roomCode, 18);
                        if (GUILayout.Button("Join with code")) _ = bootstrap.JoinPrivateMatch(roomCode);
                        break;
                    case 2:
                        GUILayout.Label("Custom lobby name");
                        room = GUILayout.TextField(room, 48);
                        if (GUILayout.Button("Create custom lobby")) _ = bootstrap.CustomLobby(room, true);
                        if (GUILayout.Button("Join custom lobby")) _ = bootstrap.CustomLobby(room, false);
                        break;
                }
            }
            else
            {
                string region = string.IsNullOrEmpty(bootstrap.Region) ? "" : " · " + bootstrap.Region.ToUpperInvariant();
                GUILayout.Label(bootstrap.SessionKind == OnlineSessionKind.QuickPlay ? "Quick Play · 3v3" + region
                    : bootstrap.SessionKind == OnlineSessionKind.PrivateMatch ? "Private match" + region
                    : "Custom lobby · " + bootstrap.DisplayRoom + region);
                if (bootstrap.SessionKind == OnlineSessionKind.PrivateMatch)
                {
                    GUILayout.Label("Room code · " + bootstrap.DisplayRoom);
                    if (GUILayout.Button("Copy room code")) GUIUtility.systemCopyBuffer = bootstrap.DisplayRoom;
                }
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

        private void DrawProfile()
        {
            var profile = LocalPlayerProfile.Current;
            var totals = profile.Data.Totals;
            GUILayout.Label("Player profile");
            GUILayout.Label("Display name");
            profileName = GUILayout.TextField(profileName, 28);
            if (GUILayout.Button("Save name"))
            {
                profile.Rename(profileName);
                profileName = profile.Data.DisplayName;
            }
            GUILayout.Label(profile.Data.TutorialCompleted ? "Training completed" : "Training available");
            GUILayout.Label(totals.Matches + " matches · " + totals.Wins + " wins · " + totals.Losses + " losses");
            GUILayout.Label(totals.Kills + " kills · " + totals.Deaths + " deaths");
            GUILayout.Label(totals.CampsCleared + " camps cleared · " + totals.BossesKilled + " bosses killed");
            GUILayout.Label(totals.KingsKilled + " Kings killed");
            if (GUILayout.Button("Back")) profileOpen = false;
        }
    }
}
