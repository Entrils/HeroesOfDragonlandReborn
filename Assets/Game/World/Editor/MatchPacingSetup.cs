using System;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.World.Editor
{
    public static class MatchPacingSetup
    {
        public const string ConfigPath = "Assets/Game/Data/Battlefield/MatchBalance.asset";
        [MenuItem("Heroes of Dragonland/Battlefield/Configure Match Pacing")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != BattlefieldSceneBuilder.ScenePath) throw new InvalidOperationException("Open the battlefield in Edit Mode.");
            var config = AssetDatabase.LoadAssetAtPath<MatchBalanceConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MatchBalanceConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                var data = new SerializedObject(config);
                data.FindProperty("progression").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelProgressionDefinition>("Assets/Game/Data/Progression/DefaultLevelProgression.asset");
                var profiles = data.FindProperty("camps"); profiles.arraySize = 4;
                int[] xp = { 25, 60, 120, 360 };
                float[] first = { 0, 0, 150, 150 }, repeat = { 75, 105, 165, 210 }, lateRepeat = { 75, 75, 120, 150 }, multiplier = { 1, 2, 1.5f, 1.5f };
                for (int i = 0; i < 4; i++)
                {
                    var entry = profiles.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("difficulty").intValue = i;
                    entry.FindPropertyRelative("experience").intValue = xp[i];
                    entry.FindPropertyRelative("firstSpawnSeconds").floatValue = first[i];
                    entry.FindPropertyRelative("respawnSeconds").floatValue = repeat[i];
                    entry.FindPropertyRelative("lateRespawnSeconds").floatValue = lateRepeat[i];
                    entry.FindPropertyRelative("rewardRadius").floatValue = i == 3 ? 20 : 9;
                    entry.FindPropertyRelative("lateExperienceMultiplier").floatValue = multiplier[i];
                }
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(config);
            }
            if (!config.TryValidate(out var reason)) throw new InvalidOperationException(reason);
            EditPrefab("Assets/Game/Data/Battlefield/BattlefieldMatch.prefab", root => {
                var match = root.GetComponent<NetworkMatch>();
                var data = new SerializedObject(match); data.FindProperty("balance").objectReferenceValue = config; data.ApplyModifiedPropertiesWithoutUndo();
                if (root.GetComponent<NetworkMatchPacing>() == null) root.AddComponent<NetworkMatchPacing>();
            });
            EditPrefab("Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab", root => {
                if (root.GetComponent<NetworkPlayerExperienceRewards>() == null) root.AddComponent<NetworkPlayerExperienceRewards>();
                var data = new SerializedObject(root.GetComponent<SessionProgression>());
                data.FindProperty("definition").objectReferenceValue = config.Progression; data.ApplyModifiedPropertiesWithoutUndo();
            });
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            var hud = UnityEngine.Object.FindAnyObjectByType<GameplayHudView>();
            if (bootstrap == null || hud == null) throw new InvalidOperationException("Missing battlefield bootstrap/HUD.");
            var presenter = hud.GetComponent<MatchPacingPresenter>() ?? Undo.AddComponent<MatchPacingPresenter>(hud.gameObject);
            var presenterData = new SerializedObject(presenter); presenterData.FindProperty("bootstrap").objectReferenceValue = bootstrap; presenterData.ApplyModifiedPropertiesWithoutUndo();
            // Retire stale hardcoded map reward signs; live camp labels and the HUD read the config.
            foreach (var label in UnityEngine.Object.FindObjectsByType<TextMesh>())
                if (label.transform.parent != null && label.transform.parent.name.StartsWith("Camp "))
                {
                    Undo.RecordObject(label, "Update objective sign");
                    label.text = label.text.Contains("HOME") ? "HOME CAMP" : label.text.Contains("FLANK") ? "FLANK CAMP / LATE SURGE" : "CONTESTED OBJECTIVE";
                    EditorUtility.SetDirty(label);
                }
            AssetDatabase.SaveAssets(); Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset")) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[MatchPacing] Saved centralized balance, authoritative player rewards, camp pacing and public HUD.");
        }
        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
