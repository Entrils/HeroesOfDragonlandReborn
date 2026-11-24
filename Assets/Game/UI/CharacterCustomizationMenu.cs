using System.Collections.Generic;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Profiles;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    internal sealed class CharacterCustomizationMenu : System.IDisposable
    {
        private readonly PlayerProfile profile;
        private readonly AppearancePreview preview;
        private CharacterAppearance draft;
        private bool closeUp;

        internal CharacterCustomizationMenu(PlayerProfile profile, CharacterAppearanceVisual model)
        {
            this.profile = profile;
            draft = profile.EffectiveAppearance;
            if (model != null) preview = new AppearancePreview(model);
        }

        internal bool Draw()
        {
            GUILayout.Label("Character customization");
            if (preview != null)
            {
                preview.Update(draft, closeUp);
                var rect = GUILayoutUtility.GetRect(1, 208, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, preview.Texture, ScaleMode.ScaleToFit);
                if (GUILayout.Button(closeUp ? "View full body" : "View face")) closeUp = !closeUp;
            }

            foreach (var slot in CharacterAppearance.Slots) DrawOption(slot);
            GUILayout.BeginHorizontal();
            bool saved = GUILayout.Button("Save appearance");
            bool cancelled = GUILayout.Button("Cancel");
            if (GUILayout.Button("Reset draft")) draft = default;
            GUILayout.EndHorizontal();
            if (saved)
            {
                profile.SetAppearance(draft);
                profile.CosmeticItems.Unequip(CosmeticCategory.CharacterAppearance);
            }
            return saved || cancelled;
        }

        private void DrawOption(AppearanceSlot slot)
        {
            int option = draft.Get(slot);
            var color = CharacterAppearance.Swatch(slot, option);
            GUILayout.BeginHorizontal();
            GUILayout.Label(FieldLabel(slot), GUILayout.Width(94));
            bool previous = GUILayout.Button("<", GUILayout.Width(26));
            var label = CharacterAppearance.Name(slot, option);
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            var swatch = GUILayoutUtility.GetRect(16, 16);
            if (color.a > 0)
            {
                var original = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                GUI.color = original;
            }
            bool next = GUILayout.Button(">", GUILayout.Width(26));
            if (previous || next)
            {
                int count = CharacterAppearance.Count(slot);
                draft = draft.With(slot, (option + (next ? 1 : count - 1)) % count);
            }
            GUILayout.EndHorizontal();
        }

        private static string FieldLabel(AppearanceSlot slot) => slot switch
        {
            AppearanceSlot.HairColor => "Hair color",
            AppearanceSlot.SkinTone => "Skin tone",
            AppearanceSlot.EyeColor => "Eye color",
            _ => slot.ToString()
        };

        public void Dispose() => preview?.Dispose();
    }

    internal sealed class AppearancePreview : System.IDisposable
    {
        private readonly GameObject stage;
        private readonly CharacterAppearanceVisual model;
        private readonly Camera camera;
        private readonly CharacterVisual visual;
        private readonly List<GameObject> sampleParts = new();
        private CosmeticDefinition displayedCosmetic;
        private readonly RenderTexture target;
        internal Texture Texture => target;

        internal AppearancePreview(CharacterAppearanceVisual prefab)
        {
            // Separate by distance so the lobby and preview cameras never render each other's subjects.
            stage = new GameObject("Character customization preview") { hideFlags = HideFlags.DontSave };
            stage.transform.position = new Vector3(0, -300, 0);
            model = Object.Instantiate(prefab, stage.transform, false);
            visual = model.GetComponent<CharacterVisual>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var cameraObject = new GameObject("Preview camera");
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.065f, .08f, .11f);
            camera.orthographic = true;
            camera.farClipPlane = 8f;
            camera.depth = -10f;
            target = new RenderTexture(432, 288, 24) { name = "Character Preview" };
            camera.targetTexture = target;
            var lightObject = new GameObject("Preview key light");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localPosition = new Vector3(.9f, 2f, 1.2f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 4f;
            light.color = new Color(1f, .9f, .83f);
            Update(default, false);
        }

        internal void Update(CharacterAppearance appearance, bool closeUp)
        {
            model.Apply(appearance);
            float height = closeUp ? 1.68f : .94f;
            camera.orthographicSize = closeUp ? .24f : 1.07f;
            camera.transform.localPosition = new Vector3(.48f, height + .04f, 3f);
            camera.transform.LookAt(stage.transform.position + Vector3.up * height);
        }

        internal void ShowCosmetic(CosmeticDefinition cosmetic, CharacterAppearance appearance, EquipmentVisualCatalog catalog,
            ItemDefinition armor, ItemDefinition weapon)
        {
            Update(appearance, cosmetic?.Category == CosmeticCategory.CharacterAppearance);
            if (visual == null || cosmetic == displayedCosmetic) return;
            foreach (var sample in sampleParts) EquipmentVisualSafety.Release(sample);
            sampleParts.Clear();
            model.SetHelmet(false);
            displayedCosmetic = cosmetic;

            var gesture = cosmetic?.Gesture ?? VictoryGesture.None;
            if (visual.Animator != null) visual.Animator.enabled = gesture == VictoryGesture.None;
            CosmeticPresentation.Pose(visual, gesture);

            if (cosmetic?.Category == CosmeticCategory.ArmorSkin) ShowSample(cosmetic, catalog, armor);
            if (cosmetic?.Category == CosmeticCategory.WeaponSkin) ShowSample(cosmetic, catalog, weapon);
        }

        private void ShowSample(CosmeticDefinition cosmetic, EquipmentVisualCatalog catalog, ItemDefinition item)
        {
            var definition = catalog != null ? catalog.Resolve(item) : null;
            if (definition == null || !definition.TryValidate(out _)) return;
            var properties = new MaterialPropertyBlock();
            foreach (var part in definition.Parts)
            {
                var instance = EquipmentVisualSafety.Spawn(part.Prefab != null ? part.Prefab : item.Weapon?.WeaponPrefab,
                    part.Resolve(visual));
                if (instance == null) continue;
                part.Pose.Apply(instance.transform);
                CosmeticPresentation.Tint(instance, cosmetic.Color, properties);
                sampleParts.Add(instance);
            }
        }

        public void Dispose()
        {
            camera.enabled = false;
            camera.targetTexture = null;
            stage.SetActive(false);
            target.Release();
            Object.Destroy(target);
            Object.Destroy(stage);
        }
    }
}
