using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.World.Editor
{
    public static class SessionSkillsSceneBuilder
    {
        public const string Folder = "Assets/Game/Data/Skills";
        public const string PlayerPath = "Assets/Game/Data/MultiplayerFortress/NetworkFortressPlayer.prefab";
        [MenuItem("Dragonland/Install Session Skills")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Data", "Skills");
            var definitions = new[] {
                Create("PowerStrike", "Power Strike", "A Mana-powered melee swing dealing twice normal damage.", SkillEffect.MeleeStrike, 1, Items.ItemStat.Strength, 10, 20, 5, 2),
                Create("Guard", "Guard", "Become invulnerable for 1.5 seconds. Death ends the guard.", SkillEffect.Guard, 2, Items.ItemStat.Mind, 10, 25, 10, 1.5f),
                Create("MeleeTraining", "Melee Training", "Passively add 5 damage to melee hits while requirements are met.", SkillEffect.MeleeTraining, 2, Items.ItemStat.Vitality, 12, 0, 0, 5)
            };
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var skills = root.GetComponent<SessionSkills>() ?? root.AddComponent<SessionSkills>();
                var data = new SerializedObject(skills); var entries = data.FindProperty("definitions"); entries.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                if (root.GetComponent<NetworkSessionSkills>() == null) root.AddComponent<NetworkSessionSkills>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MultiplayerFortressSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MultiplayerFortressSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                MultiplayerBootstrap bootstrap = null;
                foreach (var obj in scene.GetRootGameObjects()) if (obj.TryGetComponent<MultiplayerBootstrap>(out var candidate)) bootstrap = candidate;
                if (bootstrap == null) throw new InvalidOperationException("Missing bootstrap.");
                var bootstrapData = new SerializedObject(bootstrap);
                if (AssetDatabase.GetAssetPath(bootstrapData.FindProperty("playerPrefab").objectReferenceValue) != PlayerPath)
                    throw new InvalidOperationException("The scene uses a different player prefab. Install skills on its actual spawn prefab.");
                var panel = bootstrap.GetComponentInChildren<SessionSkillsPanel>(true);
                if (panel == null)
                {
                    var go = new GameObject("Session Skills UI"); go.transform.SetParent(bootstrap.transform, false);
                    go.AddComponent<UIDocument>(); panel = go.AddComponent<SessionSkillsPanel>();
                }
                var document = panel.GetComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/Data/MultiplayerFortress/GameplayHudPanel.asset");
                if (document.panelSettings == null) throw new InvalidOperationException("Missing gameplay UI panel settings.");
                document.sortingOrder = 11;
                var serialized = new SerializedObject(panel); serialized.FindProperty("bootstrap").objectReferenceValue = bootstrap; serialized.ApplyModifiedPropertiesWithoutUndo();
                if (bootstrap.GetComponent<SessionSkillsAcceptanceProbe>() == null) bootstrap.gameObject.AddComponent<SessionSkillsAcceptanceProbe>();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save skills scene.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            AssetDatabase.SaveAssets();
        }
        private static SkillDefinition Create(string fileName, string title, string description, SkillEffect effect,
            int level, Items.ItemStat stat, float minimum, float mana, float cooldown, float magnitude)
        {
            string path = Folder + "/" + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (existing != null) { if (!existing.IsValid) throw new InvalidOperationException("Invalid skill: " + path); return existing; }
            var definition = ScriptableObject.CreateInstance<SkillDefinition>(); var data = new SerializedObject(definition);
            data.FindProperty("displayName").stringValue = title; data.FindProperty("description").stringValue = description;
            data.FindProperty("effect").enumValueIndex = (int)effect; data.FindProperty("requiredLevel").intValue = level;
            data.FindProperty("manaCost").floatValue = mana; data.FindProperty("cooldown").floatValue = cooldown; data.FindProperty("magnitude").floatValue = magnitude;
            var requirements = data.FindProperty("requirements"); requirements.arraySize = 1;
            requirements.GetArrayElementAtIndex(0).FindPropertyRelative("stat").enumValueIndex = (int)stat;
            requirements.GetArrayElementAtIndex(0).FindPropertyRelative("minimumValue").floatValue = minimum;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!definition.IsValid) throw new InvalidOperationException("Invalid skill defaults.");
            AssetDatabase.CreateAsset(definition, path); return definition;
        }
    }
}
