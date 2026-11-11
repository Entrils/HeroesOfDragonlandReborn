using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    public static class ArtPipelinePlayValidation
    {
        public static string Status { get; private set; } = "Not run";
        public static void OpenScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var previous = SceneManager.GetActiveScene();
            if (previous.isDirty) throw new InvalidOperationException("Save the current scene before running calibration.");
            SessionState.SetString("ArtPipeline.PreviousScene", previous.path);
            EditorSceneManager.OpenScene(ArtPipelineTestAsset.ScenePath, OpenSceneMode.Single);
        }
        public static void RestoreScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before restoring scene.");
            string path = SessionState.GetString("ArtPipeline.PreviousScene", "");
            if (!string.IsNullOrEmpty(path) && path != ArtPipelineTestAsset.ScenePath) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        public static void Start()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().path != ArtPipelineTestAsset.ScenePath)
                throw new InvalidOperationException("Run the calibration scene in Play Mode.");
            ArtPipelineTestAsset.Validate(SceneManager.GetActiveScene());
            Status = "RUNNING";
            var host = new GameObject("Temporary art pipeline physics check").AddComponent<ArtPipelineValidationHost>();
            host.StartCoroutine(PhysicsCheck(host.gameObject));
        }
        private static IEnumerator PhysicsCheck(GameObject host)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere); probe.name = "Temporary falling physics probe";
            probe.transform.position = new Vector3(.2f, 2, .2f); probe.transform.localScale = Vector3.one * .2f;
            var body = probe.AddComponent<Rigidbody>(); body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            try
            {
                yield return new WaitForSeconds(1.5f);
                if (probe.transform.position.y < 1.08f || probe.transform.position.y > 1.15f || Mathf.Abs(body.linearVelocity.y) > .1f)
                    throw new InvalidOperationException("Physics probe did not rest on the 1m crate: " + probe.transform.position);
                Status = "PASS: live prefab dimensions, all axes, UVs, material, ground pivot and rigidbody collision at Y=1m.";
                Debug.Log("[ArtPipelinePlayValidation] " + Status);
                probe.SetActive(false);
                Capture();
            }
            finally
            {
                if (Status == "RUNNING") Status = "FAILED; see Console";
                probe.SetActive(false); UnityEngine.Object.Destroy(probe); UnityEngine.Object.Destroy(host);
            }
        }
        private static void Capture()
        {
            var camera = Camera.main;
            var texture = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes("Builds/art-pipeline-calibration.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
