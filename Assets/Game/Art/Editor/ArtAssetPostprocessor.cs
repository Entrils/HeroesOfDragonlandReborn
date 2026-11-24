using System;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    // First-import defaults only. Tracked .meta settings and artist-approved overrides survive reimports.
    public sealed class ArtAssetPostprocessor : AssetPostprocessor
    {
        private const string CastleGuardModel = "Assets/Art/Characters/Models/SK_CastleGuard.fbx";

        private bool IsModel => assetPath.StartsWith("Assets/Art/", StringComparison.Ordinal)
            && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            && !assetPath.StartsWith("Assets/Art/Models/", StringComparison.Ordinal);
        private void OnPreprocessModel()
        {
            if (!IsModel || !assetImporter.importSettingsMissing) return;
            ConfigureModel((ModelImporter)assetImporter, assetPath);
        }
        public static void ConfigureModel(ModelImporter importer, string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            bool animated = !name.StartsWith("SM_", StringComparison.Ordinal);
            importer.preserveHierarchy = animated;
            importer.optimizeGameObjects = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false; importer.importLights = false;
            importer.addCollider = false; importer.isReadable = false;
            importer.materialImportMode = path == CastleGuardModel
                ? ModelImporterMaterialImportMode.ImportStandard
                : ModelImporterMaterialImportMode.None;
            if (path == CastleGuardModel)
            {
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importAnimation = animated;
            importer.animationType = !animated ? ModelImporterAnimationType.None
                : path.StartsWith("Assets/Art/Characters/", StringComparison.Ordinal) ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            importer.generateSecondaryUV = !animated && path.StartsWith("Assets/Art/Environment/", StringComparison.Ordinal);
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (assetPath != CastleGuardModel)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && material.shader != shader)
            {
                Color color = material.color;
                material.shader = shader;
                material.SetColor("_BaseColor", color);
            }

            (float metallic, float smoothness) = material.name switch
            {
                "Guard_BlackenedSteel" => (0.76f, 0.65f),
                "Guard_BurnishedSteel" => (0.8f, 0.72f),
                "Guard_AntiqueBrass" => (0.72f, 0.66f),
                "Guard_Chainmail" => (0.58f, 0.43f),
                "Guard_Leather" => (0f, 0.32f),
                "Guard_Oxblood" => (0f, 0.18f),
                _ => (0f, 0.1f)
            };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat(material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness", smoothness);
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/Textures/", StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
            ConfigureTexture((TextureImporter)assetImporter, assetPath);
        }
        public static void ConfigureTexture(TextureImporter importer, string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            bool normal = name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = name.EndsWith("_BaseColor", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_Emission", StringComparison.OrdinalIgnoreCase);
            importer.mipmapEnabled = true; importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
        }
    }
}
