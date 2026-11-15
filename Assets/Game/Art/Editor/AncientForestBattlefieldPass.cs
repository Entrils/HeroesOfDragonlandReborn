using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>Opt-in M56 visual pass. Preserves existing cover collision and camp data.</summary>
    public static class AncientForestBattlefieldPass
    {
        private const string RootName = "AncientForest | M56 visual replacement";

        private static bool Forest(Vector3 p) =>
            (p.x >= -42 && p.x <= -4 && p.z >= 8 && p.z <= 42) ||
            (p.x >= 4 && p.x <= 42 && p.z >= -42 && p.z <= -8);

        private static Material TiledMaterial(string sourceName, string suffix, Vector2 tiling)
        {
            string path = AncientForestAssetBuilder.Materials + "/" + sourceName + suffix + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(AncientForestAssetBuilder.LoadMaterial(sourceName)) { name = sourceName + suffix };
            material.SetTextureScale("_BaseMap", tiling);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Spawn(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AncientForestAssetBuilder.LoadPrefab(name), parent.gameObject.scene);
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, rotation);
            return obj;
        }

        [MenuItem("Heroes of Dragonland/Art Pipeline/Apply Ancient Forest to Loaded Battlefield (M56)")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/DragonlandBattlefield.unity")
                throw new InvalidOperationException("Open only the battlefield in Edit Mode, then invoke through MCP.");
            if (scene.isDirty) throw new InvalidOperationException("Save/review the existing scene changes before applying this pass.");
            var map = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Battlefield |", StringComparison.Ordinal));
            if (map.transform.Find(RootName) != null) throw new InvalidOperationException("M56 already applied; inspect instead of duplicating.");
            AncientForestAssetBuilder.Validate();
            var renderers = map.GetComponentsInChildren<MeshRenderer>(true);
            var trunks = renderers.Where(r => r.name == "Tree trunk | collision cover" && Forest(r.bounds.center)).ToArray();
            var canopies = renderers.Where(r => r.name == "Tree canopy placeholder" && Forest(r.bounds.center)).ToArray();
            var floors = renderers.Where(r => r.name == "Forest").ToArray();
            if (trunks.Length != 36 || canopies.Length != 36 || floors.Length != 2)
                throw new InvalidOperationException("Battlefield forest structure changed; inspect through MCP before adapting the pass.");
            var spawner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NetworkCampSpawner>(true)).Single();
            string campsBefore = EditorJsonUtility.ToJson(spawner);
            var campData = new SerializedObject(spawner);
            var entries = campData.FindProperty("entries");
            var camps = new List<Vector3>();
            for (int i = 0; i < entries.arraySize; i++)
                camps.Add(spawner.transform.TransformPoint(entries.GetArrayElementAtIndex(i).FindPropertyRelative("localPosition").vector3Value));
            var colliders = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(true)).ToArray();
            var snapshots = colliders.Select(c => (c, c.bounds, c.enabled)).ToArray();
            var trails = renderers.Where(r => r.name == "Trail junction").ToArray();
            int undoGroup;
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("M56 Ancient Forest visual replacement");
            try
            {
                var root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetParent(map.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "M56 forest visuals");
                foreach (var trunk in trunks)
                {
                    var collider = trunk.GetComponent<Collider>();
                    if (collider == null || !collider.enabled) throw new InvalidOperationException("Missing retained tree cover collider.");
                    var bounds = collider.bounds;
                    if (Mathf.Abs(bounds.size.x - .85f) > .03f || Mathf.Abs(bounds.size.y - 3.6f) > .03f)
                        throw new InvalidOperationException("Tree cover dimensions changed; do not silently refit collision.");
                    int variant = Mathf.Abs(Mathf.RoundToInt(trunk.transform.localPosition.x * 5)) % 2 + 1;
                    var obj = Spawn("SM_AF_TreeAncient0" + variant, root.transform,
                        new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), trunk.transform.rotation);
                    // The original cover collider remains the only gameplay/camera/lock-on blocker.
                    foreach (var extra in obj.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(extra);
                    Undo.RecordObject(trunk, "Hide placeholder tree renderer");
                    trunk.enabled = false;
                }
                foreach (var canopy in canopies)
                {
                    Undo.RecordObject(canopy, "Hide placeholder canopy renderer");
                    canopy.enabled = false;
                }
                var floorMaterial = TiledMaterial("M_AF_ForestFloor", "_Map", new Vector2(9.5f, 8.5f));
                foreach (var floor in floors)
                {
                    Undo.RecordObject(floor, "Apply forest floor material");
                    floor.sharedMaterial = floorMaterial;
                }
                foreach (var trail in trails.Where(t => Forest(t.bounds.center)))
                {
                    int length = Mathf.RoundToInt(Mathf.Pow(2, Mathf.Round(Mathf.Log(Mathf.Max(4, trail.transform.lossyScale.z), 2))));
                    length = Mathf.Clamp(length, 4, 32);
                    var material = TiledMaterial("M_AF_DirtPath", "_Map" + length, new Vector2(.8f, length / 4f));
                    Undo.RecordObject(trail, "Apply path material without changing route");
                    trail.sharedMaterial = material;
                }

                bool Clear(Vector3 p)
                {
                    if (camps.Any(c => Vector2.Distance(new Vector2(c.x, c.z), new Vector2(p.x, p.z)) < 10)) return false;
                    foreach (var trail in trails)
                    {
                        var local = trail.transform.InverseTransformPoint(p);
                        if (Mathf.Abs(local.x) < .5f + 1.8f / trail.transform.lossyScale.x &&
                            Mathf.Abs(local.z) < .5f + 1.8f / trail.transform.lossyScale.z) return false;
                    }
                    foreach (var collider in colliders)
                    {
                        var b = collider.bounds;
                        if (b.size.y < .5f || b.max.y < .6f) continue; // Skip traversal ground.
                        if (p.x > b.min.x - 1 && p.x < b.max.x + 1 && p.z > b.min.z - 1 && p.z < b.max.z + 1) return false;
                    }
                    return true;
                }

                int grass = 0, bushes = 0;
                for (int x = -40; x <= -7; x += 3)
                for (int z = 10; z <= 40; z += 3)
                {
                    var p = new Vector3(x, .025f, z);
                    var mirrored = new Vector3(-x, .025f, -z);
                    if (!Clear(p) || !Clear(mirrored)) continue;
                    bool bush = (x + z) % 5 == 0;
                    if (bush ? bushes >= 10 : grass >= 48) continue;
                    string name = bush ? "SM_AF_Bush01" : "SM_AF_GrassClump01";
                    Spawn(name, root.transform, p, Quaternion.identity);
                    Spawn(name, root.transform, mirrored, Quaternion.Euler(0, 180, 0));
                    if (bush) bushes++; else grass++;
                }
                foreach (int sign in new[] { -1, 1 })
                    Spawn("PF_AF_AmbientMotes", root.transform, new Vector3(-23 * sign, 3.5f, 25 * sign), Quaternion.identity);
                if (EditorJsonUtility.ToJson(spawner) != campsBefore)
                    throw new InvalidOperationException("Camp configuration changed unexpectedly.");
                var after = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(true)).ToArray();
                if (after.Length != colliders.Length || snapshots.Any(s => s.c == null ||
                    s.c.enabled != s.enabled || (s.c.bounds.center - s.bounds.center).sqrMagnitude > .00001f ||
                    (s.c.bounds.size - s.bounds.size).sqrMagnitude > .00001f))
                    throw new InvalidOperationException("Pre-existing collision changed unexpectedly.");
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[M56] Applied 36 tree visuals, paired floor/path materials, {grass * 2} grass and {bushes * 2} bushes. Camp data/collision preserved. Review Play Mode, solid prop placement and frame budget through MCP before saving acceptance.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }
    }
}
