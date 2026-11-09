using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class SessionSkillsPanel : MonoBehaviour
    {
        [SerializeField] private MultiplayerBootstrap bootstrap;
        private VisualElement window;
        private Button toggle;
        private Label points, status;
        private readonly Label[] descriptions = new Label[SessionSkills.SkillCount];
        private readonly Button[] learn = new Button[SessionSkills.SkillCount], use = new Button[SessionSkills.SkillCount];
        private NetworkSessionSkills network;
        private SessionSkills skills;
        private LocalGameplayInputGate gate;
        private string feedback = "";
        private bool open, pending;
        private float nextRefresh, requestAt;
        private void OnEnable()
        {
            gate = bootstrap != null ? bootstrap.GetComponent<LocalGameplayInputGate>() : null;
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1; root.style.alignItems = Align.Center; root.style.justifyContent = Justify.Center;
            root.style.color = Color.white; root.style.fontSize = 15;
            toggle = new Button(() => SetOpen(!open)) { text = "Skills [K]" };
            toggle.style.position = Position.Absolute; toggle.style.right = 190; toggle.style.bottom = 22;
            toggle.style.width = 130; toggle.style.height = 34; root.Add(toggle);
            window = new VisualElement { name = "SessionSkillsWindow" };
            window.style.width = Length.Percent(88); window.style.maxWidth = 820;
            window.style.maxHeight = Length.Percent(84);
            window.style.backgroundColor = new Color(.035f, .045f, .065f, .98f);
            window.style.paddingLeft = window.style.paddingRight = 16;
            window.style.paddingTop = window.style.paddingBottom = 12; root.Add(window);
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row;
            points = new Label(); points.style.flexGrow = 1; points.style.fontSize = 20; header.Add(points);
            header.Add(new Button(() => SetOpen(false)) { text = "Close [K / Esc]" }); window.Add(header);
            window.Add(new Label("Skills and skill points reset each session. Requirements use base + allocated attributes."));
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; window.Add(scroll);
            for (int i = 0; i < SessionSkills.SkillCount; i++)
            {
                int index = i;
                var card = new VisualElement(); card.style.marginTop = 12; card.style.paddingBottom = 8;
                descriptions[i] = new Label(); descriptions[i].style.whiteSpace = WhiteSpace.Normal; card.Add(descriptions[i]);
                var buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row;
                learn[i] = new Button(() => Request(index, true)) { text = "Learn" };
                use[i] = new Button(() => Request(index, false)) { text = "Use" };
                buttons.Add(learn[i]); buttons.Add(use[i]); card.Add(buttons); scroll.Add(card);
            }
            status = new Label(); status.style.whiteSpace = WhiteSpace.Normal; status.style.marginTop = 8; window.Add(status);
            SetOpen(false); toggle.style.display = DisplayStyle.None;
        }
        private void OnDisable() { SetOpen(false); Bind(null); }
        private void Update()
        {
            if (!Application.isFocused || Keyboard.current == null) return;
            if (Keyboard.current.kKey.wasPressedThisFrame) SetOpen(!open);
            else if (open && Keyboard.current.escapeKey.wasPressedThisFrame) SetOpen(false);
        }
        private void LateUpdate()
        {
            NetworkSessionSkills found = null;
            var runner = bootstrap != null ? bootstrap.Runner : null;
            if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var actor) && actor != null && actor.IsValid && actor.HasInputAuthority)
                found = actor.GetComponent<NetworkSessionSkills>();
            if (!ReferenceEquals(found, network)) Bind(found);
            if (pending && Time.unscaledTime - requestAt > 4) { pending = false; feedback = "No response yet. Check skill state before retrying."; }
            if (open && Time.unscaledTime >= nextRefresh) { Refresh(); nextRefresh = Time.unscaledTime + .1f; }
        }
        private void Bind(NetworkSessionSkills value)
        {
            SetOpen(false);
            if (network != null) network.RequestResolved -= OnResolved;
            network = value; skills = value != null ? value.GetComponent<SessionSkills>() : null;
            if (network != null) network.RequestResolved += OnResolved;
            pending = false; feedback = "";
            if (toggle != null) toggle.style.display = skills != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private void SetOpen(bool value)
        {
            open = value && skills != null && gate != null;
            if (window != null) window.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (gate != null) gate.SetBlocked(this, open);
            if (open) Refresh();
        }
        private void Refresh()
        {
            if (skills == null) return;
            points.text = "SESSION SKILLS — Points: " + skills.SkillPoints;
            bool available = !pending && network.CanRequest;
            for (int i = 0; i < SessionSkills.SkillCount; i++)
            {
                var definition = skills.GetDefinition(i);
                if (definition == null) { descriptions[i].text = "Unconfigured skill"; learn[i].SetEnabled(false); use[i].SetEnabled(false); continue; }
                bool canLearn = skills.CanLearn(i, out string learnReason), canUse = skills.CanActivate(i, out string useReason);
                string requirements = "Level " + definition.RequiredLevel;
                foreach (var requirement in definition.Requirements) requirements += ", " + requirement.Stat + " " + requirement.MinimumValue;
                descriptions[i].text = definition.DisplayName + " | " + definition.Kind + (skills.IsLearned(i) ? " | Learned" : "")
                    + "\n" + definition.Description + "\nRequires: " + requirements + " | Points: " + definition.PointCost
                    + (definition.Kind == SkillKind.Active ? " | Mana: " + definition.ManaCost + " | Cooldown: " + definition.Cooldown + "s" : "")
                    + (skills.CooldownRemaining(i) > 0 ? "\nReady in " + skills.CooldownRemaining(i).ToString("0.0") + "s" : "")
                    + "\n" + (skills.IsLearned(i) ? useReason : learnReason);
                learn[i].SetEnabled(available && canLearn); use[i].SetEnabled(available && canUse);
                use[i].style.display = definition.Kind == SkillKind.Active ? DisplayStyle.Flex : DisplayStyle.None;
            }
            status.text = pending ? "Waiting for server..." : !network.CanRequest ? "Skills require an active match." : feedback;
        }
        private void Request(int index, bool learning)
        {
            if (!open || pending || network == null) return;
            pending = true; requestAt = Time.unscaledTime;
            if (!(learning ? network.RequestLearn(index) : network.RequestActivate(index))) OnResolved(false, "Request is unavailable.");
            Refresh();
        }
        private void OnResolved(bool accepted, string reason) { pending = false; feedback = reason; nextRefresh = 0; }
    }
}
