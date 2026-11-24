using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CharacterAppearanceVisual : MonoBehaviour
    {
        public const string BodyShape = "Cosmetic_BodyTrim";
        public const string NarrowFaceShape = "Cosmetic_FaceNarrow";
        public const string SquareFaceShape = "Cosmetic_FaceSquare";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        [SerializeField] private SkinnedMeshRenderer[] body = System.Array.Empty<SkinnedMeshRenderer>();
        [SerializeField] private SkinnedMeshRenderer face, hair, brows, eyes;
        [SerializeField] private Renderer[] skin = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock properties;
        private bool helmet, shaved;
        public int AppliedCode { get; private set; } = -1;

        public void Apply(CharacterAppearance appearance)
        {
            if (AppliedCode == appearance.Code) return;
            bool first = AppliedCode < 0;
            AppliedCode = appearance.Code;
            if (first && appearance.Code == 0) return; // Keep the authored defaults untouched.
            int bodyPreset = appearance.Get(AppearanceSlot.Body);
            foreach (var renderer in body) SetShape(renderer, BodyShape, bodyPreset * 50f);
            int facePreset = appearance.Get(AppearanceSlot.Face);
            SetShape(face, NarrowFaceShape, facePreset == 1 ? 100f : 0f);
            SetShape(face, SquareFaceShape, facePreset == 2 ? 100f : 0f);
            shaved = appearance.Get(AppearanceSlot.Hairstyle) == 1;
            RefreshHair();

            int skinTone = appearance.Get(AppearanceSlot.SkinTone);
            var skinTint = skinTone switch
            {
                1 => new Color(1.10f, 1.03f, 1.01f),
                2 => new Color(.82f, .69f, .57f),
                3 => new Color(.51f, .34f, .29f),
                _ => Color.white
            };
            foreach (var renderer in skin) SetColor(renderer, 0, skinTint);
            SetColor(eyes, 2, skinTint); // Lid margin follows skin, while cornea stays untouched.

            int hairColor = appearance.Get(AppearanceSlot.HairColor);
            var hairTint = hairColor switch
            {
                1 => new Color(.25f, .33f, .46f),
                2 => new Color(1.22f, .80f, .60f),
                3 => new Color(.93f, 1.12f, 1.37f),
                _ => Color.white
            };
            if (hair != null)
                for (int i = 0; i < hair.sharedMaterials.Length; i++) SetColor(hair, i, hairTint);
            SetColor(brows, 0, hairTint);

            int eyeColor = appearance.Get(AppearanceSlot.EyeColor);
            var eyeTint = eyeColor switch
            {
                1 => new Color(1.13f, .97f, .67f),
                2 => new Color(.74f, 1.12f, .69f),
                3 => new Color(.71f, .94f, 1.24f),
                _ => Color.white
            };
            SetColor(eyes, 0, eyeTint);
        }

        public void SetHelmet(bool value)
        {
            helmet = value;
            RefreshHair();
        }

        private void RefreshHair()
        {
            if (hair != null) hair.forceRenderingOff = helmet || shaved;
        }

        private static void SetShape(SkinnedMeshRenderer renderer, string shape, float weight)
        {
            if (renderer == null || renderer.sharedMesh == null) return;
            int index = renderer.sharedMesh.GetBlendShapeIndex(shape);
            if (index >= 0) renderer.SetBlendShapeWeight(index, weight);
        }

        private void SetColor(Renderer renderer, int slot, Color tint)
        {
            if (renderer == null) return;
            var materials = renderer.sharedMaterials;
            if (slot >= materials.Length || materials[slot] == null || !materials[slot].HasProperty(BaseColor)) return;
            properties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, slot);
            properties.SetColor(BaseColor, materials[slot].GetColor(BaseColor) * tint);
            renderer.SetPropertyBlock(properties, slot);
        }
    }
}
