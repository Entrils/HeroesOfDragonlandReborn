using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Profiles;
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
        private BuildPresetPicker presetPicker;
        private readonly Label[] descriptions = new Label[SessionSkills.Capacity], blockers = new Label[SessionSkills.Capacity];
        private readonly Button[] learn = new Button[SessionSkills.Capacity], use = new Button[SessionSkills.Capacity];
        private readonly VisualElement[] cards = new VisualElement[SessionSkills.Capacity];
        private readonly Button[] branches = new Button[4];
        private SkillBranch branch;
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
            toggle = new Button(() => SetOpen(!open)) { text = "K", tooltip = "Skills [K]" };
            HudMetalwork.StyleShortcut(toggle, false);
            root.Add(toggle);
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
            presetPicker = new BuildPresetPicker(LocalPlayerProfile.Current, Refresh);
            window.Add(presetPicker);
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < branches.Length; i++)
            {
                var selected = (SkillBranch)i;
                branches[i] = new Button(() => { branch = selected; Refresh(); });
                tabs.Add(branches[i]);
            }
            window.Add(tabs);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; window.Add(scroll);
            for (int i = 0; i < SessionSkills.Capacity; i++)
            {
                int index = i;
                var card = cards[i] = new VisualElement(); card.style.marginTop = 12; card.style.paddingBottom = 8;
                descriptions[i] = new Label(); descriptions[i].style.whiteSpace = WhiteSpace.Normal; card.Add(descriptions[i]);
                blockers[i] = new Label(); blockers[i].style.whiteSpace = WhiteSpace.Normal;
                blockers[i].style.color = new Color(1, .77f, .34f); card.Add(blockers[i]);
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
            var preset = presetPicker.Selected;
            for (int i = 0; i < branches.Length; i++)
            {
                var tab = (SkillBranch)i;
                bool hasPriority = false;
                if (preset != null)
                    for (int index = 0; index < skills.NodeCount && !hasPriority; index++)
                    {
                        var node = skills.GetDefinition(index);
                        hasPriority = node != null && node.Branch == tab && preset.SkillPriority(node, out _) > 0;
                    }
                branches[i].text = hasPriority ? $"{tab} (preset)" : tab.ToString();
                branches[i].SetEnabled(tab != branch);
            }
            for (int i = 0; i < SessionSkills.Capacity; i++)
            {
                var definition = skills.GetDefinition(i);
                bool visible = definition != null && definition.Branch == branch;
                cards[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                bool canLearn = skills.CanLearn(i, out string learnReason), canUse = skills.CanActivate(i, out string useReason);
                int rank = skills.GetRank(i);
                string blocker = rank == definition.MaxRank && definition.Kind == SkillKind.Active ? useReason : learnReason;
                learn[i].text = rank == 0 ? "Learn" : "Rank up";
                learn[i].style.display = rank < definition.MaxRank ? DisplayStyle.Flex : DisplayStyle.None;
                blockers[i].text = blocker;
                blockers[i].style.display = string.IsNullOrEmpty(blocker) ? DisplayStyle.None : DisplayStyle.Flex;
                string requirements = "Level " + definition.RequiredLevel;
                foreach (var requirement in definition.Requirements) requirements += ", " + requirement.Stat + " " + requirement.MinimumValue;
                foreach (var prerequisite in definition.Prerequisites)
                    requirements += $", {prerequisite.Skill.DisplayName} rank {prerequisite.Rank}";
                int targetRank = 0;
                int priority = preset?.SkillPriority(definition, out targetRank) ?? 0;
                string recommendation = priority > 0 ? $" | Preset #{priority}, target rank {targetRank}" : "";
                descriptions[i].text = definition.DisplayName + recommendation + " | " + definition.Kind + " | Rank " + rank + " / " + definition.MaxRank
                    + "\n" + definition.Description + "\nRequires: " + requirements + " | Points: " + definition.PointCost
                    + (definition.Kind == SkillKind.Active ? " | Mana: " + skills.ManaCost(definition).ToString("0.##") + " | Cooldown: " + skills.Cooldown(definition).ToString("0.##") + "s" : "")
                    + (definition.Ability != null ? " | Cast: " + definition.Ability.CastTime + "s | Range: " + definition.Ability.Range + "m" : "")
                    + (skills.CooldownRemaining(i) > 0 ? "\nReady in " + skills.CooldownRemaining(i).ToString("0.0") + "s" : "");
                learn[i].SetEnabled(available && canLearn); use[i].SetEnabled(available && canUse);
                use[i].style.display = definition.Kind == SkillKind.Active ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var caster = skills.GetComponent<HeroesOfDragonland.Abilities.AbilityCaster>();
            string magic = caster != null ? (caster.CastRemaining > 0 ? "Casting: " + caster.CastRemaining.ToString("0.0") + "s. " : "")
                + (caster.ShieldRemaining > 0 ? "Shield: " + caster.ShieldCapacity.ToString("0") + " / " + caster.ShieldRemaining.ToString("0.0") + "s. " : "") : "";
            status.text = magic + (pending ? "Waiting for server..." : !network.CanRequest ? "Skills require an active match." : feedback);
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
