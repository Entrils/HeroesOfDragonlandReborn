using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.Profiles;
using HeroesOfDragonland.Skills;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    public sealed class BuildPresetPicker : VisualElement
    {
        private readonly PlayerProfile profile;
        private readonly DropdownField choices;
        private readonly Foldout advice;
        private readonly Label details;

        public SessionBuildPreset Selected { get; private set; }

        public BuildPresetPicker(PlayerProfile profile, Action selectionChanged)
        {
            this.profile = profile;
            Selected = profile.BuildPreset;
            var options = new List<string> { "None" };
            int selectedIndex = 0;
            foreach (var option in SessionBuildPreset.All)
            {
                options.Add(option.DisplayName);
                if (option == Selected) selectedIndex = options.Count - 1;
            }

            choices = new DropdownField("Build preset", options, selectedIndex) { name = "BuildPresetPicker" };
            choices.style.marginTop = 6;
            Add(choices);

            advice = new Foldout { text = "Preset recommendations", value = true, name = "BuildPresetRecommendations" };
            advice.style.marginBottom = 4;
            details = new Label { name = "BuildPresetAdvice" };
            details.style.whiteSpace = WhiteSpace.Normal;
            advice.Add(details);
            Add(advice);

            choices.RegisterValueChangedCallback(_ =>
            {
                Select(choices.index);
                selectionChanged();
            });
            RefreshAdvice();
        }

        private void Select(int index)
        {
            Selected = index > 0 && index <= SessionBuildPreset.All.Count ? SessionBuildPreset.All[index - 1] : null;
            profile.SelectBuildPreset(Selected);
            RefreshAdvice();
        }

        private void RefreshAdvice()
        {
            advice.style.display = Selected != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (Selected == null) return;
            var quickSlots = Selected.QuickSlots.Select((effect, index) => $"{index + 1}. {effect} potion");
            details.text = $"Attribute priority  {string.Join(" > ", Selected.Attributes)}\n"
                + $"Weapons  {string.Join(", ", Selected.Weapons)}\n"
                + $"Suggested quick slots  {string.Join("   ", quickSlots)}";
        }
    }
}
