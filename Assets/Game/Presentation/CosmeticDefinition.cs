using System;
using System.Collections.Generic;
using HeroesOfDragonland.Presentation;
using UnityEngine;

namespace HeroesOfDragonland.Cosmetics
{
    public enum CosmeticCategory { ArmorSkin, WeaponSkin, CharacterAppearance, Banner, Emblem, VictoryPose, ProfileIcon }
    public enum VictoryGesture { None, Salute, RaiseFist }

    public sealed class CosmeticDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public CosmeticCategory Category { get; }
        public bool StartsUnlocked { get; }
        public Color Color { get; }
        public CharacterAppearance Appearance { get; }
        public VictoryGesture Gesture { get; }
        internal int WireId { get; }

        internal CosmeticDefinition(string id, string name, CosmeticCategory category, bool unlocked, Color color,
            int wireId = 0, CharacterAppearance appearance = default, VictoryGesture gesture = VictoryGesture.None)
        {
            Id = id; Name = name; Category = category; StartsUnlocked = unlocked;
            Color = color; WireId = wireId; Appearance = appearance; Gesture = gesture;
        }
    }

    public static class CosmeticCatalog
    {
        // Save IDs and wire indices are stable. Wire indices are category-local and zero means default.
        public static IReadOnlyList<CosmeticDefinition> All { get; } = Array.AsReadOnly(new CosmeticDefinition[]
        {
            new("armor.ironwood", "Ironwood Armor", CosmeticCategory.ArmorSkin, true, new(.66f, .85f, .71f), 1),
            new("armor.wintersteel", "Wintersteel Armor", CosmeticCategory.ArmorSkin, false, new(.60f, .79f, 1f), 2),
            new("weapon.ember", "Ember Finish", CosmeticCategory.WeaponSkin, true, new(1.22f, .64f, .38f), 1),
            new("weapon.frost", "Frost Finish", CosmeticCategory.WeaponSkin, true, new(.58f, .82f, 1.19f), 2),
            new("appearance.copper", "Copper Scout", CosmeticCategory.CharacterAppearance, true, new(.81f, .43f, .23f),
                appearance: default(CharacterAppearance).With(AppearanceSlot.Face, 1).With(AppearanceSlot.HairColor, 2)
                    .With(AppearanceSlot.SkinTone, 2).With(AppearanceSlot.EyeColor, 1)),
            new("appearance.ash", "Ash Scout", CosmeticCategory.CharacterAppearance, false, new(.65f, .64f, .62f),
                appearance: default(CharacterAppearance).With(AppearanceSlot.Body, 2).With(AppearanceSlot.Face, 2)
                    .With(AppearanceSlot.HairColor, 3).With(AppearanceSlot.EyeColor, 3)),
            new("banner.dawn", "Dawn Standard", CosmeticCategory.Banner, true, new(.88f, .57f, .23f), 1),
            new("banner.pine", "Pine Standard", CosmeticCategory.Banner, true, new(.34f, .58f, .45f), 2),
            new("emblem.star", "North Star", CosmeticCategory.Emblem, true, new(.72f, .84f, .98f), 1),
            new("emblem.flame", "Twin Flame", CosmeticCategory.Emblem, false, new(.91f, .40f, .22f), 2),
            new("pose.salute", "Salute", CosmeticCategory.VictoryPose, true, Color.white, 1, gesture: VictoryGesture.Salute),
            new("pose.fist", "Raised Fist", CosmeticCategory.VictoryPose, true, Color.white, 2, gesture: VictoryGesture.RaiseFist),
            new("icon.compass", "Compass", CosmeticCategory.ProfileIcon, true, new(.54f, .76f, .87f)),
            new("icon.crown", "Crown", CosmeticCategory.ProfileIcon, false, new(.92f, .76f, .34f))
        });

        public static CosmeticDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var cosmetic in All)
                if (string.Equals(cosmetic.Id, id, StringComparison.Ordinal)) return cosmetic;
            return null;
        }

        internal static CosmeticDefinition ResolveWire(CosmeticCategory category, int id)
        {
            if (id == 0) return null;
            foreach (var cosmetic in All)
                if (cosmetic.Category == category && cosmetic.WireId == id) return cosmetic;
            return null;
        }
    }
}
