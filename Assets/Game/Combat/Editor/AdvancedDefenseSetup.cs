using System;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Combat.Editor
{
    public static class AdvancedDefenseSetup
    {
        public const string DefinitionPath = "Assets/Game/Data/DefaultParry.asset";
        [MenuItem("Heroes of Dragonland/Combat/Build Advanced Defense")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            var definition = AssetDatabase.LoadAssetAtPath<ParryDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ParryDefinition>(); AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentInChildren<CombatDefense>(true) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Configure(root, definition); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i); bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= Configure(root, definition);
                if (changed && !string.IsNullOrEmpty(scene.path)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterStatsConfig", new[] { "Assets/Game/Data" }))
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<CharacterStatsConfig>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("[AdvancedDefense] Saved defense/poise components, player parry references, stat migration and Fusion prefabs.");
        }
        private static bool Configure(GameObject root, ParryDefinition definition)
        {
            bool changed = false;
            foreach (var defense in root.GetComponentsInChildren<CombatDefense>(true))
            {
                if (defense.GetComponent<CharacterPoise>() == null) { Undo.AddComponent<CharacterPoise>(defense.gameObject); changed = true; }
                if (defense.GetComponent<MeleeAttack>() == null) continue;
                var parry = defense.GetComponent<ParryDefense>();
                if (parry == null) { parry = Undo.AddComponent<ParryDefense>(defense.gameObject); changed = true; }
                var data = new SerializedObject(parry);
                if (data.FindProperty("definition").objectReferenceValue == null)
                {
                    data.FindProperty("definition").objectReferenceValue = definition; data.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(parry); changed = true;
                }
            }
            return changed;
        }
    }
}
