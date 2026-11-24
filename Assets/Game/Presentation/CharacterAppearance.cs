using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    // Slot order and option indices are part of the profile and network contract. Append, never reorder.
    public enum AppearanceSlot { Body, Face, Hairstyle, HairColor, SkinTone, EyeColor, Voice, Emblem }

    public readonly struct CharacterAppearance
    {
        private const int MaximumCode = ushort.MaxValue;
        private static readonly string[][] Names =
        {
            new[] { "Standard", "Trim", "Slender" },
            new[] { "Original", "Narrow jaw", "Square jaw" },
            new[] { "Swept", "Shaved" },
            new[] { "Original", "Raven", "Copper", "Ash" },
            new[] { "Original", "Fair", "Warm", "Deep" },
            new[] { "Original", "Hazel", "Green", "Blue" },
            new[] { "Default", "Low (placeholder)", "Bright (placeholder)" },
            new[] { "None", "Sun", "Oak", "Mountain" }
        };

        public int Code { get; }
        private CharacterAppearance(int code) => Code = code;
        public static IEnumerable<AppearanceSlot> Slots => (AppearanceSlot[])Enum.GetValues(typeof(AppearanceSlot));
        public static int Count(AppearanceSlot slot) => Names[(int)slot].Length;
        public static string Name(AppearanceSlot slot, int option) => Names[(int)slot][option];
        public int Get(AppearanceSlot slot) => (Code >> ((int)slot * 2)) & 3;
        public string Label(AppearanceSlot slot) => Name(slot, Get(slot));

        public CharacterAppearance With(AppearanceSlot slot, int value)
        {
            if (value < 0 || value >= Count(slot)) return this;
            int shift = (int)slot * 2;
            return new CharacterAppearance((Code & ~(3 << shift)) | (value << shift));
        }

        public static bool TryDecode(int code, out CharacterAppearance appearance)
        {
            appearance = default;
            if (code < 0 || code > MaximumCode) return false;
            for (int slot = 0; slot < Names.Length; slot++)
                if (((code >> (slot * 2)) & 3) >= Names[slot].Length) return false;
            appearance = new CharacterAppearance(code);
            return true;
        }

        public static Color Swatch(AppearanceSlot slot, int option)
        {
            return slot switch
            {
                AppearanceSlot.HairColor => HairSwatches[option],
                AppearanceSlot.SkinTone => SkinSwatches[option],
                AppearanceSlot.EyeColor => EyeSwatches[option],
                AppearanceSlot.Emblem => EmblemSwatches[option],
                _ => Color.clear
            };
        }

        private static readonly Color[] HairSwatches =
        {
            new(.32f, .18f, .10f), new(.11f, .09f, .10f), new(.61f, .25f, .11f), new(.63f, .60f, .57f)
        };
        private static readonly Color[] SkinSwatches =
        {
            new(.73f, .51f, .39f), new(.88f, .67f, .59f), new(.66f, .42f, .25f), new(.39f, .23f, .16f)
        };
        private static readonly Color[] EyeSwatches =
        {
            new(.44f, .36f, .29f), new(.57f, .42f, .19f), new(.35f, .60f, .32f), new(.35f, .58f, .79f)
        };
        private static readonly Color[] EmblemSwatches =
        {
            Color.clear, new(.92f, .64f, .17f), new(.40f, .64f, .36f), new(.55f, .68f, .81f)
        };
    }
}
