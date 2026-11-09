using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfDragonland.Multiplayer;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    public void Execute(ExecutionResult result)
    {
        // Every legal edge, every skipped/backwards edge and duplicate transitions.
        var allowed = new bool[6,6];
        allowed[0,1] = allowed[1,2] = allowed[2,3] = allowed[2,5] = true;
        allowed[3,4] = allowed[3,5] = allowed[4,5] = allowed[5,1] = true;
        for (int from = 0; from < 6; from++)
            for (int to = 0; to < 6; to++)
                Check(MatchLifecycleRules.CanTransition((MatchPhase)from, (MatchPhase)to) == allowed[from,to], "Transition " + from + " -> " + to);
        Check(!MatchLifecycleRules.CanTransition((MatchPhase)99, MatchPhase.Playing), "Unknown phase rejected");
        for (int minimum = 2; minimum <= 6; minimum++)
        {
            Check(!MatchLifecycleRules.HasEnoughPlayers(0, 3, minimum), "Team A required");
            Check(!MatchLifecycleRules.HasEnoughPlayers(3, 0, minimum), "Team B required");
            Check(MatchLifecycleRules.HasEnoughPlayers(3, 3, minimum), "Full lobby starts");
        }
        Check(!MatchLifecycleRules.HasEnoughPlayers(1, 1, 3), "Minimum enforced");
        Check(MatchLifecycleRules.HasEnoughPlayers(1, 1, 2), "Two-team minimum");
        const string folder = "Assets/Game/Data/MultiplayerFortress/";
        var config = AssetDatabase.LoadAssetAtPath<MatchLifecycleConfig>(folder + "MatchLifecycle.asset");
        Check(config != null, "Saved lifecycle configuration");
        Check(config.MinimumPlayers >= 2 && config.MinimumPlayers <= 6, "Config player bounds");
        Check(config.StartingDelay >= 0 && config.FinishedDelay >= 0 && config.ReturningDelay >= 0, "Config timers");
        var temporary = UnityEngine.Object.Instantiate(config);
        try
        {
            var data = new SerializedObject(temporary);
            data.FindProperty("minimumPlayers").intValue = -10;
            data.FindProperty("startingDelay").floatValue = -1;
            data.FindProperty("finishedDelay").floatValue = float.NaN;
            data.FindProperty("returningDelay").floatValue = float.PositiveInfinity;
            data.ApplyModifiedPropertiesWithoutUndo();
            Check(temporary.MinimumPlayers == 2 && temporary.StartingDelay == 0, "Invalid lower bounds clamped");
            Check(!float.IsNaN(temporary.FinishedDelay) && !float.IsInfinity(temporary.ReturningDelay), "Invalid timers cannot stall match");
        }
        finally { result.DestroyObject(temporary); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "NetworkMatch.prefab");
        Check(prefab != null && new SerializedObject(prefab.GetComponent("NetworkMatch")).FindProperty("lifecycle").objectReferenceValue == config, "Match prefab config reference");
        var baked = new SerializedObject(prefab.GetComponent("NetworkObject")).FindProperty("NetworkedBehaviours");
        bool found = false;
        for (int i = 0; i < baked.arraySize; i++) found |= baked.GetArrayElementAtIndex(i).objectReferenceValue == prefab.GetComponent("NetworkMatch");
        Check(found, "Match Fusion bake");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerFortressTest.unity", OpenSceneMode.Additive);
        try
        {
            found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.TryGetComponent<NetworkFortressSpawner>(out var fortress)) continue;
                found = true;
                Check(root.GetComponent<NetworkRoundReset>() != null, "Round reset service installed");
                Check(root.GetComponent<TeamSpawnService>() != null, "Shared spawn service installed");
                Check(root.GetComponent<NetworkCampSpawner>() != null && root.GetComponent<NetworkEnemySpawner>() != null, "World factories available");
                Check(root.GetComponent<NetworkFortressPresentation>() != null, "Lobby and winner UI installed");
                Check(root.GetComponent<MultiplayerLifecycleProbe>() != null, "Opt-in acceptance probe installed");
                var data = new SerializedObject(fortress);
                Check(data.FindProperty("matchPrefab").objectReferenceValue == prefab.GetComponent("NetworkMatch"), "Scene match reference");
            }
            Check(found, "Fortress scene contains lifecycle root");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        result.Log("PASS: " + checks + " match lifecycle transition, configuration and saved integration checks");
    }
}
