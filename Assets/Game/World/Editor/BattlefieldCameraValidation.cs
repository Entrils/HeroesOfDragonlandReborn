using System;
using System.Collections;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeroesOfDragonland.World.Editor
{
    public static class BattlefieldCameraValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string message) { if (!value) throw new Exception("Camera validation: " + message); }
        public static void Setup()
        {
            Check(!Application.isPlaying, "Use Edit Mode");
            var camera = UnityEngine.Object.FindFirstObjectByType<BattlefieldCamera>();
            Undo.RecordObject(camera.transform, "Restore third-person camera");
            var view = camera.GetComponent<Camera>(); Undo.RecordObject(view, "Restore perspective");
            if (camera.GetComponent<ThirdPersonCamera>() == null) Undo.AddComponent<ThirdPersonCamera>(camera.gameObject);
            view.orthographic = false; view.fieldOfView = 60; view.nearClipPlane = .1f;
            camera.transform.SetPositionAndRotation(new Vector3(-41, 3, -7), Quaternion.Euler(20, 90, 0));
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveScene(camera.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void StartChecks()
        {
            Check(Application.isPlaying, "Use Play Mode"); Status = "RUNNING";
            UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>().StartCoroutine(Run());
        }
        private static IEnumerator Run()
        {
            GameObject wall = null;
            try
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>();
                NetworkObject avatar = null;
                Check(bootstrap.Running && bootstrap.Runner.TryGetPlayerObject(bootstrap.Runner.LocalPlayer, out avatar), "Local human exists");
                var camera = Camera.main; var binder = camera.GetComponent<BattlefieldCamera>(); var follow = camera.GetComponent<ThirdPersonCamera>();
                Check(!camera.orthographic && binder.HasLocalTarget && follow.Target == avatar.transform, "Perspective follows local human, not bots");
                Vector3 initialForward = camera.transform.forward; initialForward.y = 0;
                Check(Vector3.Dot(initialForward.normalized, avatar.transform.forward) > .99f, "Camera starts behind character facing");
                Check(camera.transform.position.y < avatar.transform.position.y + 6 && Vector3.Distance(camera.transform.position, avatar.transform.position) < 7, "Third-person distance and height");
                follow.ApplyLook(new Vector2(750, 0), true, .016f); yield return null;
                Vector2 move = binder.ToWorldMovement(Vector2.up);
                Vector3 forward = camera.transform.forward; forward.y = 0;
                Check(Vector3.Dot(new Vector3(move.x, 0, move.y).normalized, forward.normalized) > .99f, "W follows camera yaw");
                follow.ApplyLook(new Vector2(-750, 0), true, .016f); yield return null;
                Vector3 pivot = avatar.transform.position + Vector3.up * .6f;
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Temporary camera obstruction check";
                wall.transform.position = pivot - camera.transform.forward * 2;
                wall.transform.localScale = Vector3.one; Physics.SyncTransforms();
                yield return null; yield return null;
                Check(Vector3.Distance(camera.transform.position, pivot) < 2.2f, "Camera retracts before wall");
                wall.SetActive(false); UnityEngine.Object.DestroyImmediate(wall); wall = null;
                yield return null;
                var old = avatar;
                typeof(MultiplayerBootstrap).GetMethod("ResetRoundPlayers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(bootstrap, null);
                yield return null; yield return null;
                Check(bootstrap.Runner.TryGetPlayerObject(bootstrap.Runner.LocalPlayer, out var replacement) && replacement != old
                    && follow.Target == replacement.transform, "Camera rebinds after avatar recreation");
                Check(!bootstrap.Avatars.Where(a => a.GetComponent<NetworkTestPlayer>().IsBot).Any(a => follow.Target == a.transform), "Bots never own camera");
                Status = "PASS local third-person perspective, orbit, camera-relative W, wall collision, avatar rebind and bot isolation";
                Debug.Log(Status);
            }
            finally
            {
                if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
                if (Status == "RUNNING") Status = "FAILED; see Console";
            }
        }
        public static void Capture()
        {
            var camera = Camera.main; var texture = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes("Builds/third-person-restored.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
