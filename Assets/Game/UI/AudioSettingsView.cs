using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class AudioSettingsView : MonoBehaviour
    {
        [SerializeField] private AudioPreferences preferences;
        [SerializeField] private GameAudioService audioService;
        [SerializeField] private LocalGameplayInputGate inputGate;
        private VisualElement root, panel;
        private readonly Slider[] sliders = new Slider[3];
        private bool open;
        public bool IsOpen => open;
        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.pickingMode = PickingMode.Ignore;
            var button = new Button(() => SetOpen(!open)) { text = "Audio [F10]" };
            button.style.position = Position.Absolute; button.style.left = 18; button.style.bottom = 58;
            root.Add(button);
            panel = new VisualElement(); panel.name = "AudioSettings";
            panel.style.position = Position.Absolute; panel.style.left = 18; panel.style.bottom = 98;
            panel.style.width = 300; panel.style.maxWidth = Length.Percent(90);
            panel.style.paddingLeft = panel.style.paddingRight = 16; panel.style.paddingTop = panel.style.paddingBottom = 12;
            panel.style.backgroundColor = new Color(.07f, .08f, .09f, .98f); panel.style.color = Color.white;
            panel.Add(new Label("AUDIO"));
            string[] labels = { "Master", "Music", "SFX" };
            for (int i = 0; i < sliders.Length; i++)
            {
                int index = i;
                var slider = new Slider(labels[i], 0, 1) { name = "Audio" + labels[i], showInputField = true };
                slider.RegisterValueChangedCallback(change => preferences?.Set(index, change.newValue));
                slider.style.marginTop = 12; sliders[i] = slider; panel.Add(slider);
            }
            panel.Add(new Button(() => SetOpen(false)) { text = "Close" }); root.Add(panel);
            if (preferences != null) preferences.Changed += Refresh;
            SetOpen(false); Refresh();
        }
        private void Update()
        {
            if (!Application.isFocused || Keyboard.current == null) return;
            if (Keyboard.current.f10Key.wasPressedThisFrame) SetOpen(!open);
            else if (open && Keyboard.current.escapeKey.wasPressedThisFrame) SetOpen(false);
        }
        private void Start() => Refresh();
        private void Refresh() { for (int i = 0; i < sliders.Length; i++) sliders[i]?.SetValueWithoutNotify(preferences != null ? preferences.Get(i) : 0); }
        public void SetOpen(bool value)
        {
            open = value; if (panel != null) panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            inputGate?.SetBlocked(this, open);
            if (!open) preferences?.Save();
            else { Refresh(); audioService?.Play(AudioCue.UiClick, Vector3.zero, this); }
        }
        private void OnDisable()
        {
            if (preferences != null) preferences.Changed -= Refresh;
            SetOpen(false); root?.Clear();
        }
    }
}
