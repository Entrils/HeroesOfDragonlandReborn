using System;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    // Invoke via Unity MCP after importing Fusion.
    public static class MultiplayerTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MultiplayerFoundationTest.unity";
        public static void BuildClient()
        {
            System.IO.Directory.CreateDirectory("Builds/MultiplayerFoundation");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/MultiplayerFoundation/DragonlandMultiplayer.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            string summary = report.summary.result + " errors=" + report.summary.totalErrors
                + " warnings=" + report.summary.totalWarnings;
            System.IO.File.WriteAllText("Builds/MultiplayerFoundation/build-result.txt", summary);
            Debug.Log("[Multiplayer] Build " + summary);
        }
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Test scene already exists; inspect it instead of overwriting.");
            const string folder = "Assets/Game/Data/MultiplayerFoundation";
            if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Test data already exists.");
            AssetDatabase.CreateFolder("Assets/Game/Data", "MultiplayerFoundation");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, folder + "/Player.mat");
                var actor = new GameObject("Network Test Player");
                actor.AddComponent<NetworkObject>();
                actor.AddComponent<NetworkTransform>();
                actor.AddComponent<NetworkTestPlayer>();
                var view = actor.AddComponent<NetworkPlayerPresentation>();
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body"; body.transform.SetParent(actor.transform, false);
                body.GetComponent<Collider>().enabled = false;
                body.GetComponent<Renderer>().sharedMaterial = material;
                var label = new GameObject("Player Label").AddComponent<TextMesh>();
                label.transform.SetParent(actor.transform, false);
                label.transform.localPosition = Vector3.up * 1.6f;
                label.anchor = TextAnchor.MiddleCenter; label.characterSize = 0.12f; label.fontSize = 32;
                var fields = new SerializedObject(view);
                fields.FindProperty("body").objectReferenceValue = body.GetComponent<Renderer>();
                fields.FindProperty("label").objectReferenceValue = label;
                fields.ApplyModifiedPropertiesWithoutUndo();
                var prefab = PrefabUtility.SaveAsPrefabAsset(actor, folder + "/NetworkTestPlayer.prefab");
                // Remove only this temporary authoring instance; the prefab asset is preserved.
                UnityEngine.Object.DestroyImmediate(actor);
                var bootstrap = new GameObject("Multiplayer Bootstrap").AddComponent<MultiplayerBootstrap>();
                bootstrap.gameObject.AddComponent<MultiplayerTestPanel>();
                bootstrap.gameObject.AddComponent<MultiplayerValidationProbe>();
                fields = new SerializedObject(bootstrap);
                fields.FindProperty("playerPrefab").objectReferenceValue = prefab.GetComponent<NetworkObject>();
                fields.ApplyModifiedPropertiesWithoutUndo();
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Test Arena"; ground.transform.position = new Vector3(0, -0.25f, 0);
                ground.transform.localScale = new Vector3(40, 0.5f, 40);
                var groundMaterial = new Material(material) { color = new Color(0.25f, 0.3f, 0.35f) };
                AssetDatabase.CreateAsset(groundMaterial, folder + "/Ground.mat");
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 32, -26);
                camera.transform.LookAt(Vector3.zero); camera.orthographic = true; camera.orthographicSize = 23;
                camera.gameObject.AddComponent<AudioListener>();
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
                AssetDatabase.SaveAssets();
                Debug.Log("[Multiplayer] Created " + ScenePath + ". Register/bake Fusion prefab before building.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
