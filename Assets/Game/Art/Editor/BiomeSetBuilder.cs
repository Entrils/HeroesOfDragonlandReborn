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
    /// <summary>M57 shared URP authoring commands; invoke explicitly through Unity MCP.</summary>
    public static class BiomeSetBuilder
    {
        private const string Source = "ArtSource/Environment/Biomes/Biomes.manifest.json";
        private const string Prefabs = "Assets/Art/Environment/Prefabs/Biomes";
        private const string Materials = "Assets/Art/Materials/Environment/Biomes";
        private const string Textures = "Assets/Art/Textures/Environment/Biomes";
        private const string ScenePath = "Assets/Art/Environment/Scenes/BiomesLookDev.unity";
        [Serializable] private sealed class Manifest
        {
            public Entry[] assets;
            public Palette[] palette;
            public Transition[] transitions;
            public Accent[] accents;
            public Placement[] placements;
            public string[] reuse;
        }
        [Serializable] private sealed class Entry
        {
            public string name;
            public string fbx;
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
            public string stem;
        }
        [Serializable] private sealed class Transition
        {
            public string name;
            public string stem;
        }
        [Serializable] private sealed class Accent
        {
            public string biome;
            public string color;
            public float intensity;
            public float range;
        }
        [Serializable] private sealed class Placement
        {
            public string asset;
            public float[] position;
            public float yaw;
            public string material;
        }

        private static Manifest Read()
        {
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(Source));
            if (data?.assets?.Length != 5 || data.transitions?.Length != 5 || data.accents?.Length != 4)
                throw new InvalidOperationException("Missing/incomplete M57 manifest.");
            return data;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static Color Parse(string color)
        {
            if (!ColorUtility.TryParseHtmlString("#" + color, out var value)) throw new InvalidOperationException("Invalid palette swatch.");
            return value;
        }
        private static Material LoadMaterial(string name) => name.StartsWith("M_AF_", StringComparison.Ordinal)
            ? AncientForestAssetBuilder.LoadMaterial(name)
            : AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + name + ".mat")
                ?? throw new InvalidOperationException("Missing M57 material: " + name);
        private static GameObject LoadPrefab(string name) => name.StartsWith("SM_BS_", StringComparison.Ordinal)
            ? AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab")
                ?? throw new InvalidOperationException("Missing M57 prefab: " + name)
            : AncientForestAssetBuilder.LoadPrefab(name);

        private static void BuildMaterial(string name, string stem, Color color, float roughness)
        {
            string path = Materials + "/" + name + ".mat";
            if (File.Exists(path)) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", string.IsNullOrEmpty(stem) ? color : Color.white);
            material.SetFloat("_Smoothness", 1 - roughness);
            material.SetFloat("_Metallic", name == "M_BS_WarIron" ? .7f : 0);
            if (name == "M_BS_OldCloth") material.SetFloat("_Cull", (float)CullMode.Off);
            if (!string.IsNullOrEmpty(stem))
            {
                foreach (string suffix in new[] { "BaseColor", "Normal", "Mask" })
                {
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/T_" + stem + "_" + suffix + ".png") == null)
                        throw new InvalidOperationException("Missing M57 texture: " + stem + "/" + suffix);
                    if (stem.StartsWith("BS_Transition", StringComparison.Ordinal))
                    {
                        var importer = (TextureImporter)AssetImporter.GetAtPath(Textures + "/T_" + stem + "_" + suffix + ".png");
                        if (importer.wrapModeU != TextureWrapMode.Clamp || importer.wrapModeV != TextureWrapMode.Repeat)
                        {
                            importer.wrapModeU = TextureWrapMode.Clamp;
                            importer.wrapModeV = TextureWrapMode.Repeat;
                            importer.SaveAndReimport();
                        }
                    }
                }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/T_" + stem + "_BaseColor.png"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/T_" + stem + "_Normal.png"));
                material.SetFloat("_BumpScale", .45f);
                material.EnableKeyword("_NORMALMAP");
                var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/T_" + stem + "_Mask.png");
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
            var filter = model.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == entry.name + "_LOD0");
            var size = new Vector3(entry.dimensionsUnity[0], entry.dimensionsUnity[1], entry.dimensionsUnity[2]);
            if ((filter.sharedMesh.bounds.size - size).magnitude > .03f) throw new InvalidOperationException("M57 scale mismatch: " + entry.name);
            foreach (var pair in new[] { ("Front", Vector3.forward), ("Right", Vector3.right), ("Up", Vector3.up) })
            {
                var socket = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_" + pair.Item1);
                if ((model.transform.InverseTransformPoint(socket.position) - pair.Item2).magnitude > .02f)
                    throw new InvalidOperationException("M57 orientation mismatch: " + entry.name);
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
                var names = new[] { entry.lod0Materials, entry.lod1Materials, entry.lod2Materials };
                var lods = new LOD[entry.triangles.Length];
                MeshFilter last = null;
                for (int i = 0; i < lods.Length; i++)
                {
                    var renderer = model.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == entry.name + "_LOD" + i);
                    renderer.gameObject.SetActive(true);
                    renderer.enabled = true;
                    renderer.sharedMaterials = names[i].Select(LoadMaterial).ToArray();
                    if (lods.Length == 1 || i == 2) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    lods[i] = new LOD(i == lods.Length - 1 ? .012f : i == 0 ? .2f : .07f, new UnityEngine.Renderer[] { renderer });
                    last = renderer.GetComponent<MeshFilter>();
                }
                if (lods.Length > 1)
                {
                    var group = root.AddComponent<LODGroup>();
                    group.SetLODs(lods);
                    group.RecalculateBounds();
                }
                if (entry.name.EndsWith("ShatteredStandard", StringComparison.Ordinal))
                {
                    var pole = root.AddComponent<BoxCollider>();
                    pole.center = new Vector3(0, 1.6f, 0);
                    pole.size = new Vector3(.12f, 3.2f, .12f);
                    var baseCollider = root.AddComponent<BoxCollider>();
                    baseCollider.center = new Vector3(0, .15f, 0);
                    baseCollider.size = new Vector3(.6f, .3f, .6f);
                }
                else if (lods.Length > 1)
                {
                    var collider = root.AddComponent<MeshCollider>();
                    collider.sharedMesh = last.sharedMesh;
                    collider.convex = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + entry.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Build Shared Biome Set (M57)")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            var data = Read();
            if (File.Exists(ScenePath) || data.assets.Any(e => File.Exists(Prefabs + "/" + e.name + ".prefab")))
                throw new InvalidOperationException("M57 authored outputs already exist; inspect/repair rather than overwrite.");
            AssetDatabase.Refresh();
            foreach (var name in data.reuse) LoadPrefab(name);
            foreach (var entry in data.assets) Model(entry);
            Folder(Prefabs);
            Folder(Materials);
            Folder("Assets/Art/Environment/Scenes");
            foreach (var item in data.palette) BuildMaterial(item.name, item.stem, Parse(item.color), item.roughness);
            foreach (var item in data.transitions) BuildMaterial(item.name, item.stem, Color.white, .9f);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var entry in data.assets) BuildPrefab(entry, preview);
                foreach (var item in data.accents)
                {
                    var root = new GameObject("PV_BS_Accent" + item.biome);
                    SceneManager.MoveGameObjectToScene(root, preview);
                    var light = root.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = Parse(item.color);
                    light.intensity = item.intensity;
                    light.range = item.range;
                    light.shadows = LightShadows.None;
                    PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + root.name + ".prefab");
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var p in data.placements)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(p.asset), scene);
                    obj.transform.SetPositionAndRotation(new Vector3(p.position[0], p.position[1], p.position[2]), Quaternion.Euler(0, p.yaw, 0));
                    if (!string.IsNullOrEmpty(p.material))
                        foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>(true))
                            renderer.sharedMaterials = Enumerable.Repeat(LoadMaterial(p.material), renderer.sharedMaterials.Length).ToArray();
                }
                foreach (var accent in data.accents)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/PV_BS_Accent" + accent.biome + ".prefab");
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    obj.transform.position = new Vector3(accent.biome == "Forest" || accent.biome == "Ruins" ? -8 : 8, 1.5f,
                        accent.biome == "Forest" || accent.biome == "Highlands" ? 8 : -8);
                }
                var key = new GameObject("SharedOvercastKey").AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.1f;
                key.shadows = LightShadows.Soft;
                key.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.4f, .44f, .48f);
                var camera = new GameObject("BiomeComparisonCamera").AddComponent<Camera>();
                camera.transform.position = new Vector3(34, 38, -44);
                camera.transform.LookAt(new Vector3(0, 0, 3));
                camera.fieldOfView = 50;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("M57 look-development scene save failed.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("[M57] Built shared materials, five model prefabs, four optional accent prefabs and comparison scene. Map integration and production acceptance remain separate.");
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Validate Shared Biome Set (M57)")]
        public static void Validate()
        {
            var data = Read();
            foreach (var entry in data.assets)
            {
                Model(entry);
                var prefab = LoadPrefab(entry.name);
                if (prefab.GetComponentInChildren<Rigidbody>(true) != null) throw new InvalidOperationException("Static kit has a Rigidbody.");
                if (entry.triangles.Length > 1 && prefab.GetComponent<LODGroup>()?.GetLODs().Length != entry.triangles.Length)
                    throw new InvalidOperationException("M57 prefab has missing LODs: " + entry.name);
                int expected = entry.name.EndsWith("TerrainTile", StringComparison.Ordinal) ? 0
                    : entry.name.EndsWith("ShatteredStandard", StringComparison.Ordinal) ? 2 : 1;
                if (prefab.GetComponentsInChildren<Collider>(true).Length != expected)
                    throw new InvalidOperationException("M57 prefab collision policy mismatch: " + entry.name);
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(m => m == null))
                        throw new InvalidOperationException("M57 material assignment missing: " + entry.name);
            }
            foreach (var item in data.transitions)
            {
                LoadMaterial(item.name);
                foreach (string suffix in new[] { "BaseColor", "Normal", "Mask" })
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(Textures + "/T_" + item.stem + "_" + suffix + ".png");
                    if (importer == null || importer.wrapModeU != TextureWrapMode.Clamp || importer.wrapModeV != TextureWrapMode.Repeat)
                        throw new InvalidOperationException("Transition wrap mode mismatch: " + item.name);
                }
            }
            foreach (var item in data.accents)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/PV_BS_Accent" + item.biome + ".prefab");
                var light = prefab == null ? null : prefab.GetComponent<Light>();
                if (light == null || light.shadows != LightShadows.None || light.range > 4.01f)
                    throw new InvalidOperationException("M57 local accent budget mismatch: " + item.biome);
            }
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("M57 comparison scene is missing.");
            Debug.Log("[M57] PASS asset setup; live biome/camp readability and performance remain pending.");
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Apply Biome Ground Materials (M57)")]
        public static void ApplyLoadedGrounds()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/DragonlandBattlefield.unity" || scene.isDirty)
                throw new InvalidOperationException("Use the saved battlefield scene in Edit Mode through MCP.");
            var map = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Battlefield |", StringComparison.Ordinal));
            const string marker = "BiomeSet | M57 ground treatment";
            if (map.transform.Find(marker) != null) throw new InvalidOperationException("M57 ground treatment already applied.");
            var renderers = map.GetComponentsInChildren<MeshRenderer>(true);
            var names = new[] { "Forest", "Rocky Highlands", "Ruined Battlefield", "Corrupted reach" };
            var materials = new[] { "M_AF_ForestFloor", "M_BS_HighlandGravel", "M_BS_BattlefieldPaving", "M_BS_CorruptedAsh" };
            var selected = names.Select(name => renderers.Where(r => r.name == name).ToArray()).ToArray();
            if (selected[0].Length != 2 || selected[1].Length != 2 || selected[2].Length != 1 || selected[3].Length != 2)
                throw new InvalidOperationException("Biome floor structure changed; inspect before adapting M57.");
            foreach (string name in materials) LoadMaterial(name);
            Folder(Materials);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string path = Materials + "/" + materials[i] + "_Map.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(LoadMaterial(materials[i])) { name = materials[i] + "_Map" };
                        var scale = selected[i][0].transform.lossyScale;
                        material.SetTextureScale("_BaseMap", new Vector2(scale.x / 4, scale.z / 4));
                        AssetDatabase.CreateAsset(material, path);
                    }
                    foreach (var renderer in selected[i])
                    {
                        Undo.RecordObject(renderer, "M57 biome material");
                        renderer.sharedMaterial = material;
                    }
                }
                var root = new GameObject(marker);
                root.transform.SetParent(map.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "M57 biome treatment marker");
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            catch { Undo.RevertAllDownToGroup(undoGroup); throw; }
            Debug.Log("[M57] Ground materials assigned without geometry/camp/navigation changes. Prop placement, transition collars and lighting accents still require MCP review and save.");
        }
    }
}
