using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.World.Editor
{
    public static class CameraRigValidation
    {
        public static string RigStatus { get; private set; } = "Not run";
        public static string NetworkStatus { get; private set; } = "Not run";

        [MenuItem("Heroes of Dragonland/Camera/Validate M85 Rig")]
        public static void StartRig()
        {
            Check(Application.isPlaying, "Play Mode required");
            var host = Object.FindAnyObjectByType<MultiplayerBootstrap>();
            Check(host != null, "Battlefield bootstrap");
            if (RigStatus == "Running") return;
            RigStatus = "Running";
            host.StartCoroutine(RunRig());
        }

        private static IEnumerator RunRig()
        {
            var fixture = new GameObject("M85 camera clearance fixture");
            fixture.transform.position = new Vector3(120, 90, 120);
            try
            {
                Vector3 origin = fixture.transform.position;
                var avatar = new GameObject("Local subject").transform;
                avatar.SetParent(fixture.transform, false);
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(fixture.transform, false);
                var view = cameraObject.AddComponent<Camera>();
                view.enabled = false;
                view.nearClipPlane = .1f;
                view.aspect = 16f / 9f;
                var rig = cameraObject.AddComponent<ThirdPersonCamera>();
                rig.SetTarget(avatar, Vector3.up * 1.5f);
                yield return Frames();
                var start = view.transform.position;
                Check(start.x - origin.x > .35f && start.z < origin.z - 4, "free shoulder and boom");

                var oldRotation = view.transform.rotation;
                rig.ApplyLook(new Vector2(.75f, 0), false, 1);
                Check(Mathf.Abs(Mathf.DeltaAngle(0, rig.InputYaw) - 90) < .1f
                    && Quaternion.Angle(oldRotation, view.transform.rotation) < .1f, "immediate input and damped rendered orbit");
                yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(rig.InputYaw, view.transform.eulerAngles.y)) < 2, "orbit settles");
                rig.ApplyLook(new Vector2(-.75f, 0), false, 1);
                yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(rig.InputYaw, view.transform.eulerAngles.y)) < 1, "orbit returns");

                var wall = Box(fixture.transform, new Vector3(0, 2.2f, -2), new Vector3(8, 5, .24f));
                Physics.SyncTransforms();
                yield return Frames();
                Check(view.transform.position.z > origin.z - 1.8f && Clearance(wall, view) > .24f, "boom retracts with near-plane clearance");
                var compressed = view.transform.position;
                wall.gameObject.SetActive(false);
                Physics.SyncTransforms();
                yield return null;
                Check(Vector3.Distance(compressed, view.transform.position) < .8f, "opening the wall does not snap the camera outward");
                yield return Until(() => view.transform.position.z < origin.z - 4.4f, "boom extends after clearing wall");

                var rightWall = Box(fixture.transform, new Vector3(.64f, 2, -2), new Vector3(.2f, 5, 12));
                var leftWall = Box(fixture.transform, new Vector3(-.64f, 2, -2), new Vector3(.2f, 5, 12));
                Physics.SyncTransforms();
                yield return Frames();
                Check(Clearance(rightWall, view) > .24f && Clearance(leftWall, view) > .24f
                    && view.transform.position.z < origin.z - 4.3f, "shoulder folds into a fortress-width corridor");
                rightWall.gameObject.SetActive(false);
                leftWall.gameObject.SetActive(false);
                var overlap = Box(fixture.transform, new Vector3(.22f, 1.5f, 0), new Vector3(.24f, 4, 1));
                Physics.SyncTransforms();
                yield return Frames();
                Check(Clearance(overlap, view) > .24f, "camera remains outside wall when pivot starts overlapping");
                overlap.gameObject.SetActive(false);

                var slope = Box(fixture.transform, new Vector3(0, -.45f, -2), new Vector3(5, .22f, 12));
                slope.transform.localRotation = Quaternion.Euler(13, 0, 0);
                var beforeStep = view.transform.position;
                avatar.localPosition += new Vector3(.25f, .8f, 0);
                Physics.SyncTransforms();
                yield return null;
                Check(view.transform.position.y < beforeStep.y + .75f, "follow smooths the elevation change");
                yield return Until(() => view.transform.position.y > beforeStep.y + .73f, "follow catches up on slope");
                Check(Clearance(slope, view) > .24f, "slope clears the camera");
                slope.gameObject.SetActive(false);

                Vector3 feet = avatar.localPosition;
                var center = AimTarget(fixture.transform, feet + new Vector3(0, 0, 7), 2, 1.3f);
                var left = AimTarget(fixture.transform, feet + new Vector3(-3, 0, 7), 2, 1.3f);
                var right = AimTarget(fixture.transform, feet + new Vector3(3, 0, 7), 2, 1.3f);
                var lockOn = avatar.gameObject.AddComponent<TargetLock>();
                lockOn.SetViewCamera(view);
                Physics.SyncTransforms();
                lockOn.Toggle();
                Check(lockOn.CurrentTarget == center, "lock acquires the central visible target");
                lockOn.Switch(-1);
                Check(lockOn.CurrentTarget == left, "switch left");
                lockOn.Switch(1);
                Check(lockOn.CurrentTarget == center, "switch right returns to nearest target");
                lockOn.Switch(1);
                Check(lockOn.CurrentTarget == right, "switch right reaches the next target");
                lockOn.Unlock();
                center.gameObject.SetActive(false);
                left.gameObject.SetActive(false);
                right.gameObject.SetActive(false);

                var boss = AimTarget(fixture.transform, feet + new Vector3(0, 0, 8), 7, 15);
                Physics.SyncTransforms();
                lockOn.Toggle();
                Check(lockOn.CurrentTarget == boss && boss.FramingHeight > 6.9f, "visible boss body can be acquired with off-screen aim point");
                rig.SetFocusTarget(boss);
                rig.ApplyLook(Vector2.zero, true, .02f);
                var anchor = avatar.position + Vector3.up * 1.5f;
                yield return Until(() => Vector3.Distance(view.transform.position, anchor) > 6.5f, "large target pulls the view back");
                Vector3 viewport = view.WorldToViewportPoint(boss.FramingCenter);
                Check(viewport.z > 0 && viewport.y > .05f && viewport.y < .95f, "elevated boss body is framed");

                boss.transform.localPosition = feet + new Vector3(.3f, 0, .3f);
                rig.ApplyLook(Vector2.zero, true, .1f);
                float lockedYaw = rig.InputYaw;
                boss.transform.localPosition = feet + new Vector3(-.3f, 0, -.3f);
                rig.ApplyLook(Vector2.zero, true, .1f);
                Check(Mathf.Abs(Mathf.DeltaAngle(lockedYaw, rig.InputYaw)) < .1f, "point-blank crossing holds the orbit bearing");
                oldRotation = view.transform.rotation;
                rig.SetFocusTarget(null);
                yield return null;
                Check(Quaternion.Angle(oldRotation, view.transform.rotation) < 28, "unlock blends the view");

                avatar.position += Vector3.right * 12;
                yield return Frames();
                Check(Vector3.Distance(avatar.position, view.transform.position) < 7, "teleport reseeds follow without dragging through world");
                Check(avatar.rotation == Quaternion.identity, "camera never rotates the followed actor");
                RigStatus = "PASS free orbit, smoothing, shoulder, walls, slope, switching, boss, close lock and teleport";
                Debug.Log("[M85] " + RigStatus);
            }
            finally
            {
                Object.Destroy(fixture);
                if (RigStatus == "Running") RigStatus = "Failed";
            }
        }

        [MenuItem("Heroes of Dragonland/Camera/Validate M85 Local Match")]
        public static async void StartNetwork()
        {
            var bootstrap = Object.FindAnyObjectByType<MultiplayerBootstrap>();
            Check(Application.isPlaying && bootstrap != null && !bootstrap.Running && !bootstrap.Busy, "idle Battlefield Play Mode");
            NetworkStatus = "Running";
            var bots = bootstrap.GetComponent<DeveloperBotService>();
            int previousBots = bots.RequestedBots;
            try
            {
                bots.SetBotCount(5);
                await bootstrap.StartSession(GameMode.Single, "m85-camera-validation");
                var spawner = bootstrap.GetComponent<NetworkFortressSpawner>();
                await Wait(() => spawner.Match != null && spawner.Match.State == MatchPhase.Lobby
                    && spawner.Match.TeamACount + spawner.Match.TeamBCount == 6, "complete lobby roster");
                var match = spawner.Match;
                match.RPC_SetReady(true);
                await Wait(() => match.CanStartLobby, "ready");
                Check(match.TryStartLobby(), "start match");
                await Wait(() => match.CanPlay, "countdown");

                var runner = bootstrap.Runner;
                Check(runner.TryGetPlayerObject(runner.LocalPlayer, out var avatar), "local human avatar");
                var binder = Object.FindAnyObjectByType<BattlefieldCamera>();
                var rig = binder.GetComponent<ThirdPersonCamera>();
                var view = binder.GetComponent<Camera>();
                await Wait(() => rig.Target == avatar.transform, "bind only local avatar");
                Check(!bots.Avatars.Any(bot => bot.transform == rig.Target), "bots never control the local rig");
                Vector2 intent = binder.ToWorldMovement(Vector2.up);
                var expected = Quaternion.Euler(0, rig.InputYaw, 0) * Vector3.forward;
                Check(Vector2.Dot(intent, new Vector2(expected.x, expected.z)) > .999f, "input uses intent yaw");
                var stats = avatar.GetComponent<CharacterStats>();
                stats.ApplyDamage(10000);
                var spectator = binder.GetComponent<BattlefieldSpectator>();
                await Wait(() => spectator.Active && spectator.Viewed != null, "death hands off to isolated spectator");
                Check(!view.enabled && !rig.enabled && spectator.Viewed.Team == avatar.GetComponent<NetworkTestPlayer>().Team,
                    "spectator shows an eligible ally and disables the gameplay camera");
                stats.ResetToFull();
                await Wait(() => rig.enabled && !spectator.Active && view.enabled, "restored living camera");
                typeof(MultiplayerBootstrap).GetMethod("ResetRoundPlayers", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(bootstrap, null);
                await Wait(() => runner.TryGetPlayerObject(runner.LocalPlayer, out var replacement)
                    && replacement != avatar && rig.Target == replacement.transform, "rebind recreated local avatar");
                NetworkStatus = "PASS local authority, input basis, spectator handoff, and avatar replacement";
                Debug.Log("[M85] " + NetworkStatus);
            }
            catch (Exception exception)
            {
                NetworkStatus = "Failed";
                Debug.LogException(exception);
            }
            finally
            {
                if (bootstrap != null) await bootstrap.Disconnect();
                if (bots != null) bots.SetBotCount(previousBots);
            }
        }

        private static BoxCollider Box(Transform root, Vector3 position, Vector3 size)
        {
            var gameObject = new GameObject("Clearance obstacle");
            gameObject.transform.SetParent(root, false);
            gameObject.transform.localPosition = position;
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private static Targetable AimTarget(Transform root, Vector3 position, float height, float aimHeight)
        {
            var collider = Box(root, position, new Vector3(.9f, height, .9f));
            collider.center = Vector3.up * height * .5f;
            var target = collider.gameObject.AddComponent<Targetable>();
            var data = new SerializedObject(target);
            data.FindProperty("localAimOffset").vector3Value = Vector3.up * aimHeight;
            data.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        private static float Clearance(Collider obstacle, Camera view)
            => Vector3.Distance(obstacle.ClosestPoint(view.transform.position), view.transform.position);

        private static IEnumerator Frames() { yield return null; yield return null; }

        private static IEnumerator Until(Func<bool> ready, string label)
        {
            double timeout = Time.realtimeSinceStartupAsDouble + 7;
            while (!ready() && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            Check(ready(), label);
        }

        private static async Task Wait(Func<bool> ready, string label)
        {
            double timeout = Time.realtimeSinceStartupAsDouble + 15;
            while (Application.isPlaying && Time.realtimeSinceStartupAsDouble < timeout)
            {
                if (ready()) return;
                await Task.Delay(20);
            }
            Check(false, label);
        }

        private static void Check(bool result, string label)
        {
            if (!result) throw new InvalidOperationException("[M85] " + label);
        }
    }
}
