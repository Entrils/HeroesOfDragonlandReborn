using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>M56 explicit Editor authoring. Run through Unity MCP, never during gameplay.</summary>
    public static class AncientForestAssetBuilder
    {
        public const string Prefabs = "Assets/Art/Environment/Prefabs/AncientForest";
        public const string Materials = "Assets/Art/Materials/Environment/AncientForest";
        private const string Textures = "Assets/Art/Textures/Environment/AncientForest";
        private const string ScenePath = "Assets/Art/Environment/Scenes/AncientForestLookDev.unity";
        private const string ManifestPath = "ArtSource/Environment/AncientForest/AncientForest.manifest.json";

        [Serializable] private sealed class Manifest
        {
            public Entry[] assets;
            public Palette[] palette;
            public Placement[] placements;
            public string[] reuse;
        }
        [Serializable] private sealed class Entry
        {
            public string name;
            public string fbx;
            public string collider;
            public float[] dimensionsUnity;
            public int[] triangles;
            public string[] lod0Materials;
            public string[] lod1Materials;
            public string[] lod2Materials;
        }
        [Serializable] private sealed class Palette
        {
            public string name;
            public string color;
            public float roughness;
            public bool textured;
        }
        [Serializable] private sealed class Placement
        {
            public string asset;
            public float[] position;
            public float yaw;
            public float scale;
        }

        private static Manifest Read()
        {
            var value = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (value?.assets == null || value.assets.Length != 19 || value.palette == null)
                throw new InvalidOperationException("Missing/incomplete Ancient Forest manifest.");
            return value;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static Material LoadMaterial(string name)
        {
            var value = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + name + ".mat");
            if (value == null) throw new InvalidOperationException("Build M56 assets first: " + name);
            return value;
        }

        private static void BuildMaterial(Palette item)
        {
            string path = Materials + "/" + item.name + ".mat";
            if (File.Exists(path)) return; // Respect reviewed material edits.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");
            if (!ColorUtility.TryParseHtmlString("#" + item.color, out var color))
                throw new InvalidOperationException("Invalid palette color: " + item.name);
            var material = new Material(shader) { name = item.name, enableInstancing = true };
            material.SetColor("_BaseColor", item.textured ? Color.white : color);
            material.SetFloat("_Smoothness", 1 - item.roughness);
            if (item.name.Contains("Leaves") || item.name.EndsWith("Moss", StringComparison.Ordinal))
                material.SetFloat("_Cull", (float)CullMode.Off); // Opaque leaf silhouettes, no alpha cards.
            if (item.textured)
            {
                var stem = Textures + "/T_AF_" + item.name.Substring(5);
                foreach (var suffix in new[] { "BaseColor", "Normal", "Mask" })
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_" + suffix + ".png") == null)
                        throw new InvalidOperationException("Missing texture: " + stem + "_" + suffix);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_BaseColor.png"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_Normal.png"));
                material.SetFloat("_BumpScale", .6f);
                material.EnableKeyword("_NORMALMAP");
                var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_Mask.png");
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetTexture("_OcclusionMap", mask);
                material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            AssetDatabase.CreateAsset(material, path);
        }

        private static GameObject Model(Entry entry)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbx);
            if (model == null) throw new InvalidOperationException("Missing FBX: " + entry.fbx);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            var lod0 = filters.Single(f => f.name == entry.name + "_LOD0");
            var expected = new Vector3(entry.dimensionsUnity[0], entry.dimensionsUnity[1], entry.dimensionsUnity[2]);
            if ((lod0.sharedMesh.bounds.size - expected).magnitude > .03f)
                throw new InvalidOperationException("Imported scale/bounds mismatch: " + entry.name);
            for (int i = 0; i < entry.triangles.Length; i++)
                if (filters.Single(f => f.name == entry.name + "_LOD" + i).sharedMesh == null)
                    throw new InvalidOperationException("Missing LOD: " + entry.name);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if ((t.localScale - Vector3.one).sqrMagnitude > .00001f || Quaternion.Angle(t.localRotation, Quaternion.identity) > .1f)
                    throw new InvalidOperationException("Corrective transform in imported model: " + t.name);
            foreach (var pair in new[] { ("Front", Vector3.forward), ("Right", Vector3.right), ("Up", Vector3.up) })
            {
                var socket = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_" + pair.Item1);
                if ((model.transform.InverseTransformPoint(socket.position) - pair.Item2).magnitude > .02f)
                    throw new InvalidOperationException("Imported orientation mismatch: " + entry.name);
            }
            return model;
        }

        private static void BuildPrefab(Entry entry, Scene preview)
        {
            var root = new GameObject(entry.name);
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(Model(entry), preview);
                model.transform.SetParent(root.transform, false);
                var lods = new LOD[entry.triangles.Length];
                var materialNames = new[] { entry.lod0Materials, entry.lod1Materials, entry.lod2Materials };
                for (int i = 0; i < lods.Length; i++)
                {
                    var renderer = model.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == entry.name + "_LOD" + i);
                    renderer.gameObject.SetActive(true);
                    renderer.enabled = true;
                    renderer.sharedMaterials = materialNames[i].Select(LoadMaterial).ToArray();
                    if (entry.collider == "none" || i == 2) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    float threshold = i == lods.Length - 1 ? .012f : i == 0 ? .20f : .07f;
                    lods[i] = new LOD(threshold, new UnityEngine.Renderer[] { renderer });
                }
                if (lods.Length > 1)
                {
                    var group = root.AddComponent<LODGroup>();
                    group.SetLODs(lods);
                    group.RecalculateBounds();
                }
                if (entry.collider == "capsule")
                {
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0, 1.8f, 0);
                    capsule.radius = .425f;
                    capsule.height = 3.6f;
                }
                else if (entry.collider == "mesh")
                {
                    var filter = model.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name.StartsWith("COL_", StringComparison.Ordinal));
                    filter.gameObject.SetActive(true);
                    filter.GetComponent<MeshRenderer>().enabled = false;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + entry.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildAmbient(Scene preview)
        {
            var root = new GameObject("PF_AF_AmbientMotes");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new InvalidOperationException("URP particle shader unavailable.");
                string path = Materials + "/M_AF_AmbientMotes.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = "M_AF_AmbientMotes" };
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/T_AF_AmbientMote_BaseColor.png"));
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_Blend", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    AssetDatabase.CreateAsset(material, path);
                }
                var particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = true;
                main.playOnAwake = true;
                main.maxParticles = 16;
                main.startLifetime = 6;
                main.startSpeed = .08f;
                main.startSize = .025f;
                main.startColor = new Color(.85f, .83f, .72f, .12f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
                var emission = particles.emission;
                emission.rateOverTime = 2;
                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(12, 2, 12);
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/PF_AF_AmbientMotes.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static GameObject LoadPrefab(string name)
        {
            string folder = name.StartsWith("SM_FR_", StringComparison.Ordinal)
                ? "Assets/Art/Environment/Prefabs/FortressRuins" : Prefabs;
            var value = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".prefab");
            if (value == null) throw new InvalidOperationException("Build required prefab first: " + name);
            return value;
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Build Ancient Forest Assets (M56)")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            var data = Read();
            if (File.Exists(ScenePath) || data.assets.Any(e => File.Exists(Prefabs + "/" + e.name + ".prefab")))
                throw new InvalidOperationException("M56 outputs already exist; validate/repair through MCP, do not overwrite reviewed art.");
            AssetDatabase.Refresh();
            foreach (var name in data.reuse) LoadPrefab(name);
            foreach (var entry in data.assets) Model(entry);
            Folder(Prefabs);
            Folder(Materials);
            Folder("Assets/Art/Environment/Scenes");
            foreach (var item in data.palette) BuildMaterial(item);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var entry in data.assets) BuildPrefab(entry, preview);
                BuildAmbient(preview);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var ground = new GameObject("CalibrationGround | test scene only").AddComponent<BoxCollider>();
                ground.center = new Vector3(0, -.25f, 20);
                ground.size = new Vector3(24, .5f, 64);
                foreach (var p in data.placements)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(p.asset), scene);
                    obj.transform.SetPositionAndRotation(new Vector3(p.position[0], p.position[1], p.position[2]), Quaternion.Euler(0, p.yaw, 0));
                    obj.transform.localScale = Vector3.one * p.scale;
                }
                var ambient = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("PF_AF_AmbientMotes"), scene);
                ambient.transform.position = new Vector3(0, 3.5f, 2);
                var light = new GameObject("OvercastKey").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.4f, .44f, .48f);
                var camera = new GameObject("ForestPathCamera").AddComponent<Camera>();
                camera.fieldOfView = 60;
                camera.transform.position = new Vector3(0, 2.1f, -11);
                camera.transform.LookAt(new Vector3(0, 1.3f, 5));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("M56 scene save failed.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Validate Ancient Forest Assets (M56)")]
        public static void Validate()
        {
            foreach (var entry in Read().assets)
            {
                Model(entry);
                var prefab = LoadPrefab(entry.name);
                if (prefab.GetComponentInChildren<Rigidbody>(true) != null)
                    throw new InvalidOperationException("Forest assets are static: " + entry.name);
                if (entry.triangles.Length > 1 && prefab.GetComponent<LODGroup>()?.GetLODs().Length != entry.triangles.Length)
                    throw new InvalidOperationException("Missing LODs: " + entry.name);
                var colliders = prefab.GetComponentsInChildren<Collider>(true);
                if ((entry.collider == "none" && colliders.Length != 0) || (entry.collider != "none" && colliders.Length != 1))
                    throw new InvalidOperationException("Wrong collision policy: " + entry.name);
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.name.StartsWith("COL_")))
                    if (renderer.sharedMaterials.Any(m => m == null)) throw new InvalidOperationException("Missing material: " + entry.name);
            }
            var ambient = LoadPrefab("PF_AF_AmbientMotes").GetComponent<ParticleSystem>();
            if (ambient == null || ambient.main.maxParticles != 16)
                throw new InvalidOperationException("Ambient particle budget mismatch.");
            Debug.Log("[M56] PASS static asset setup. Battlefield replacement, Play Mode visibility and performance acceptance are separate required checks.");
        }
    }
}
