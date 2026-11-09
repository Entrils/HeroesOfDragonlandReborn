using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.UI;
using HeroesOfDragonland.World.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (Application.isPlaying) throw new Exception("Use Edit Mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionSkillsSceneBuilder.PlayerPath);
        if (prefab == null || prefab.GetComponent<SessionSkills>() == null || prefab.GetComponent("NetworkSessionSkills") == null
            || prefab.GetComponent("NetworkSessionProgression") == null || prefab.GetComponent<SessionProgression>() == null)
            throw new Exception("Missing skill/progression components on the spawn prefab.");
        var definitions = new SerializedObject(prefab.GetComponent<SessionSkills>()).FindProperty("definitions");
        if (definitions.arraySize != 3) throw new Exception("Expected exactly three skills.");
        for (int i = 0; i < 3; i++)
            if (!(definitions.GetArrayElementAtIndex(i).objectReferenceValue is SkillDefinition skill) || !skill.IsValid)
                throw new Exception("Missing or invalid skill definition " + i);
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MultiplayerFortressSceneBuilder.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(MultiplayerFortressSceneBuilder.ScenePath, OpenSceneMode.Additive);
        int count = 0;
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var panel in root.GetComponentsInChildren<SessionSkillsPanel>(true))
                {
                    count++;
                    var bootstrap = panel.GetComponentInParent<MultiplayerBootstrap>();
                    if (bootstrap == null || bootstrap.GetComponent<LocalGameplayInputGate>() == null
                        || bootstrap.GetComponent<SessionSkillsAcceptanceProbe>() == null
                        || new SerializedObject(panel).FindProperty("bootstrap").objectReferenceValue != bootstrap)
                        throw new Exception("Skill panel must reference its bootstrap, gate and opt-in test probe.");
                    var player = new SerializedObject(bootstrap).FindProperty("playerPrefab").objectReferenceValue as Component;
                    if (player == null || AssetDatabase.GetAssetPath(player) != SessionSkillsSceneBuilder.PlayerPath
                        || player.GetComponent("NetworkSessionSkills") == null)
                        throw new Exception("Scene must spawn the authored skill prefab.");
                    var document = panel.GetComponent<UIDocument>();
                    if (document.panelSettings == null || document.panelSettings.themeStyleSheet == null || document.sortingOrder != 11)
                        throw new Exception("Missing skill theme or overlay order.");
                }
            if (count != 1) throw new Exception("Expected exactly one skill panel.");
            result.Log("PASS: actual spawn prefab, network/progression components, exactly three definitions and skill UI references.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
