using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    // Presentation only. No gameplay/network reads or mutations belong here.
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHudView : MonoBehaviour
    {
        private VisualElement root, personal, targetPanel, marker;
        private Label identity, phase, objective, respawn, result, targetName;
        private ProgressBar health, stamina, mana, xp, targetHealth, kingA, kingB;
        public bool HasLocalPlayer => personal != null && personal.style.display.value != DisplayStyle.None;
        public string ResultText => result?.text;
        public string RespawnText => respawn?.text;
        public float HealthValue => health?.value ?? 0;
        public float StaminaValue => stamina?.value ?? 0;
        public float ManaValue => mana?.value ?? 0;
        public bool HasTarget => targetPanel != null && targetPanel.style.display.value != DisplayStyle.None;
        public float TargetHealthValue => targetHealth?.value ?? 0;
        public float ExperienceValue => xp?.value ?? 0;
        public string IdentityText => identity?.text;
        public string ObjectiveText => objective?.text;
        public float KingAValue => kingA?.value ?? 0;
        public float KingBValue => kingB?.value ?? 0;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.color = Color.white;
            root.style.fontSize = 16;
            personal = Panel("Player", 18, 18, null, null, 300);
            identity = Label(personal, "");
            health = Bar(personal, "Health", new Color(.75f, .18f, .22f));
            stamina = Bar(personal, "Stamina", new Color(.2f, .65f, .35f));
            mana = Bar(personal, "Mana", new Color(.18f, .42f, .85f));
            xp = Bar(personal, "XP", new Color(.65f, .45f, .85f));
            var match = Panel("Match", null, 18, 18, null, 300);
            phase = Label(match, "Not connected");
            objective = Label(match, "Protect your King. Defeat the enemy King.");
            kingA = Bar(match, "Team A King", new Color(.2f, .5f, .9f));
            kingB = Bar(match, "Team B King", new Color(.85f, .28f, .25f));
            targetPanel = Panel("Target", null, null, null, 86, 360);
            targetPanel.style.left = Length.Percent(50); targetPanel.style.marginLeft = -180;
            targetName = Label(targetPanel, "TARGET LOCKED");
            targetHealth = Bar(targetPanel, "Target", new Color(.85f, .35f, .2f));
            var notice = Panel("Notice", null, null, null, 28, 460);
            notice.style.left = Length.Percent(50);
            notice.style.marginLeft = -230;
            notice.style.backgroundColor = Color.clear;
            notice.style.bottom = StyleKeyword.Auto;
            notice.style.top = Length.Percent(40);
            notice.style.unityTextAlign = TextAnchor.MiddleCenter;
            respawn = Label(notice, "");
            result = Label(notice, "");
            result.style.fontSize = 26;
            result.style.unityFontStyleAndWeight = FontStyle.Bold;
            var controls = Label(root, "WASD move  /  Mouse look  /  MMB lock  /  Shift dodge  /  Esc menu");
            controls.name = "Controls";
            controls.style.position = Position.Absolute; controls.style.left = 18; controls.style.bottom = 18;
            controls.style.width = Length.Percent(55); controls.style.fontSize = 13;
            marker = new Label("◇");
            marker.style.position = Position.Absolute;
            marker.style.fontSize = 36;
            marker.style.color = new Color(1, .85f, .2f);
            marker.style.width = 40; marker.style.height = 44;
            root.Add(marker);
            IgnorePicking(root);
            SetLocal(false); SetTarget(false); SetKing(true, 0, 0); SetKing(false, 0, 0);
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
            panel.style.paddingTop = panel.style.paddingBottom = 10;
            panel.style.backgroundColor = new Color(.035f, .045f, .065f, .88f);
            root.Add(panel);
            return panel;
        }
        private static Label Label(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 5;
            parent.Add(label);
            return label;
        }
        private static ProgressBar Bar(VisualElement parent, string title, Color color)
        {
            var bar = new ProgressBar { title = title, lowValue = 0, highValue = 1 };
            bar.style.height = 20; bar.style.marginTop = 4;
            bar.Q(className: "unity-progress-bar__progress").style.backgroundColor = color;
            parent.Add(bar);
            return bar;
        }
        private static void IgnorePicking(VisualElement element)
        {
            element.pickingMode = PickingMode.Ignore;
            foreach (var child in element.Children()) IgnorePicking(child);
        }
        private static void Value(ProgressBar bar, string title, float value, float maximum)
        {
            bar.value = maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
            bar.title = maximum > 0 ? title + "  " + Mathf.CeilToInt(value) + " / " + Mathf.CeilToInt(maximum) : title + "  —";
        }
        public void SetLocal(bool visible) { personal.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; if (!visible) SetRespawn(""); }
        public void SetIdentity(string text) => identity.text = text;
        public void SetHealth(float value, float maximum) => Value(health, "Health", value, maximum);
        public void SetStamina(float value, float maximum) => Value(stamina, "Stamina", value, maximum);
        public void SetMana(float value, float maximum) => Value(mana, "Mana", value, maximum);
        public void SetExperience(long value, int required, bool maximum)
        {
            xp.value = maximum ? 1 : required > 0 ? Mathf.Clamp01((float)value / required) : 0;
            xp.title = maximum ? "XP  MAX LEVEL" : required > 0 ? "XP  " + value + " / " + required : "XP  —";
        }
        public void SetTarget(bool visible) { targetPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; marker.style.display = DisplayStyle.None; }
        public void SetTargetName(string value) => targetName.text = "LOCKED · " + value;
        public void SetTargetHealth(float value, float maximum) => Value(targetHealth, "Health", value, maximum);
        public void SetKing(bool teamA, float value, float maximum) => Value(teamA ? kingA : kingB, "Team " + (teamA ? "A" : "B") + " King" + (maximum > 0 && value <= 0 ? " · DEFEATED" : ""), value, maximum);
        public void SetMatch(string state, string goal, string outcome) { phase.text = state; objective.text = goal; result.text = outcome; }
        public void SetRespawn(string value) => respawn.text = value;
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
