using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    public static class TeamSpawnAreaSceneBuilder
    {
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            string path = MultiplayerFortressSceneBuilder.Folder + "/SpawnZoneRules.asset";
            var rules = AssetDatabase.LoadAssetAtPath<SpawnZoneRules>(path);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<SpawnZoneRules>();
                AssetDatabase.CreateAsset(rules, path);
            }
            var scene = EditorSceneManager.OpenScene(MultiplayerFortressSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var a = FindOrCreate(scene, TeamId.TeamA, rules);
                var b = FindOrCreate(scene, TeamId.TeamB, rules);
                bool installed = false;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<NetworkFortressSpawner>() != null)
                    {
                        var service = root.GetComponent<TeamSpawnService>() ?? root.AddComponent<TeamSpawnService>();
                        if (root.GetComponent<MultiplayerTeamSpawnProbe>() == null) root.AddComponent<MultiplayerTeamSpawnProbe>();
                        var data = new SerializedObject(service);
                        var areas = data.FindProperty("areas"); areas.arraySize = 2;
                        areas.GetArrayElementAtIndex(0).objectReferenceValue = a;
                        areas.GetArrayElementAtIndex(1).objectReferenceValue = b;
                        data.ApplyModifiedPropertiesWithoutUndo(); installed = true;
                    }
                if (!installed || !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save team spawn integration.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }
        private static TeamSpawnArea FindOrCreate(Scene scene, TeamId team, SpawnZoneRules rules)
        {
            foreach (var obj in scene.GetRootGameObjects())
                if (obj.TryGetComponent<TeamSpawnArea>(out var existing) && existing.Team == team) return existing;
            var root = new GameObject(team + " Fortress Spawn Area");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(team == TeamId.TeamA ? -15 : 15, 1, -2);
            var area = root.AddComponent<TeamSpawnArea>();
            var data = new SerializedObject(area);
            data.FindProperty("team").intValue = (int)team;
            data.FindProperty("rules").objectReferenceValue = rules;
            var points = data.FindProperty("points"); points.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var point = new GameObject("Spawn Point " + (i + 1)).transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3((i / 3) * (team == TeamId.TeamA ? -2 : 2), 0, (i % 3 - 1) * 2);
                point.rotation = Quaternion.LookRotation(team == TeamId.TeamA ? Vector3.right : Vector3.left);
                points.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return area;
        }
    }
}
