using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        const string path = "Assets/Scenes/MultiplayerFortressTest.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        int hudCount = 0;
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var presenter in root.GetComponentsInChildren<GameplayHudPresenter>(true))
                {
                    hudCount++;
                    var bootstrap = presenter.GetComponentInParent<MultiplayerBootstrap>();
                    if (bootstrap == null || new SerializedObject(presenter).FindProperty("bootstrap").objectReferenceValue != bootstrap)
                        throw new Exception("HUD must reference its scene bootstrap");
                    var document = presenter.GetComponent<UIDocument>();
                    if (document == null || document.panelSettings == null || document.panelSettings.themeStyleSheet == null)
                        throw new Exception("Saved HUD document, scale settings and theme required");
                    if (document.panelSettings.scaleMode != PanelScaleMode.ScaleWithScreenSize)
                        throw new Exception("HUD must scale with screen size");
                    if (!presenter.enabled || presenter.GetComponent<GameplayHudView>() == null || presenter.GetComponent<GameplayHudAcceptanceProbe>() == null)
                        throw new Exception("HUD view/presenter/acceptance composition incomplete");
                    if (bootstrap.GetComponent<NetworkFortressPresentation>().enabled)
                        throw new Exception("Legacy objective UI overlaps HUD");
                    if (!bootstrap.GetComponent<MultiplayerTestPanel>().enabled)
                        throw new Exception("Connection controls must remain available");
                }
            if (hudCount != 1) throw new Exception("Exactly one scene HUD required");
            result.Log("PASS: saved HUD composition, bootstrap reference, theme, scaling and connection controls");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
