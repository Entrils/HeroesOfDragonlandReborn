using System;
using System.Collections.Generic;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.World.Editor
{
    public static class FortressAssaultValidation
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("[FortressAssault] FAIL " + label); checks++; }
        public static void ValidateLayout()
        {
            Physics.SyncTransforms();
            var config = AssetDatabase.LoadAssetAtPath<FortressAssaultDefinition>(FortressAssaultSetup.Folder + "/FortressAssault.asset");
            Check(config != null && config.Gates.Count == 2 && config.Guards.Count == 6, "two gates and six additional posts");
            foreach (var team in new[] { TeamId.TeamA, TeamId.TeamB })
            {
                Vector3 king = FortressAssaultSetup.World(team, new Vector3(-5, 0, 7));
                foreach (var approach in new[] { new Vector3(13, 0, 0), new Vector3(4, 0, 15), new Vector3(4, 0, -15) })
                {
                    Vector3 world = FortressAssaultSetup.World(team, approach);
                    Check(Physics.Linecast(world + Vector3.up, king + Vector3.up, ~0, QueryTriggerInteraction.Ignore), team + " King shielded from " + approach);
                    Check(NavMesh.SamplePosition(world, out var start, 1, NavMesh.AllAreas) && NavMesh.SamplePosition(king, out _, 1, NavMesh.AllAreas), team + " approach on navigation");
                    var path = new NavMeshPath();
                    Check(NavMesh.CalculatePath(world, king, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, team + " approach reaches King chamber");
                }
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 outer = FortressAssaultSetup.World(team, new Vector3(3, 1, 14 * side));
                    Vector3 inner = FortressAssaultSetup.World(team, new Vector3(3, 1, 10 * side));
                    Check(!Physics.CheckCapsule(outer, inner, .48f, ~0, QueryTriggerInteraction.Ignore), team + " sally port retains player clearance " + side);
                }
                foreach (var entry in config.Guards)
                    if (entry.Prefab.GetComponent<NetworkFortressGuard>().Team == team)
                    {
                        var definition = entry.Prefab.GetComponent<EnemyBrain>().Definition;
                        Check(definition.LeashDistance <= 6.5f && definition.PatrolRadius <= .3f, team + " guard stays at post");
                        Check(NavMesh.SamplePosition(entry.Position, out var hit, .3f, NavMesh.AllAreas) && Vector3.Distance(hit.position, entry.Position) < .3f, team + " guard post on NavMesh");
                    }
            }
            foreach (var area in Object.FindObjectsByType<TeamSpawnArea>())
                for (int i = 0; i < area.PointCount; i++)
                {
                    Check(area.TryGetPoint(i, out var point, out _), "authored spawn point");
                    Check(!Physics.CheckCapsule(point + Vector3.up * .5f, point - Vector3.up * .5f, .48f, ~0, QueryTriggerInteraction.Ignore), "spawn clear of assault walls");
                }
            Debug.Log("[FortressAssault] layout assertions so far=" + checks);
        }
        [MenuItem("Heroes of Dragonland/Battlefield/Validate Fortress Assault In Play Mode")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            checks = 0; ValidateLayout(); var objects = new List<GameObject>();
            try
            {
                var gateObject = new GameObject("M48 gate"); objects.Add(gateObject); gateObject.SetActive(false); gateObject.transform.position = new Vector3(1200, 0, 1200);
                var gate = gateObject.AddComponent<FortressGate>(); var fields = new SerializedObject(gate); fields.FindProperty("team").intValue = (int)TeamId.TeamA; fields.ApplyModifiedPropertiesWithoutUndo();
                var stats = gateObject.GetComponent<CharacterStats>(); fields = new SerializedObject(stats);
                fields.FindProperty("configuration").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(FortressAssaultSetup.Folder + "/GateStats.asset"); fields.ApplyModifiedPropertiesWithoutUndo();
                var obstacle = gateObject.AddComponent<NavMeshObstacle>(); obstacle.carving = true;
                gateObject.SetActive(true);
                var friendly = Player("M48 friend", TeamId.TeamA, objects); var enemy = Player("M48 enemy", TeamId.TeamB, objects);
                var receiver = gateObject.GetComponent<DamageReceiver>();
                Check(stats.MaxHealth == 600 && !gate.IsOpen, "gate starts closed at full health");
                Check(receiver.TakeDamage(30, friendly) == 0 && stats.CurrentHealth == 600, "friendly damage rejected even with friendly-fire flag");
                Check(receiver.TakeDamage(30, enemy) == 30 && stats.CurrentHealth == 570, "enemy team damages gate");
                Check(receiver.TakeDamage(10000, enemy) == 570 && gate.IsOpen, "lethal damage breaches gate");
                Check(!gateObject.GetComponent<BoxCollider>().enabled && !obstacle.enabled, "breach removes physical and navigation blockers");
                Check(receiver.TakeDamage(30, enemy) == 0, "destroyed gate rejects duplicate damage");
                var config = AssetDatabase.LoadAssetAtPath<FortressAssaultDefinition>(FortressAssaultSetup.Folder + "/FortressAssault.asset");
                var proxy = Object.Instantiate(config.Gates[0].Prefab.gameObject, gateObject.transform.position + Vector3.right * 10, Quaternion.identity); objects.Add(proxy);
                float health = proxy.GetComponent<CharacterStats>().CurrentHealth;
                Check(!proxy.GetComponent<NetworkFortressGate>().CanMutateGate && proxy.GetComponent<DamageReceiver>().TakeDamage(10000, enemy) == 0
                    && proxy.GetComponent<CharacterStats>().ApplyDamage(10000) == 0 && proxy.GetComponent<CharacterStats>().CurrentHealth == health, "unspawned/replica gate fails closed");
                // Exercise guard chase/leash behavior through the same brain used online.
                var guardObject = new GameObject("M48 post guard"); objects.Add(guardObject); guardObject.SetActive(false);
                Vector3 post = FortressAssaultSetup.World(TeamId.TeamA, new Vector3(4, 0, 8.5f)); guardObject.transform.position = post;
                guardObject.AddComponent<CharacterStats>(); var agent = guardObject.AddComponent<NavMeshAgent>(); agent.enabled = false;
                var brain = guardObject.AddComponent<EnemyBrain>(); var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(FortressAssaultSetup.Folder + "/FlankGuard.asset");
                Check(brain.TryConfigureSpawn(definition, true), "guard reuses shared brain"); guardObject.SetActive(true); agent.enabled = true;
                enemy.transform.position = post + Vector3.forward * 2; enemy.AddComponent<EnemyThreatTarget>(); Physics.SyncTransforms();
                guardObject.GetComponent<DamageReceiver>().TakeDamage(1, enemy); brain.Tick(.5f);
                Check(brain.AggroTarget != null, "post guard engages nearby attacker");
                enemy.transform.position = post + Vector3.forward * 9; Physics.SyncTransforms(); brain.Tick(.1f);
                Check(brain.AggroTarget == null && !guardObject.GetComponent<EnemyMeleeAttack>().IsBusy, "guard drops attacker beyond post leash");
                Debug.Log("[FortressAssaultValidation] PASS " + checks + " layout, routes, gate, team, authority and guard assertions.");
            }
            finally { foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); }
        }
        private static GameObject Player(string name, TeamId team, List<GameObject> objects)
        {
            var go = new GameObject(name); objects.Add(go); go.AddComponent<CharacterStats>();
            go.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.Player, true); return go;
        }
        public static void CapturePreview()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            System.IO.Directory.CreateDirectory("Builds/Milestone48");
            var cameraObject = new GameObject("Temporary fortress preview"); var camera = cameraObject.AddComponent<Camera>();
            var temporary = new List<GameObject>(); var target = new RenderTexture(1400, 1000, 24);
            try
            {
                foreach (var team in new[] { TeamId.TeamA, TeamId.TeamB })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FortressAssaultSetup.Folder + "/" + team + "Gate.prefab");
                    var gate = (GameObject)PrefabUtility.InstantiatePrefab(prefab); temporary.Add(gate); gate.transform.SetPositionAndRotation(FortressAssaultSetup.World(team, new Vector3(9, 0, 0)), Quaternion.Euler(0, team == TeamId.TeamA ? 0 : 180, 0));
                    camera.orthographic = true; camera.orthographicSize = 18; camera.transform.SetPositionAndRotation(new Vector3(team == TeamId.TeamA ? -30 : 30, 40, 0), Quaternion.Euler(90, 0, 0));
                    camera.targetTexture = target; camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
                    var texture = new Texture2D(1400, 1000, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); texture.Apply();
                    System.IO.File.WriteAllBytes("Builds/Milestone48/" + team + "-fortress.png", texture.EncodeToPNG()); RenderTexture.active = old; Object.DestroyImmediate(texture);
                }
            }
            finally { camera.targetTexture = null; Object.DestroyImmediate(cameraObject); target.Release(); Object.DestroyImmediate(target); foreach (var go in temporary) Object.DestroyImmediate(go); }
        }
    }
}
