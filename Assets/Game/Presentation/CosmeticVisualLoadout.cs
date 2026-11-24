namespace HeroesOfDragonland.Cosmetics
{
    // Only visual fields used by peers travel over the wire. Appearance colors and morphs
    // already have their own packed snapshot, and profile icons stay on the local profile.
    public readonly struct CosmeticVisualLoadout
    {
        private static readonly CosmeticCategory[] Slots =
        {
            CosmeticCategory.ArmorSkin, CosmeticCategory.WeaponSkin, CosmeticCategory.Banner,
            CosmeticCategory.Emblem, CosmeticCategory.VictoryPose
        };
        private const int BitsPerSlot = 4;
        private const uint IdMask = 15;
        public uint Packed { get; }

        private CosmeticVisualLoadout(uint packed) => Packed = packed;

        internal static CosmeticVisualLoadout From(CosmeticCollection collection)
        {
            uint packed = 0;
            for (int i = 0; i < Slots.Length; i++)
                packed |= (uint)(collection.Equipped(Slots[i])?.WireId ?? 0) << (BitsPerSlot * i);
            return new CosmeticVisualLoadout(packed);
        }

        public CosmeticDefinition Get(CosmeticCategory category)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] == category) return CosmeticCatalog.ResolveWire(category, GetId(Packed, i));
            return null;
        }

        public static bool TryDecode(uint packed, out CosmeticVisualLoadout loadout)
        {
            loadout = default;
            if ((packed >> (BitsPerSlot * Slots.Length)) != 0) return false;
            for (int i = 0; i < Slots.Length; i++)
            {
                int id = GetId(packed, i);
                if (id != 0 && CosmeticCatalog.ResolveWire(Slots[i], id) == null) return false;
            }
            loadout = new CosmeticVisualLoadout(packed);
            return true;
        }

        private static int GetId(uint packed, int index) => (int)((packed >> (BitsPerSlot * index)) & IdMask);
    }
}
