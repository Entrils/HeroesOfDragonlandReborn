using System;
using System.Linq;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.Art.Editor
{
    internal static class ModelPackEquipment
    {
        private const string Equipment = "Assets/Art/Equipment/";
        private const string Output = "Assets/Art/Weapons/Prefabs";

        [MenuItem("Tools/Dragonland/Integrate Model Pack Weapons")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(Output))
                AssetDatabase.CreateFolder("Assets/Art/Weapons", "Prefabs");

            foreach (var (model, definition) in new[]
            {
                ("IronSword", "Sword"), ("Greatsword", "Greatsword"),
                ("Spear", "Spear"), ("PlayerStaff", "Staff"), ("Dragonblade", "Dragonblade")
            })
                BindModel(model, definition);

            var dragonblade = Load<EquipmentVisualDefinition>(Equipment + "Definitions/EV_Dragonblade.asset");
            var item = Load<ItemDefinition>("Assets/Game/Data/Items/Dragonblade.asset");
            foreach (var path in new[] { "EquipmentVisualCatalog.asset", "BorderScout/BorderScoutEquipmentVisualCatalog.asset" })
            {
                var catalog = Load<EquipmentVisualCatalog>(Equipment + path);
                var serialized = new SerializedObject(catalog);
                var items = serialized.FindProperty("items");
                int index = 0;
                while (index < items.arraySize &&
                    items.GetArrayElementAtIndex(index).FindPropertyRelative("item").objectReferenceValue != item)
                    index++;
                if (index == items.arraySize)
                    items.InsertArrayElementAtIndex(index);
                var binding = items.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("item").objectReferenceValue = item;
                binding.FindPropertyRelative("visual").objectReferenceValue = dragonblade;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!catalog.TryValidate(out var reason))
                    throw new InvalidOperationException(path + ": " + reason);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BindModel(string model, string definition)
        {
            string path = $"Assets/Art/Weapons/Models/SM_{model}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.addCollider || !importer.bakeAxisConversion)
            {
                ArtAssetPostprocessor.ConfigureModel(importer, path);
                importer.SaveAndReimport();
            }
            var material = BuildMaterial(model);
            var wrapper = new GameObject("PV_" + model);
            try
            {
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), wrapper.transform);
                var sockets = mesh.GetComponentsInChildren<Transform>(true);
                foreach (var socket in RequiredSockets(model))
                    if (!sockets.Any(transform => transform.name == socket))
                        throw new InvalidOperationException(path + " missing " + socket);
                mesh.transform.position -= sockets.First(transform => transform.name == "Socket_Grip").position;
                foreach (var renderer in mesh.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                if (wrapper.GetComponentInChildren<Collider>(true) != null ||
                    !EquipmentVisualSafety.CanSpawn(wrapper, out _))
                    throw new InvalidOperationException(path + " rejected by equipment presentation");

                var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, $"{Output}/PV_{model}.prefab");
                string definitionPath = Equipment + $"Definitions/EV_{definition}.asset";
                var visual = AssetDatabase.LoadAssetAtPath<EquipmentVisualDefinition>(definitionPath);
                if (visual == null && definition == "Dragonblade")
                {
                    visual = Object.Instantiate(Load<EquipmentVisualDefinition>(Equipment + "Definitions/EV_Sword.asset"));
                    AssetDatabase.CreateAsset(visual, definitionPath);
                }
                if (visual == null || prefab == null)
                    throw new InvalidOperationException(definitionPath);
                var serialized = new SerializedObject(visual);
                serialized.FindProperty("parts").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("prefab").objectReferenceValue = prefab;
                SetStowedPose(serialized, model);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
            }
        }

        private static void SetStowedPose(SerializedObject visual, string model)
        {
            // The back socket points up the spine. Keep blades below the shoulder
            // and balance long shafts diagonally around their existing grip.
            var (position, yaw) = model switch
            {
                "Greatsword" => (new Vector3(-.27f, .09f, .40f), 141f),
                "Spear" => (new Vector3(-.12f, .09f, -.32f), 34f),
                "PlayerStaff" => (new Vector3(-.12f, .09f, -.30f), 38f),
                _ => (new Vector3(-.23f, .07f, .35f), 147f)
            };
            var pose = visual.FindProperty("stowedPose");
            pose.FindPropertyRelative("position").vector3Value = position;
            pose.FindPropertyRelative("eulerAngles").vector3Value = Vector3.up * yaw;
        }

        private static string[] RequiredSockets(string model) => model switch
        {
            "Greatsword" or "Spear" => new[] { "Socket_Grip", "Socket_Tip", "Socket_OffHand" },
            "PlayerStaff" => new[] { "Socket_Grip", "Socket_Tip", "Socket_Focus" },
            _ => new[] { "Socket_Grip", "Socket_Tip" }
        };

        private static Material BuildMaterial(string model)
        {
            string materialPath = Equipment + $"Materials/M_{model}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string prefix = $"Assets/Art/Textures/Weapons/{model}/T_{model}";
            material.SetTexture("_BaseMap", ImportTexture(prefix + "_BaseColor.png"));
            material.SetTexture("_BumpMap", ImportTexture(prefix + "_Normal.png"));
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", ImportTexture(prefix + "_Mask.png"));
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetFloat("_Smoothness", 1);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Emission.png");
            if (emission != null)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ImportTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool isNormal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
            bool isColor = path.EndsWith("_BaseColor.png", StringComparison.Ordinal);
            if (importer.sRGBTexture != isColor ||
                importer.textureType != (isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default))
            {
                ArtAssetPostprocessor.ConfigureTexture(importer, path);
                importer.SaveAndReimport();
            }
            return Load<Texture2D>(path);
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
    }
}

