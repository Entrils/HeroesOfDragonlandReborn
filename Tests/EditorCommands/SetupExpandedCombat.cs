using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HeroesOfDragonland.Combat;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (Application.isPlaying) throw new Exception("Use Edit Mode");
        int added = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = root.GetComponentInChildren<DamageReceiver>(true) != null;
                foreach (var receiver in root.GetComponentsInChildren<DamageReceiver>(true))
                    if (receiver.GetComponent<CombatDefense>() == null)
                    { receiver.gameObject.AddComponent<CombatDefense>(); changed = true; added++; }
                if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); result.Log("Updated " + path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<DamageReceiver>(true) != null) changed = true;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var receiver in root.GetComponentsInChildren<DamageReceiver>(true))
                        if (receiver.GetComponent<CombatDefense>() == null)
                        { Undo.AddComponent<CombatDefense>(receiver.gameObject); changed = true; added++; }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); result.Log("Updated " + path); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        var active = SceneManager.GetActiveScene();
        result.Log("Active hierarchy: " + active.path + "; roots=" + active.rootCount + "; added defenses=" + added);
    }
}
