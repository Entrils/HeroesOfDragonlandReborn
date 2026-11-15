using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    public static class GameplayVfxSetup
    {
        public const string Root = "Assets/Art/Vfx";
        public const string CatalogPath = Root + "/GameplayVfxCatalog.asset";
        [Serializable] private sealed class Manifest { public Recipe[] effects; }
        [Serializable] private sealed class Recipe
        {
            public VfxKind kind;
            public string label, shape, color;
            public float duration, radius;
            public int priority, maximumConcurrent, particles;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Material Material(string name, int queue, Texture texture)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("URP particle unlit shader required.");
            material = new Material(shader) { name = name, renderQueue = queue };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_QueueOffset")) material.SetFloat("_QueueOffset", queue - (int)RenderQueue.Transparent);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static Vector3[] Points(params float[] xy)
        {
            var points = new Vector3[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3(xy[i * 2], xy[i * 2 + 1], 0);
            return points;
        }
        private static Vector3[] Glyph(string shape) => shape switch
        {
            "spark" => Points(-.45f,-.15f,-.1f,0,-.35f,.4f,0,.1f,.35f,.4f,.1f,0,.45f,-.15f),
            "shield" => Points(-.4f,.4f,.4f,.4f,.32f,-.18f,0,-.45f,-.32f,-.18f,-.4f,.4f),
            "star" => Points(0,.5f,.1f,.1f,.5f,0,.1f,-.1f,0,-.5f,-.1f,-.1f,-.5f,0,-.1f,.1f,0,.5f),
            "break" => Points(-.4f,.4f,-.12f,.4f,.04f,.15f,-.12f,-.06f,.1f,-.2f,0,-.45f,.35f,-.15f,.4f,.4f,.15f,.4f),
            "slash" => Points(-.5f,-.3f,-.1f,-.1f,.3f,.2f,.5f,.45f),
            "ash" => Points(-.35f,-.3f,-.2f,0,-.32f,.3f,0,.1f,.22f,.4f,.15f,-.1f,.35f,-.3f),
            "bolt" => new[] { new Vector3(0,0,-.45f),new Vector3(.08f,0,-.15f),Vector3.zero,new Vector3(-.08f,0,-.15f),new Vector3(0,0,-.45f) },
            "cross" => Points(-.4f,.1f,-.1f,.1f,-.1f,.4f,.1f,.4f,.1f,.1f,.4f,.1f,.4f,-.1f,.1f,-.1f,.1f,-.4f,-.1f,-.4f,-.1f,-.1f,-.4f,-.1f,-.4f,.1f),
            "ward" => Points(-.45f,.3f,-.35f,.5f,.35f,.5f,.45f,.3f,.38f,-.25f,0,-.5f,-.38f,-.25f,-.45f,.3f),
            "rune" => Points(0,.45f,.35f,0,0,-.45f,-.35f,0,0,.45f,0,-.45f),
            "rise" => Points(-.4f,-.2f,0,.2f,.4f,-.2f,0,.2f,0,.5f,-.2f,.3f,0,.5f,.2f,.3f),
            "diamond" => Points(0,.45f,.3f,0,0,-.45f,-.3f,0,0,.45f),
            "lid" => Points(-.4f,-.3f,.4f,-.3f,.4f,.05f,-.4f,.05f,-.25f,.4f,.45f,.4f,.4f,.05f,-.4f,.05f,-.4f,-.3f),
            "notches" => Points(-.4f,-.3f,-.4f,.3f,-.2f,.3f,-.2f,-.3f,0,-.3f,0,.3f,.2f,.3f,.2f,-.3f,.4f,-.3f,.4f,.3f),
            "crown" => Points(-.45f,.35f,-.35f,-.3f,.35f,-.3f,.45f,.35f,.15f,.05f,0,.4f,-.15f,.05f,-.45f,.35f),
            "brokenCrown" => Points(-.45f,.35f,-.35f,-.3f,-.08f,-.3f,.08f,-.05f,-.05f,.15f,.12f,.4f,.3f,.05f,.45f,.35f,.35f,-.3f,.15f,-.3f),
            "laurel" => Points(-.4f,.4f,-.5f,0,-.3f,-.4f,0,-.5f,.3f,-.4f,.5f,0,.4f,.4f),
            _ => throw new InvalidOperationException("Unknown VFX glyph: " + shape)
        };
        private static LineRenderer Line(Transform parent, string name, Material material, Color color, float width, Vector3[] points)
        {
            var child = new GameObject(name); child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.alignment = LineAlignment.View;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            line.positionCount = points.Length; line.SetPositions(points);
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        private static void MarkerArray(SerializedObject data, string field, LineRenderer line)
        {
            var array = data.FindProperty(field); array.arraySize = 1; array.GetArrayElementAtIndex(0).objectReferenceValue = line;
        }
        private static PooledVfx BuildEffect(Recipe recipe, Material glyphMaterial, Material particleMaterial)
        {
            string path = Root + "/Prefabs/VFX_" + recipe.kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<PooledVfx>();
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("VFX_" + recipe.kind);
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                ColorUtility.TryParseHtmlString("#" + recipe.color, out var color);
                color.a = .78f;
                var points = Glyph(recipe.shape).Select(p => p * recipe.radius).ToArray();
                Line(root.transform, "Glyph backing", glyphMaterial, new Color(.035f,.04f,.045f,.8f), .05f, points);
                Line(root.transform, "Effect identity", glyphMaterial, color, .025f, points);
                if (recipe.kind == VfxKind.FireProjectile)
                {
                    var ring = Enumerable.Range(0, 25).Select(i =>
                    {
                        float angle = i * Mathf.PI * 2 / 24;
                        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (recipe.radius * .5f);
                    }).ToArray();
                    Line(root.transform, "Projectile radius XY", glyphMaterial, color, .02f, ring);
                    Line(root.transform, "Projectile radius XZ", glyphMaterial, color, .02f, ring.Select(p => new Vector3(p.x, 0, p.y)).ToArray());
                }
                var offset = new Vector3(0, .4f + recipe.radius * .25f, 0);
                var friend = Line(root.transform, "Friendly smooth segments", glyphMaterial, new Color(.55f,.82f,.8f,.8f), .025f,
                    Points(-.14f,.02f,-.1f,.1f,0,.14f,.1f,.1f,.14f,.02f).Select(p => p + offset).ToArray());
                var hostile = Line(root.transform, "Hostile teeth", glyphMaterial, new Color(1,.57f,.24f,.95f), .035f,
                    Points(-.18f,0,-.11f,.14f,-.05f,.02f,0,.18f,.05f,.02f,.11f,.14f,.18f,0).Select(p => p + offset).ToArray());
                var neutral = Line(root.transform, "Neutral diamond", glyphMaterial, new Color(.78f,.73f,.58f,.8f), .025f,
                    Points(0,.13f,.1f,0,0,-.13f,-.1f,0,0,.13f).Select(p => p + offset).ToArray());
                var particleRoot = new GameObject("Sparse burst"); particleRoot.transform.SetParent(root.transform, false);
                var particles = particleRoot.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false; main.loop = false; main.stopAction = ParticleSystemStopAction.None;
                main.maxParticles = 16; main.duration = .5f; main.startLifetime = .28f;
                main.startSpeed = .35f; main.startSize = .035f; color.a = .2f; main.startColor = color;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = particles.emission; emission.rateOverTime = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)recipe.particles) });
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .1f * recipe.radius;
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = particleMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.maxParticleSize = .04f;
                var effect = root.AddComponent<PooledVfx>();
                var data = new SerializedObject(effect);
                data.FindProperty("particles").objectReferenceValue = particles;
                data.FindProperty("faceCamera").boolValue = recipe.kind != VfxKind.FireProjectile;
                data.FindProperty("referenceRadius").floatValue = recipe.radius * .5f;
                MarkerArray(data, "friendly", friend); MarkerArray(data, "hostile", hostile); MarkerArray(data, "neutral", neutral);
                data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<PooledVfx>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }
        [MenuItem("Heroes of Dragonland/Art Pipeline/Build Gameplay VFX (M59)")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use compiled Edit Mode through Unity MCP.");
            var recipes = JsonUtility.FromJson<Manifest>(File.ReadAllText("ArtSource/Vfx/VfxLanguage.json")).effects;
            if (recipes == null || recipes.Length != 17 || recipes.Select(r => r.kind).Distinct().Count() != 17
                || recipes.Any(r => r.particles < 0 || r.particles > 16 || r.radius <= 0 || r.radius > 1.2f))
                throw new InvalidOperationException("Invalid VFX authoring manifest.");
            var mote = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Environment/AncientForest/T_AF_AmbientMote_BaseColor.png");
            if (mote == null) throw new InvalidOperationException("Import the shared M56 soft-mote texture first.");
            foreach (string path in new[] { Root, Root + "/Prefabs", Root + "/Materials" }) HumanoidAssetBuilder.Folder(path);
            var glyph = Material("M_VfxGlyph", 2960, Texture2D.whiteTexture);
            var particles = Material("M_VfxParticles", 2950, mote);
            var prefabs = recipes.Select(r => BuildEffect(r, glyph, particles)).ToArray();
            var catalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VfxCatalog>();
                var data = new SerializedObject(catalog); var entries = data.FindProperty("effects"); entries.arraySize = recipes.Length;
                for (int i = 0; i < recipes.Length; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i); var recipe = recipes[i];
                    entry.FindPropertyRelative("kind").intValue = (int)recipe.kind;
                    entry.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
                    entry.FindPropertyRelative("priority").intValue = recipe.priority;
                    entry.FindPropertyRelative("maximumConcurrent").intValue = recipe.maximumConcurrent;
                    entry.FindPropertyRelative("duration").floatValue = recipe.duration;
                }
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { if (Install(root, false)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            Validate();
            Debug.Log("[M59] Built 17 pooled VFX prefabs/catalog and installed prefab presentation adapters. Install a pool/adapters in the loaded scene through MCP; then run Play and host/client validation.");
        }
        private static bool Install(GameObject root, bool undo)
        {
            if (root.GetComponent<NetworkObject>() == null) return false;
            bool changed = false;
            T Add<T>() where T : Component
            {
                var current = root.GetComponent<T>(); if (current != null) return current;
                changed = true; return undo ? Undo.AddComponent<T>(root) : root.AddComponent<T>();
            }
            if (root.GetComponent<NetworkCharacterStats>() != null)
            {
                Add<ActorVfxPresentation>();
                if (root.GetComponent<DamageReceiver>() != null) Add<NetworkImpactVfx>();
                var old = root.GetComponent<AbilityPresentation>();
                if (old != null)
                {
                    var data = new SerializedObject(old); var flag = data.FindProperty("externalVfx");
                    if (!flag.boolValue) { if (undo) Undo.RecordObject(old, "M59 external VFX"); flag.boolValue = true; data.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
                }
            }
            if (root.GetComponent<NetworkCamp>() != null || root.GetComponent<NetworkLootChest>() != null || root.GetComponent<NetworkMatch>() != null) Add<WorldVfxPresentation>();
            return changed;
        }
        [MenuItem("Heroes of Dragonland/Art Pipeline/Install Gameplay VFX in Loaded Scene (M59)")]
        public static void InstallLoadedScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            Validate();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(r => r.GetComponentsInChildren<MultiplayerBootstrap>(true)).Single();
            var pool = bootstrap.GetComponent<VfxPool>(); if (pool == null) pool = Undo.AddComponent<VfxPool>(bootstrap.gameObject);
            Undo.RecordObject(pool, "M59 pool catalog"); Set(pool, "catalog", AssetDatabase.LoadAssetAtPath<VfxCatalog>(CatalogPath));
            foreach (var obj in roots.SelectMany(r => r.GetComponentsInChildren<NetworkObject>(true))) Install(obj.gameObject, true);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[M59] Scene pool and scene-object adapters installed. Review Hierarchy, bake/import Fusion data, inspect Console and save through MCP. Gameplay/layout unchanged.");
        }
        [MenuItem("Heroes of Dragonland/Art Pipeline/Validate Gameplay VFX Assets (M59)")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VfxCatalog>(CatalogPath);
            if (catalog == null || !catalog.TryValidate(out _)) throw new InvalidOperationException("Build a complete VFX catalog first.");
            foreach (VfxKind kind in Enum.GetValues(typeof(VfxKind)))
            {
                var entry = catalog.Find(kind);
                if (entry.Prefab.GetComponentsInChildren<Collider>(true).Length != 0 || entry.Prefab.GetComponentsInChildren<NetworkObject>(true).Length != 0)
                    throw new InvalidOperationException("VFX prefab must not contain collision or network objects: " + kind);
                if (entry.MaximumConcurrent > 8) throw new InvalidOperationException("Per-kind VFX budget exceeds reviewed limit: " + kind);
                foreach (var particles in entry.Prefab.GetComponentsInChildren<ParticleSystem>(true))
                    if (particles.main.maxParticles > 16 || particles.main.playOnAwake || particles.main.stopAction != ParticleSystemStopAction.None || particles.collision.enabled || particles.lights.enabled)
                        throw new InvalidOperationException("Unbounded or unsafe pooled particles: " + kind);
                foreach (var renderer in entry.Prefab.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.renderQueue >= 3000
                        || (renderer.sharedMaterial.HasProperty("_ZWrite") && renderer.sharedMaterial.GetFloat("_ZWrite") != 0))
                        throw new InvalidOperationException("VFX material/telegraph queue mismatch: " + kind);
            }
            Debug.Log("[M59] PASS asset/catalog budgets. Pool lifecycle, outcome delivery, overlap readability and actual network traffic need Play/build validation.");
        }
    }
}
