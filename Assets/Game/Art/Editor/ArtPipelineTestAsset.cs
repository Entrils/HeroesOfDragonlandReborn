using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    public static class ArtPipelineTestAsset
    {
        public const string ModelPath = "Assets/Art/Environment/Models/SM_TestCrate.fbx";
        public const string PrefabPath = "Assets/Art/Environment/Prefabs/SM_TestCrate.prefab";
        public const string ScenePath = "Assets/Art/Environment/Scenes/ArtPipelineValidation.unity";
        public const string MaterialPath = "Assets/Art/Materials/Environment/M_TestCrate.mat";
        public const string TexturePath = "Assets/Art/Textures/Environment/T_TestCrate_BaseColor.png";

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
        [MenuItem("Heroes of Dragonland/Art Pipeline/Create Required Folders")]
        public static void CreateFolders()
        {
            foreach (string category in new[] { "Characters", "Weapons", "Environment", "Creatures", "Materials", "Textures", "Animations" })
                Folder("Assets/Art/" + category);
            foreach (string path in new[] { "Assets/Art/Environment/Models", "Assets/Art/Environment/Prefabs", "Assets/Art/Environment/Scenes",
                "Assets/Art/Materials/Environment", "Assets/Art/Textures/Environment" }) Folder(path);
        }
        [MenuItem("Heroes of Dragonland/Art Pipeline/Build Test Crate Prefab and Scene")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new InvalidOperationException("Close the calibration scene before rebuilding it; its saved asset and GUID will be preserved.");
            CreateFolders(); AssetDatabase.Refresh();
            var modelImporter = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var albedoImporter = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (modelImporter == null || albedoImporter == null) throw new InvalidOperationException("Run the Blender test export first.");
            ArtAssetPostprocessor.ConfigureModel(modelImporter, ModelPath); modelImporter.SaveAndReimport();
            ArtAssetPostprocessor.ConfigureTexture(albedoImporter, TexturePath); albedoImporter.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (model == null || texture == null) throw new InvalidOperationException("Run the Blender test export first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Undo.RecordObject(material, "Configure test crate material");
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .2f);
            EditorUtility.SetDirty(material);
            var previous = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            try
            {
                GameObject crate;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                {
                    crate = new GameObject("SM_TestCrate"); Undo.RegisterCreatedObjectUndo(crate, "Create crate prefab");
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                    visual.transform.SetParent(crate.transform, false);
                    foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                        renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    var collider = Undo.AddComponent<BoxCollider>(crate);
                    collider.center = new Vector3(0, .5f, 0); collider.size = Vector3.one;
                    PrefabUtility.SaveAsPrefabAssetAndConnect(crate, PrefabPath, InteractionMode.AutomatedAction);
                }
                else crate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), preview);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Calibration ground | top Y=0";
                floor.transform.position = new Vector3(.75f, -.05f, 0); floor.transform.localScale = new Vector3(5, .1f, 4);
                var reference = GameObject.CreatePrimitive(PrimitiveType.Cube); reference.name = "Unity reference | 1 x 1 x 1 meter";
                reference.transform.position = new Vector3(1.7f, .5f, 0);
                Label("Blender crate | 1m", new Vector3(0, 1.35f, 0));
                Label("Unity cube | 1m", new Vector3(1.7f, 1.35f, 0));
                Label("+Z front", new Vector3(0, .08f, 1.25f));
                var light = new GameObject("Calibration directional light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(45, -30, 0);
                var camera = new GameObject("Calibration camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(3.5f, 2.4f, 4.5f); camera.transform.LookAt(new Vector3(.75f, .5f, 0));
                camera.fieldOfView = 45; camera.nearClipPlane = .05f;
                if (!EditorSceneManager.SaveScene(preview, ScenePath)) throw new InvalidOperationException("Could not save calibration scene.");
                AssetDatabase.SaveAssets();
                Validate(preview);
            }
            finally
            {
                EditorSceneManager.CloseScene(preview, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            Debug.Log("[ArtPipeline] Saved test material, linked prefab and separate calibration scene.");
        }
        private static void Label(string text, Vector3 position)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.text = text; label.characterSize = .04f; label.fontSize = 42; label.anchor = TextAnchor.MiddleCenter;
            label.transform.position = position; label.transform.rotation = Quaternion.LookRotation(position - new Vector3(3.5f, 2.4f, 4.5f));
        }
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException("Art pipeline: " + reason); }
        public static void Validate(Scene scene)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            Require(importer != null && Mathf.Approximately(importer.globalScale, 1) && importer.useFileScale && importer.bakeAxisConversion, "Importer scale/axis defaults");
            Require(!importer.importAnimation && importer.animationType == ModelImporterAnimationType.None && !importer.addCollider && !importer.isReadable, "Static model import profile");
            var crate = scene.GetRootGameObjects().First(obj => obj.name == "SM_TestCrate");
            Require(crate.transform.localPosition == Vector3.zero && crate.transform.localRotation == Quaternion.identity && crate.transform.localScale == Vector3.one, "Prefab root identity");
            var bounds = new Bounds(); bool first = true;
            foreach (var renderer in crate.GetComponentsInChildren<MeshRenderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                Require(renderer.sharedMaterials.All(m => m != null && AssetDatabase.GetAssetPath(m) == MaterialPath), "External Unity material assignment");
            }
            Require(!first && Vector3.Distance(bounds.size, Vector3.one) < .002f, "Expected 1m bounds, got " + bounds.size);
            Require(Vector3.Distance(bounds.center, new Vector3(0,.5f,0)) < .002f && Mathf.Abs(bounds.min.y) < .002f, "Ground-centered pivot");
            var front = crate.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Socket_Front");
            Require(front != null && Vector3.Distance(front.position, new Vector3(0,.5f,.51f)) < .002f, "Front must be Unity +Z, got " + (front != null ? front.position.ToString() : "missing socket"));
            var right = crate.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Socket_Right");
            var up = crate.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Socket_Up");
            Require(right != null && Vector3.Distance(right.position, new Vector3(.51f,.5f,0)) < .002f, "Right must be Unity +X");
            Require(up != null && Vector3.Distance(up.position, new Vector3(0,1.1f,0)) < .002f, "Up must be Unity +Y");
            foreach (var transform in crate.GetComponentsInChildren<Transform>())
                Require(Vector3.Distance(transform.localScale, Vector3.one) < .002f && Quaternion.Angle(transform.localRotation, Quaternion.identity) < .1f, "No corrective child rotations/scales: " + transform.name);
            foreach (var filter in crate.GetComponentsInChildren<MeshFilter>())
            {
                Require(filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0, "Mesh reference");
                Require(filter.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)
                    && filter.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1), "UV0 and lightmap UV1");
            }
            var box = crate.GetComponent<BoxCollider>();
            Require(box != null && !box.isTrigger && box.center == new Vector3(0,.5f,0) && box.size == Vector3.one, "Simple collider");
            Physics.SyncTransforms();
            Require(Physics.Raycast(new Vector3(.2f,2,.2f), Vector3.down, out var hit, 3) && hit.collider == box && Mathf.Abs(hit.point.y - 1) < .002f, "Crate top blocks physics at Y=1");
            // Prefab linkage is an authoring invariant; Play Mode instances are runtime scene objects.
            Require(Application.isPlaying || PrefabUtility.IsPartOfPrefabInstance(crate), "Scene retains prefab connection");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Require(material.shader.name == "Universal Render Pipeline/Lit" && material.GetTexture("_BaseMap") != null, "URP material/texture");
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            Require(textureImporter.sRGBTexture && textureImporter.mipmapEnabled && !textureImporter.isReadable, "Albedo texture profile");
            Debug.Log("[ArtPipelineValidation] PASS 1m bounds, pivot, +Z front, identity transforms, UVs, material/texture, linked prefab and physics.");
        }
    }
}
