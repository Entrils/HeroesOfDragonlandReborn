using System.Linq;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Profiles;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    internal sealed class CosmeticCollectionMenu : System.IDisposable
    {
        private static readonly string[] Categories =
        {
            "Armor Skin", "Weapon Skin", "Appearance", "Banner", "Emblem", "Victory Pose", "Profile Icon"
        };

        private readonly PlayerProfile profile;
        private readonly AppearancePreview preview;
        private readonly EquipmentVisualCatalog visuals;
        private readonly ItemDefinition sampleArmor, sampleWeapon;
        private CosmeticCategory category;
        private CosmeticDefinition selected;

        internal CosmeticCollectionMenu(PlayerProfile profile, CharacterAppearanceVisual model,
            EquipmentVisualCatalog visuals, ItemCatalog items)
        {
            this.profile = profile;
            this.visuals = visuals;
            if (model != null) preview = new AppearancePreview(model);
            if (items != null && visuals != null)
            {
                sampleArmor = items.Items.FirstOrDefault(item => item != null && item.EquipmentSlot == EquipmentSlot.Chest
                    && visuals.Resolve(item) != null);
                sampleWeapon = items.Items.FirstOrDefault(item => item != null && item.Weapon?.Archetype == WeaponArchetype.Sword
                    && visuals.Resolve(item) != null);
            }
        }

        internal bool Draw()
        {
            GUILayout.Label("Cosmetic collection");
            var nextCategory = (CosmeticCategory)GUILayout.SelectionGrid((int)category, Categories, 3);
            if (category != nextCategory)
            {
                category = nextCategory;
                selected = null;
            }

            var equipped = profile.CosmeticItems.Equipped(category);
            foreach (var cosmetic in CosmeticCatalog.All)
            {
                if (cosmetic.Category != category) continue;
                string suffix = cosmetic == equipped ? " (equipped)" : profile.CosmeticItems.Owns(cosmetic.Id) ? " (owned)" : " (locked)";
                if (GUILayout.Button(cosmetic.Name + suffix)) selected = cosmetic;
            }

            if (selected != null) DrawSelection(equipped);
            if (equipped != null && GUILayout.Button("Unequip " + equipped.Name)) profile.CosmeticItems.Unequip(category);
            return GUILayout.Button("Back");
        }

        private void DrawSelection(CosmeticDefinition equipped)
        {
            if (preview != null && category is CosmeticCategory.ArmorSkin or CosmeticCategory.WeaponSkin
                or CosmeticCategory.CharacterAppearance or CosmeticCategory.VictoryPose)
            {
                var appearance = category == CosmeticCategory.CharacterAppearance ? selected.Appearance : profile.EffectiveAppearance;
                preview.ShowCosmetic(selected, appearance, visuals, sampleArmor, sampleWeapon);
                var rect = GUILayoutUtility.GetRect(1, 184, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, preview.Texture, ScaleMode.ScaleToFit);
            }
            DrawBadge(selected);
            bool previous = GUI.enabled;
            GUI.enabled = previous && selected != equipped && profile.CosmeticItems.Owns(selected.Id);
            if (GUILayout.Button("Equip " + selected.Name)) profile.CosmeticItems.Equip(selected.Id);
            GUI.enabled = previous;
        }

        internal static void DrawBadge(CosmeticDefinition cosmetic)
        {
            GUILayout.BeginHorizontal();
            var swatch = GUILayoutUtility.GetRect(28, 20, GUILayout.Width(28));
            Color previous = GUI.color;
            GUI.color = cosmetic.Color;
            GUI.DrawTexture(swatch, Texture2D.whiteTexture);
            GUI.color = previous;
            GUILayout.Label(cosmetic.Name);
            GUILayout.EndHorizontal();
        }

        public void Dispose() => preview?.Dispose();
    }
}
