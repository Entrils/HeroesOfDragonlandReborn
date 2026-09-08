using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.World.Camps;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) throw new Exception("Use Edit Mode");
        var player = GameObject.Find("Player");
        if (player.GetComponent<SessionProgression>() != null) throw new Exception("Progression already configured; inspect instead of duplicating.");
        if (!AssetDatabase.IsValidFolder("Assets/Game/Data/Progression")) AssetDatabase.CreateFolder("Assets/Game/Data", "Progression");
        var definition = ScriptableObject.CreateInstance<LevelProgressionDefinition>();
        var data = new SerializedObject(definition);
        var levels = data.FindProperty("levels");
        int[] xp = { 100, 150, 250, 400, 600 };
        int[] points = { 1, 2, 2, 3, 3 };
        levels.arraySize = xp.Length;
        for (int i = 0; i < xp.Length; i++)
        {
            var step = levels.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("requiredXP").intValue = xp[i];
            step.FindPropertyRelative("progressionPoints").intValue = points[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        if (!definition.TryValidate(out string reason)) throw new Exception(reason);
        AssetDatabase.CreateAsset(definition, "Assets/Game/Data/Progression/DefaultLevelProgression.asset");
        result.RegisterObjectCreation(definition);
        result.RegisterObjectModification(player);
        var progression = Undo.AddComponent<SessionProgression>(player);
        var config = new SerializedObject(progression);
        config.FindProperty("definition").objectReferenceValue = definition;
        config.ApplyModifiedPropertiesWithoutUndo();
        Undo.AddComponent<SessionProgressionDebug>(player);

        var sessionObject = new GameObject("Session Progression");
        result.RegisterObjectCreation(sessionObject);
        var session = sessionObject.AddComponent<ProgressionSession>();
        var roster = new SerializedObject(session);
        roster.FindProperty("players").arraySize = 1;
        roster.FindProperty("players").GetArrayElementAtIndex(0).objectReferenceValue = progression;
        roster.ApplyModifiedPropertiesWithoutUndo();
        var camp = GameObject.Find("Test Neutral Camp");
        result.RegisterObjectModification(camp);
        var source = Undo.AddComponent<CampExperienceSource>(camp);
        var sourceConfig = new SerializedObject(source);
        sourceConfig.FindProperty("recipients").arraySize = 1;
        sourceConfig.FindProperty("recipients").GetArrayElementAtIndex(0).objectReferenceValue = progression;
        sourceConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.scene);
        result.Log("Configured session roster, player progression, six-level curve and camp XP recipient.");
    }
}
