using System;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    internal sealed class DragonlandMenuView
    {
        internal sealed class Actions
        {
            internal Action PlayUnranked, PlayRanked, CreatePrivate, Reconnect, Cancel;
            internal Action Training, SkipTraining, Profile, Settings, Results, Quit;
            internal Action<string> JoinPrivate, CreateCustom, JoinCustom;
        }

        private readonly VisualElement screen, home, play, choices;
        private readonly Label status;
        private readonly Button playButton, training, skip, reconnect, cancel, profile, settings, results;
        private readonly Actions actions;
        private readonly Button[] tabs = new Button[4];
        private TextField roomCode, customRoom;
        private Label rating;
        private string rememberedCode = "", rememberedRoom;
        private int mode;
        private bool showingPlay;

        internal DragonlandMenuView(VisualElement root, Actions actions, string initialRoom)
        {
            this.actions = actions;
            rememberedRoom = initialRoom;
            var styles = Resources.Load<StyleSheet>("Dragonland/FrontEnd");
            screen = Element("dl-menu");
            if (styles != null) screen.styleSheets.Add(styles);
            var art = Element("dl-menu-art");
            var texture = Resources.Load<Texture2D>("Dragonland/TitleValley");
            if (texture != null) art.style.backgroundImage = new StyleBackground(texture);
            art.pickingMode = PickingMode.Ignore;
            screen.Add(art);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("dl-menu-scroll");
            screen.Add(scroll);
            var frame = Element("dl-menu-frame");
            scroll.Add(frame);

            var crest = Element("dl-menu-crest");
            crest.Add(HudMetalwork.Crown());
            crest.Add(Text("HEROES OF", "dl-menu-overline"));
            frame.Add(crest);
            frame.Add(Text("DRAGONLAND", "dl-menu-title"));
            frame.Add(Text("REBORN", "dl-menu-subtitle"));

            home = Element("dl-menu-home");
            HudMetalwork.Plate(home);
            frame.Add(home);
            playButton = AddButton(home, "Play", () => ShowPlay(true), true);
            var trainingRow = Element("dl-menu-training");
            home.Add(trainingRow);
            training = AddButton(trainingRow, "Training grounds", actions.Training);
            training.AddToClassList("dl-menu-grow");
            skip = AddButton(trainingRow, "Skip", actions.SkipTraining);
            skip.tooltip = "Skip training";
            skip.AddToClassList("dl-menu-quiet");
            results = AddButton(home, "Match results", actions.Results);
            var tools = Element("dl-menu-tools");
            home.Add(tools);
            profile = ToolButton(tools, "Profile", actions.Profile, MenuIcon.Profile);
            settings = ToolButton(tools, "Settings", actions.Settings, MenuIcon.Settings);
            ToolButton(tools, "Quit", actions.Quit, MenuIcon.Quit);

            play = Element("dl-menu-play");
            HudMetalwork.Plate(play);
            frame.Add(play);
            var tabRow = Element("dl-menu-tabs");
            play.Add(tabRow);
            string[] names = { "Unranked", "Ranked", "Private", "Custom" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                tabs[i] = AddButton(tabRow, names[i], () => SelectMode(index, true));
                tabs[i].AddToClassList("dl-menu-tab");
            }
            choices = Element("dl-menu-choices");
            play.Add(choices);
            AddButton(play, "Back", () => ShowPlay(false)).AddToClassList("dl-menu-quiet");
            reconnect = AddButton(frame, "Reconnect to previous match", actions.Reconnect);
            cancel = AddButton(frame, "Cancel connection", actions.Cancel);
            status = Text("", "dl-menu-status");
            frame.Add(status);

            screen.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || !showingPlay || cancel.style.display.value != DisplayStyle.None) return;
                ShowPlay(false);
                evt.StopPropagation();
            });
            SelectMode(0);
            ShowPlay(false, false);
            Display(screen, false);
            root.Add(screen);
        }

        internal void Hide() => Display(screen, false);

        internal void Refresh(bool busy, bool canCancel, bool canReconnect, bool trainingAvailable, bool trainingComplete,
            bool recommendTraining, bool hasSettings, bool hasResults, string displayName, string connectionStatus, RankedPlayerData ranked)
        {
            bool wasVisible = screen.style.display.value != DisplayStyle.None;
            Display(screen, true);
            if (!wasVisible) (showingPlay ? tabs[mode] : playButton).schedule.Execute(() =>
            {
                if (screen.style.display.value != DisplayStyle.None) (showingPlay ? tabs[mode] : playButton).Focus();
            });
            Display(training, trainingAvailable);
            Display(skip, trainingAvailable && recommendTraining);
            Display(settings, hasSettings);
            Display(results, hasResults);
            Display(reconnect, canReconnect);
            Display(cancel, canCancel);
            SetText(training, trainingComplete ? "Replay training" : "Training grounds");
            if (profile.tooltip != displayName) profile.tooltip = displayName;
            if (status.text != connectionStatus) status.text = connectionStatus;
            Display(status, !string.IsNullOrEmpty(connectionStatus));
            playButton.SetEnabled(!busy);
            training.SetEnabled(!busy);
            profile.SetEnabled(!busy);
            skip.SetEnabled(!busy);
            choices.SetEnabled(!busy);
            if (rating != null)
            {
                string value = $"Rating {ranked.Rating}" + (ranked.Provisional ? " · Provisional" : "");
                if (rating.text != value) rating.text = value;
            }
        }

        private void ShowPlay(bool value, bool focus = true)
        {
            showingPlay = value;
            Display(home, !value);
            Display(play, value);
            if (focus) (value ? tabs[mode] : playButton).Focus();
        }

        private void SelectMode(int index, bool focus = false)
        {
            if (roomCode != null) rememberedCode = roomCode.value;
            if (customRoom != null) rememberedRoom = customRoom.value;
            roomCode = customRoom = null;
            rating = null;
            mode = index;
            choices.Clear();
            for (int i = 0; i < tabs.Length; i++) tabs[i].EnableInClassList("dl-menu-selected", i == mode);
            switch (mode)
            {
                case 0:
                    AddButton(choices, "Find match · 3v3", actions.PlayUnranked, true);
                    break;
                case 1:
                    rating = Text("", "dl-menu-note");
                    choices.Add(rating);
                    var rankedButton = AddButton(choices, "Find ranked match · 3v3", actions.PlayRanked, true);
                    rankedButton.tooltip = "Requires six human players";
                    break;
                case 2:
                    AddButton(choices, "Create private match", actions.CreatePrivate, true);
                    roomCode = AddField("Room code", rememberedCode, 18);
                    AddButton(choices, "Join with code", () => actions.JoinPrivate(roomCode.value));
                    break;
                case 3:
                    customRoom = AddField("Lobby name", rememberedRoom, 48);
                    AddButton(choices, "Create custom lobby", () => actions.CreateCustom(customRoom.value), true);
                    AddButton(choices, "Join custom lobby", () => actions.JoinCustom(customRoom.value));
                    break;
            }
            if (focus) tabs[mode].Focus();
        }

        private TextField AddField(string label, string value, int maxLength)
        {
            var field = new TextField(label) { value = value, maxLength = maxLength };
            field.AddToClassList("dl-menu-field");
            choices.Add(field);
            return field;
        }

        private static VisualElement Element(string style)
        {
            var element = new VisualElement();
            element.AddToClassList(style);
            return element;
        }

        private static Label Text(string value, string style)
        {
            var label = new Label(value) { enableRichText = false };
            label.AddToClassList(style);
            return label;
        }

        private static Button AddButton(VisualElement parent, string text, Action action, bool primary = false)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("dl-menu-button");
            if (primary) button.AddToClassList("dl-menu-primary");
            parent.Add(button);
            return button;
        }

        private enum MenuIcon { Profile, Settings, Quit }

        private static Button ToolButton(VisualElement parent, string label, Action action, MenuIcon icon)
        {
            var button = AddButton(parent, label, action);
            button.AddToClassList("dl-menu-tool");
            var mark = Element("dl-menu-icon");
            mark.pickingMode = PickingMode.Ignore;
            mark.generateVisualContent += context =>
            {
                var pen = context.painter2D;
                pen.lineWidth = 1.5f;
                pen.strokeColor = HudMetalwork.Brass;
                pen.BeginPath();
                void Line(float x, float y, float endX, float endY)
                {
                    pen.MoveTo(new Vector2(x, y));
                    pen.LineTo(new Vector2(endX, endY));
                }
                switch (icon)
                {
                    case MenuIcon.Profile:
                        Line(9, 2, 13, 6); pen.LineTo(new Vector2(9, 11)); pen.LineTo(new Vector2(5, 6)); pen.ClosePath();
                        Line(2, 16, 6, 13); pen.LineTo(new Vector2(12, 13)); pen.LineTo(new Vector2(16, 16));
                        break;
                    case MenuIcon.Settings:
                        Line(2, 5, 16, 5); Line(6, 2, 6, 8);
                        Line(2, 13, 16, 13); Line(12, 10, 12, 16);
                        break;
                    case MenuIcon.Quit:
                        Line(8, 2, 3, 2); pen.LineTo(new Vector2(3, 16)); pen.LineTo(new Vector2(8, 16));
                        Line(7, 9, 16, 9); Line(12, 5, 16, 9); pen.LineTo(new Vector2(12, 13));
                        break;
                }
                pen.Stroke();
            };
            button.Add(mark);
            return button;
        }

        private static void SetText(Button button, string value)
        {
            if (button.text != value) button.text = value;
        }

        private static void Display(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
