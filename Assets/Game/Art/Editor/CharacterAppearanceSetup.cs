using System;
using System.Linq;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.Art.Editor
{
    public static class CharacterAppearanceSetup
    {
        private const string Folder = "Assets/Art/Characters/Cosmetics";
        private const string ScoutPrefab = "Assets/Art/Characters/Prefabs/PV_BorderScout.prefab";
        internal const string ScoutModel = "Assets/Art/Characters/Models/SK_BorderScout.fbx";

        [MenuItem("Heroes of Dragonland/Art/Install Border Scout Cosmetics")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Characters", "Cosmetics");
            using (var scope = new PrefabUtility.EditPrefabContentsScope(ScoutPrefab))
            {
                var root = scope.prefabContentsRoot;
                var models = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                SkinnedMeshRenderer Find(string name) => models.Single(model => model.name == "Scout_" + name);
                var torso = new[] { Find("TrunkCovered"), Find("Shirt"), Find("Jerkin") };
                foreach (var model in torso) model.sharedMesh = BuildMorphMesh(model.name, AddTrim);
                var face = Find("HeadNeck");
                face.sharedMesh = BuildMorphMesh(face.name, AddFacePresets);

                var appearance = root.GetComponent<CharacterAppearanceVisual>() ?? root.AddComponent<CharacterAppearanceVisual>();
                var data = new SerializedObject(appearance);
                SetArray(data.FindProperty("body"), torso);
                SetArray(data.FindProperty("skin"), models.Where(model =>
                    model.sharedMaterials.Length > 0 && model.sharedMaterials[0].name == "M_Scout_Skin").ToArray());
                data.FindProperty("face").objectReferenceValue = face;
                data.FindProperty("hair").objectReferenceValue = Find("Hair");
                data.FindProperty("brows").objectReferenceValue = Find("Brows");
                data.FindProperty("eyes").objectReferenceValue = Find("Eyes");
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterAppearance] Installed visual-only cosmetic options on the Border Scout.");
        }

        private static void SetArray(SerializedProperty property, SkinnedMeshRenderer[] models)
        {
            property.arraySize = models.Length;
            for (int i = 0; i < models.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
        }

        internal static void RefreshImportedMeshes()
        {
            foreach (var name in new[] { "Scout_TrunkCovered", "Scout_Shirt", "Scout_Jerkin", "Scout_HeadNeck" })
            {
                // The prefab overrides these meshes to add cosmetic morphs.
                // Retain the GUID so existing renderer and prefab references survive a model reimport.
                var mesh = BuildMorphMesh(name, name == "Scout_HeadNeck" ? AddFacePresets : AddTrim);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }
        }

        private static Mesh BuildMorphMesh(string meshName, Action<Mesh> addShapes)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ScoutModel);
            var source = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == meshName).sharedMesh;
            var copy = Object.Instantiate(source);
            copy.name = meshName + "_Cosmetics";
            addShapes(copy);
            var path = $"{Folder}/{copy.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(copy, path);
                existing = copy;
            }
            else
            {
                EditorUtility.CopySerialized(copy, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(copy);
            }
            return existing;
        }

        private static void AddTrim(Mesh mesh) =>
            AddShape(mesh, CharacterAppearanceVisual.BodyShape, vertex =>
            {
                // Narrow the soft waist silhouette and taper back to the stock shoulders and hips.
                // The unchanged armature keeps weapon sockets and armor mounts in their authored pose.
                float waist = Mathf.SmoothStep(0, 1, 1f - Mathf.Abs(-vertex.z - 1.17f) / .26f);
                float center = Mathf.SmoothStep(0, 1, 1f - Mathf.Abs(vertex.x) / .39f);
                return new Vector3(-vertex.x * waist * center * .24f, -vertex.y * waist * center * .08f, 0);
            });

        private static void AddFacePresets(Mesh mesh)
        {
            AddShape(mesh, CharacterAppearanceVisual.NarrowFaceShape, vertex => FaceDelta(vertex, -.18f));
            AddShape(mesh, CharacterAppearanceVisual.SquareFaceShape, vertex => FaceDelta(vertex, .16f));
        }

        private static Vector3 FaceDelta(Vector3 vertex, float width)
        {
            // Imported mesh coordinates have height along -Z and forward along +Y.
            // Keep ears, eyes, and skull fixed for hair and helmets.
            float height = Mathf.SmoothStep(0, 1, 1f - Mathf.Abs(-vertex.z - 1.616f) / .057f);
            float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.035f, .105f, vertex.y));
            return new Vector3(vertex.x * width * height * front, 0, 0);
        }

        private static void AddShape(Mesh mesh, string shape, Func<Vector3, Vector3> offset)
        {
            var vertices = mesh.vertices;
            var deltas = Array.ConvertAll(vertices, vertex => offset(vertex));
            mesh.AddBlendShapeFrame(shape, 100f, deltas, null, null);
        }
    }

    internal sealed class BorderScoutCosmeticMeshImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CharacterAppearanceSetup.ScoutModel) >= 0)
                CharacterAppearanceSetup.RefreshImportedMeshes();
        }
    }
}
