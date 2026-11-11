using System;
using UnityEditor;

namespace HeroesOfDragonland.Art.Editor
{
    // First-import defaults only. Tracked .meta settings and artist-approved overrides survive reimports.
    public sealed class ArtAssetPostprocessor : AssetPostprocessor
    {
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
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importAnimation = animated;
            importer.animationType = !animated ? ModelImporterAnimationType.None
                : path.StartsWith("Assets/Art/Characters/", StringComparison.Ordinal) ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            importer.generateSecondaryUV = !animated && path.StartsWith("Assets/Art/Environment/", StringComparison.Ordinal);
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
