using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.UI
{
    // Presentation only. No gameplay/network reads or mutations belong here.
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHudView : MonoBehaviour
    {
        private VisualElement bossPanel;
        private Label bossName, bossStatus, bossMove;
        private HudMeter bossHealth;
        private bool locked, bossIsTarget;
        public bool HasBoss => bossPanel != null && bossPanel.style.display.value != DisplayStyle.None;
        public float BossHealthValue => bossHealth?.Fraction ?? 0;
        private VisualElement root, personal, matchPanel, targetPanel, marker, notice, context;
        private Label identity, phase, objective, matchNotice, respawn, result, targetName, pacing;
        private Label recapHeadline, recapRecent, recapAssists;
        private Label quickItem;
        private VisualElement details;
        private Label vitalsReadout, healthNumber, helpKey;
        private Label secondaryObjectives, staminaWarning, kingWarning, prompt, feedback, respawnDetails;
        public bool HasObjectiveSummary => details != null && details.style.display.value == DisplayStyle.Flex
            && secondaryObjectives.style.display.value == DisplayStyle.Flex;
        public string FeedbackText => feedback?.text;
        public string KingWarningText => kingWarning?.text;
        public string StaminaWarningText => staminaWarning?.text;
        private HudMeter health, stamina, mana, xp, targetHealth, kingA, kingB;
        public bool HasLocalPlayer => personal != null && personal.style.display.value != DisplayStyle.None;
        public string ResultText => result?.text;
        public string RespawnText => respawn?.text;
        public float HealthValue => health?.Fraction ?? 0;
        public float StaminaValue => stamina?.Fraction ?? 0;
        public float ManaValue => mana?.Fraction ?? 0;
        public bool HasTarget => locked;
        public float TargetHealthValue => targetHealth?.Fraction ?? 0;
        public float ExperienceValue => xp?.Fraction ?? 0;
        public string IdentityText => identity?.text;
        public string ObjectiveText => objective?.text;
        public float KingAValue => kingA?.Fraction ?? 0;
        public float KingBValue => kingB?.Fraction ?? 0;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.color = new Color(.96f, .93f, .86f);
            root.style.fontSize = 14;
            personal = Panel("Player", 22, null, null, 22, 228);
            var playerHeader = new VisualElement();
            playerHeader.style.flexDirection = FlexDirection.Row;
            playerHeader.style.justifyContent = Justify.SpaceBetween;
            personal.Add(playerHeader);
            identity = Label(playerHeader, "");
            Heading(identity);
            healthNumber = Label(playerHeader, "");
            health = new HudMeter(personal, new Color32(171, 53, 66, 255), 12);
            stamina = new HudMeter(personal, new Color32(106, 155, 109, 255), 8);
            staminaWarning = Label(personal, "");
            mana = new HudMeter(personal, new Color32(81, 139, 177, 255), 7);
            xp = new HudMeter(personal, HudMetalwork.Brass, 4);
            quickItem = Label(personal, "");
            quickItem.name = "QuickItem";
            quickItem.style.marginTop = 7;
            quickItem.style.color = new Color(.9f, .76f, .55f);
            SetQuickItem(0, 0, null, 0);
            var match = Panel("Match", null, 14, null, null, 234);
            matchPanel = match;
            Center(match);
            phase = Label(match, "Not connected");
            Heading(phase);
            phase.style.unityTextAlign = TextAnchor.MiddleCenter;
            var kings = new VisualElement();
            kings.style.flexDirection = FlexDirection.Row;
            kings.style.alignItems = Align.Center;
            match.Add(kings);
            kingA = new HudMeter(kings, new Color32(87, 142, 180, 255), 8);
            kings.Add(HudMetalwork.Crown());
            kingB = new HudMeter(kings, new Color32(174, 67, 71, 255), 8);
            kingA.style.flexBasis = kingB.style.flexBasis = 0;
            kingA.style.flexGrow = kingB.style.flexGrow = 1;
            matchNotice = Label(match, "");
            details = Panel("HudDetails", null, 22, 22, null, 308);
            objective = Label(details, "");
            Heading(objective);
            vitalsReadout = Label(details, "");
            secondaryObjectives = Label(details, "Clear camps for equipment. Contest the boss and breach the fortress.");
            secondaryObjectives.style.fontSize = 12;
            secondaryObjectives.style.marginTop = 5;
            secondaryObjectives.style.color = new Color(.8f, .82f, .77f);
            secondaryObjectives.style.display = DisplayStyle.None;
            kingWarning = Label(match, "");
            pacing = Label(details, "");
            pacing.name = "MatchPacing";
            Label(details, "WASD Move · Shift Dodge · MMB Lock · Q/E Switch\nT / Y / U Ping · [ / ] Slot · G Use\nI Inventory · K Skills · Esc Menu");
            Label(details, "Map: ▲ You · ● Ally · □ Fort · K King · ◇ Camp · ◆ Boss · + Objective\n! Revealed · x Cleared · ? Unknown · – Unavailable");
            details.style.display = DisplayStyle.None;
            bossPanel = Panel("Boss", null, 80, null, null, 282);
            Center(bossPanel);
            match.RegisterCallback<GeometryChangedEvent>(evt => bossPanel.style.top = evt.newRect.yMax + 10);
            bossName = Label(bossPanel, "");
            Heading(bossName);
            bossName.style.unityTextAlign = TextAnchor.MiddleCenter;
            bossHealth = new HudMeter(bossPanel, new Color32(154, 65, 96, 255), 9);
            bossStatus = Label(bossPanel, ""); bossMove = Label(bossPanel, "");
            SetBoss(false);
            targetPanel = Panel("Target", null, null, null, 28, 176);
            Center(targetPanel);
            targetName = Label(targetPanel, "TARGET LOCKED");
            Heading(targetName);
            targetName.style.unityTextAlign = TextAnchor.MiddleCenter;
            targetHealth = new HudMeter(targetPanel, new Color32(180, 89, 65, 255), 7);
            notice = Panel("Notice", null, null, null, null, 460);
            Center(notice);
            notice.style.top = Length.Percent(40);
            notice.style.unityTextAlign = TextAnchor.MiddleCenter;
            respawn = Label(notice, "");
            respawn.style.fontSize = 21;
            respawn.style.unityFontStyleAndWeight = FontStyle.Bold;
            respawnDetails = Label(notice, "");
            recapHeadline = Label(notice, "");
            recapRecent = Label(notice, "");
            recapAssists = Label(notice, "");
            recapHeadline.enableRichText = recapRecent.enableRichText = recapAssists.enableRichText = false;
            recapRecent.style.whiteSpace = WhiteSpace.Normal;
            result = Label(notice, "");
            result.style.fontSize = 26;
            result.style.unityFontStyleAndWeight = FontStyle.Bold;
            context = Panel("Context", null, null, null, 93, 280);
            context.style.width = StyleKeyword.Auto;
            Center(context);
            context.style.unityTextAlign = TextAnchor.MiddleCenter;
            feedback = Label(context, "");
            prompt = Label(context, "");
            prompt.style.unityFontStyleAndWeight = FontStyle.Bold;
            helpKey = Label(root, "H · HUD");
            helpKey.name = "Controls";
            helpKey.style.position = Position.Absolute; helpKey.style.right = 22; helpKey.style.bottom = 63;
            helpKey.style.fontSize = 11;
            helpKey.style.backgroundColor = HudMetalwork.Ink;
            helpKey.style.paddingLeft = helpKey.style.paddingRight = 6;
            marker = new Label("◇");
            marker.style.position = Position.Absolute;
            marker.style.fontSize = 36;
            marker.style.color = new Color(1, .85f, .2f);
            marker.style.width = 40; marker.style.height = 44;
            root.Add(marker);
            IgnorePicking(root);
            SetLocal(false); SetTarget(false); SetKing(true, 0, 0); SetKing(false, 0, 0);
            SetMatch("Not connected", "Join a match to see objectives.", "");
            SetMatchPanelVisible(false);
            SetPacing("");
            SetContext("", "", false); SetKingWarning(false); SetStaminaWarning(false);
        }

        private static void Center(VisualElement element)
        {
            element.style.left = Length.Percent(50);
            element.style.translate = new Translate(Length.Percent(-50), 0);
        }

        private static void Heading(Label label)
        {
            label.style.fontSize = 15;
            label.style.color = new Color(.92f, .8f, .61f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        internal static void StyleSurface(VisualElement panel)
        {
            HudMetalwork.Plate(panel);
        }

        private VisualElement Panel(string name, float? left, float? top, float? right, float? bottom, float width)
        {
            var panel = new VisualElement { name = name };
            panel.style.position = Position.Absolute;
            if (left.HasValue) panel.style.left = left.Value;
            if (top.HasValue) panel.style.top = top.Value;
            if (right.HasValue) panel.style.right = right.Value;
            if (bottom.HasValue) panel.style.bottom = bottom.Value;
            panel.style.width = width;
            panel.style.maxWidth = Length.Percent(44);
            panel.style.paddingLeft = panel.style.paddingRight = 12;
            panel.style.paddingTop = panel.style.paddingBottom = 6;
            StyleSurface(panel);
            root.Add(panel);
            return panel;
        }
        private static Label Label(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 2;
            parent.Add(label);
            return label;
        }
        private void Update()
        {
            bool show = HasLocalPlayer && Application.isFocused && Keyboard.current?.hKey.isPressed == true
                && UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            details.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) vitalsReadout.text = $"{health.Readout}\n{stamina.Readout}\n{mana.Readout}\n{xp.Readout}\n{kingA.Readout} · {kingB.Readout}";
        }
        private static void IgnorePicking(VisualElement element)
        {
            element.pickingMode = PickingMode.Ignore;
            foreach (var child in element.Children()) IgnorePicking(child);
        }
        private static void Value(HudMeter bar, string title, float value, float maximum)
        {
            bar.Set(value, maximum, maximum > 0 ? $"{title}  {Mathf.CeilToInt(value)}/{Mathf.CeilToInt(maximum)}" : title + " —");
        }
        public void SetBoss(bool visible, string name = "", string status = "", float current = 0, float maximum = 0, string move = "", bool isTarget = false)
        {
            if (bossPanel == null) return;
            bossPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            bossIsTarget = visible && isTarget;
            UpdateTargetPanel();
            bossName.text = name;
            SetHint(bossStatus, status, HudMetalwork.Brass);
            SetHint(bossMove, move, Color.white);
            Value(bossHealth, "Health", current, maximum);
        }
        public void SetLocal(bool visible)
        {
            personal.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            helpKey.style.display = personal.style.display;
            if (visible) return;
            SetRespawn("");
            SetDeathRecap();
        }
        public void SetIdentity(string text) => identity.text = text;
        public void SetQuickItem(int slotCount, int index, string itemName, long quantity)
        {
            if (quickItem == null) return;
            quickItem.style.display = slotCount > 0 && quantity > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            quickItem.text = $"[G]  {itemName}  ×{quantity}";
        }
        public void SetHealth(float value, float maximum)
        {
            Value(health, "Health", value, maximum);
            healthNumber.text = $"{Mathf.CeilToInt(value)} / {Mathf.CeilToInt(maximum)}";
        }
        public void SetStamina(float value, float maximum) => Value(stamina, "Stamina", value, maximum);
        public void SetMana(float value, float maximum) => Value(mana, "Mana", value, maximum);
        public void SetExperience(long value, int required, bool maximum)
        {
            xp.Set(maximum ? 1 : value, maximum ? 1 : required, maximum ? "XP  MAX" : $"XP  {value}/{required}");
        }
        public void SetTarget(bool visible)
        {
            locked = visible;
            UpdateTargetPanel();
            marker.style.display = DisplayStyle.None;
        }
        private void UpdateTargetPanel()
        {
            if (targetPanel != null) targetPanel.style.display = locked && !bossIsTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public void SetTargetName(string value) => targetName.text = value;
        public void SetTargetHealth(float value, float maximum) => Value(targetHealth, "Health", value, maximum);
        public void SetKing(bool teamA, float value, float maximum) => Value(teamA ? kingA : kingB, "Team " + (teamA ? "A" : "B") + " King" + (maximum > 0 && value <= 0 ? " · DEFEATED" : ""), value, maximum);
        public void SetMatch(string state, string goal, string outcome, bool announceGoal = false)
        {
            phase.text = state;
            objective.text = goal;
            SetHint(matchNotice, announceGoal ? goal : "", Color.white);
            SetHint(result, outcome, new Color(.96f, .82f, .57f));
            RefreshNotice();
        }
        internal void SetMatchPanelVisible(bool visible) =>
            matchPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        public void SetDeathRecap(string headline = "", string recent = "", string assists = "")
        {
            SetHint(recapHeadline, headline, Color.white);
            SetHint(recapRecent, recent, Color.white);
            SetHint(recapAssists, assists, Color.white);
            RefreshNotice();
        }

        public void SetRespawn(string value, string details = "")
        {
            SetHint(respawn, value, Color.white);
            SetHint(respawnDetails, details, Color.white);
            RefreshNotice();
        }

        private void RefreshNotice()
        {
            notice.style.display = string.IsNullOrEmpty(respawn.text) && string.IsNullOrEmpty(result.text)
                && string.IsNullOrEmpty(respawnDetails.text) && string.IsNullOrEmpty(recapHeadline.text)
                && string.IsNullOrEmpty(recapRecent.text) && string.IsNullOrEmpty(recapAssists.text)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
        internal void SetObjectives(bool visible) => secondaryObjectives.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        internal void SetStaminaWarning(bool active) => SetHint(staminaWarning, active ? "Low stamina" : "", new Color(1, .77f, .34f));
        internal void SetKingWarning(bool active) => SetHint(kingWarning, active ? "Your King is under attack" : "", new Color(1, .42f, .32f));
        internal void SetContext(string message, string interaction, bool warning)
        {
            SetHint(feedback, message, warning ? new Color(1, .77f, .34f) : new Color(.91f, 1, .93f));
            SetHint(prompt, interaction, Color.white);
            context.style.display = message.Length == 0 && interaction.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
        private static void SetHint(Label label, string text, Color color)
        {
            text ??= "";
            if (label.text == text && label.style.color.value == color && label.style.display.value == (text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex)) return;
            label.text = text;
            label.style.color = color;
            label.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public string PacingText => pacing?.text;
        public void SetPacing(string value) { if (pacing != null) SetHint(pacing, value, new Color(.84f, .86f, .8f)); }
        public void PlaceTarget(Camera camera, Vector3 position)
        {
            if (camera == null || root.panel == null) { marker.style.display = DisplayStyle.None; return; }
            Vector3 point = camera.WorldToScreenPoint(position);
            bool visible = point.z > 0 && point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height;
            marker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(point.x, Screen.height - point.y));
            panelPoint.x = Mathf.Clamp(panelPoint.x, 20, Mathf.Max(20, root.resolvedStyle.width - 20));
            panelPoint.y = Mathf.Clamp(panelPoint.y, 22, Mathf.Max(22, root.resolvedStyle.height - 22));
            marker.style.left = panelPoint.x - 20; marker.style.top = panelPoint.y - 22;
        }
    }
}
