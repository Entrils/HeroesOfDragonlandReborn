using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>Explicit M55 authoring command. Invoke through Unity MCP in Edit Mode.</summary>
    public static class FortressRuinsKitBuilder
    {
        private const string Prefabs = "Assets/Art/Environment/Prefabs/FortressRuins";
        private const string Materials = "Assets/Art/Materials/Environment/FortressRuins";
        private const string ScenePath = "Assets/Art/Environment/Scenes/FortressRuinsKitTest.unity";
        private const string ManifestPath = "ArtSource/Environment/FortressRuins/FortressRuins.manifest.json";

        [Serializable] private sealed class Manifest
        {
            public AssetEntry[] assets;
            public Placement[] placements;
        }
        [Serializable] private sealed class AssetEntry
        {
            public string name;
            public string fbx;
            public float[] dimensionsUnity;
            public string[] lod0Materials;
            public string[] lod1Materials;
        }
        [Serializable] private sealed class Placement
        {
            public string asset;
            public float[] position;
            public float yaw;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        private static Manifest ReadManifest()
        {
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (data?.assets == null || data.assets.Length != 21 || data.placements == null)
                throw new InvalidOperationException("M55 manifest is missing or incomplete.");
            return data;
        }

        private static MeshRenderer Renderer(GameObject model, string suffix)
        {
            return model.GetComponentsInChildren<MeshRenderer>(true)
                .Single(r => r.name.EndsWith(suffix, StringComparison.Ordinal));
        }

        private static void CheckModel(AssetEntry entry)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbx);
            if (model == null) throw new InvalidOperationException("Missing imported FBX: " + entry.fbx);
            var lod0 = Renderer(model, "_LOD0");
            Renderer(model, "_LOD1");
            var collision = model.GetComponentsInChildren<MeshFilter>(true)
                .Single(f => f.name.StartsWith("COL_", StringComparison.Ordinal));
            if (collision.sharedMesh == null || collision.sharedMesh.vertexCount == 0)
                throw new InvalidOperationException("Empty collision mesh: " + entry.name);
            var bounds = lod0.GetComponent<MeshFilter>().sharedMesh.bounds;
            // Imported mesh bounds alone do not catch corrective parent rotations/scales.
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if ((t.localScale - Vector3.one).sqrMagnitude > .00001f ||
                    Quaternion.Angle(t.localRotation, Quaternion.identity) > .1f)
                    throw new InvalidOperationException("Non-identity imported transform: " + entry.name + "/" + t.name);
            }
            if ((bounds.size - V(entry.dimensionsUnity)).magnitude > .03f)
                throw new InvalidOperationException("Imported meter-scale bounds mismatch: " + entry.name);
            foreach (var pair in new[] { ("Front", Vector3.forward), ("Right", Vector3.right), ("Up", Vector3.up) })
            {
                var socket = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_" + pair.Item1);
                if ((model.transform.InverseTransformPoint(socket.position) - pair.Item2).magnitude > .02f)
                    throw new InvalidOperationException("Imported basis mismatch: " + entry.name + "/" + pair.Item1);
            }
        }

        private static Vector3 V(float[] value) => new Vector3(value[0], value[1], value[2]);

        private static Material Material(string name)
        {
            string path = Materials + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Color color;
            switch (name)
            {
                case "M_FR_Stone": color = new Color(.59f, .596f, .545f); break;
                case "M_FR_Wood": color = new Color(.396f, .349f, .294f); break;
                case "M_FR_Iron": color = new Color(.27f, .29f, .31f); break;
                default: throw new InvalidOperationException("Unknown kit material: " + name);
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", name == "M_FR_Iron" ? .9f : 0);
            material.SetFloat("_Smoothness", name == "M_FR_Iron" ? .5f : .15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Build Fortress Ruins Kit (M55)")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("M55 build requires Edit Mode.");
            var manifest = ReadManifest();
            // Refuse to replace authored results. Existing GUIDs and imported metadata stay intact.
            if (File.Exists(ScenePath) || manifest.assets.Any(e => File.Exists(Prefabs + "/" + e.name + ".prefab")))
                throw new InvalidOperationException("M55 output already exists. Use Validate; do not overwrite authored kit assets.");
            AssetDatabase.Refresh();
            foreach (var entry in manifest.assets) CheckModel(entry);
            Folder(Prefabs);
            Folder(Materials);
            Folder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
            foreach (var entry in manifest.assets)
            {
                var wrapper = new GameObject(entry.name);
                SceneManager.MoveGameObjectToScene(wrapper, preview);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbx), preview);
                    model.transform.SetParent(wrapper.transform, false);
                    var lod0 = Renderer(model, "_LOD0");
                    var lod1 = Renderer(model, "_LOD1");
                    lod0.gameObject.SetActive(true);
                    lod1.gameObject.SetActive(true);
                    lod0.enabled = true;
                    lod1.enabled = true;
                    lod0.sharedMaterials = entry.lod0Materials.Select(Material).ToArray();
                    lod1.sharedMaterials = entry.lod1Materials.Select(Material).ToArray();
                    var collision = model.GetComponentsInChildren<MeshFilter>(true)
                        .Single(f => f.name.StartsWith("COL_", StringComparison.Ordinal));
                    collision.gameObject.SetActive(true);
                    collision.GetComponent<MeshRenderer>().enabled = false;
                    var collider = collision.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = collision.sharedMesh;
                    collider.convex = false; // Static kit only. Never attach this to a dynamic Rigidbody.
                    var group = wrapper.AddComponent<LODGroup>();
                    group.SetLODs(new[] { new LOD(.18f, new UnityEngine.Renderer[] { lod0 }),
                        new LOD(.015f, new UnityEngine.Renderer[] { lod1 }) });
                    group.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(wrapper, Prefabs + "/" + entry.name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var p in manifest.placements)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + p.asset + ".prefab");
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    obj.transform.SetPositionAndRotation(V(p.position), Quaternion.Euler(0, p.yaw, 0));
                }
                var light = new GameObject("OvercastKey").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.4f, .44f, .48f);
                var camera = new GameObject("KitOverview").AddComponent<Camera>();
                camera.fieldOfView = 50;
                camera.transform.position = new Vector3(35, 30, -38);
                camera.transform.LookAt(new Vector3(5, 1, 4));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save M55 test scene.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Validate Fortress Ruins Kit (M55)")]
        public static void Validate()
        {
            var manifest = ReadManifest();
            foreach (var entry in manifest.assets)
            {
                CheckModel(entry);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + entry.name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing prefab: " + entry.name);
                var group = prefab.GetComponent<LODGroup>();
                var collider = prefab.GetComponentInChildren<MeshCollider>(true);
                if (group == null || group.GetLODs().Length != 2 || collider == null || collider.sharedMesh == null || collider.convex)
                    throw new InvalidOperationException("Missing LOD/static collision setup: " + entry.name);
                if (prefab.GetComponentInChildren<Rigidbody>(true) != null)
                    throw new InvalidOperationException("Static kit must not have a Rigidbody: " + entry.name);
                foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.name.StartsWith("COL_")))
                    if (r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m == null))
                        throw new InvalidOperationException("Unassigned material: " + entry.name);
            }
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Missing M55 test scene.");
            Debug.Log("[M55] PASS imported basis/bounds, 21 prefabs, material assignments, LODs and static collision. Play Mode and visual review still required.");
        }
    }
}
