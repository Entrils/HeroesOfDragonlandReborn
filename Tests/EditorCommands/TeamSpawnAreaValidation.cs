using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerFortressTest.unity", OpenSceneMode.Additive);
        try
        {
            TeamSpawnService service = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.TryGetComponent<TeamSpawnService>(out var found)) service = found;
            Check(service != null, "Saved service");
            foreach (var team in new[] { TeamId.TeamA, TeamId.TeamB })
            {
                var area = service.GetArea(team);
                Check(area != null && area.PointCount == 6 && area.Rules != null, "Six authored points per fortress");
                Check(!area.Rules.ProtectedZone, "Protection remains opt-in");
                for (int i = 0; i < area.PointCount; i++)
                {
                    Check(area.TryGetPoint(i, out var point, out _) && area.Contains(point, -.5f), "Valid team point");
                    Check(team == TeamId.TeamA ? point.x < -10 : point.x > 10, "Own fortress only");
                    for (int j = 0; j < i; j++)
                    {
                        area.TryGetPoint(j, out var other, out _);
                        Check(Vector3.Distance(point, other) >= 1.1f, "Nonoverlapping authored points");
                    }
                }
                var temporaryRules = UnityEngine.Object.Instantiate(area.Rules);
                try
                {
                    var settings = new SerializedObject(temporaryRules);
                    settings.FindProperty("protectedZone").boolValue = true;
                    settings.FindProperty("enemyPlayerEntry").intValue = (int)EnemySpawnZoneEntry.BlockEntry;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    var data = new SerializedObject(area); data.FindProperty("rules").objectReferenceValue = temporaryRules; data.ApplyModifiedPropertiesWithoutUndo();
                    var enemy = team == TeamId.TeamA ? TeamId.TeamB : TeamId.TeamA;
                    Vector3 center = area.transform.position, left = center + Vector3.left * 6, right = center + Vector3.right * 6;
                    Check(!service.AllowsSpawn(enemy, center) && !service.AllowsSpawn(TeamId.Unassigned, center), "Enemy and neutral spawn denied");
                    Check(service.AllowsSpawn(team, center), "Friendly spawn allowed");
                    Check(area.BlocksEntry(enemy, left, right, .5f), "Fast crossing cannot tunnel");
                    Check(!area.BlocksEntry(team, left, right, .5f), "Friendly movement allowed");
                    Check(!area.BlocksEntry(enemy, center, right, .5f), "Existing occupants can exit");
                    Check(!area.BlocksEntry(enemy, left, left + Vector3.forward, .5f), "Outside movement allowed");
                    settings.FindProperty("enemyPlayerEntry").intValue = (int)EnemySpawnZoneEntry.Allow; settings.ApplyModifiedPropertiesWithoutUndo();
                    Check(!area.BlocksEntry(enemy, left, right, .5f) && !service.AllowsSpawn(enemy, center), "Entry and spawn rules independent");
                    settings.FindProperty("protectedZone").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
                    Check(service.AllowsSpawn(enemy, center), "Disabled protection allows enemy spawn");
                }
                finally { result.DestroyObject(temporaryRules); }
            }
            Check(service.GetArea(TeamId.Unassigned) == null, "Invalid team rejected");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        result.Log("PASS: " + checks + " team spawn geometry, configuration and zone policy checks");
    }
}
