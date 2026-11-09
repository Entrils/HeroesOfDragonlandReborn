using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.World.Editor
{
    public static class InventoryEquipmentSceneBuilder
    {
        [MenuItem("Dragonland/Install Inventory and Equipment UI")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/Data/MultiplayerFortress/GameplayHudPanel.asset");
            if (settings == null) throw new InvalidOperationException("Install the gameplay HUD first.");
            const string path = "Assets/Scenes/MultiplayerFortressTest.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                MultiplayerBootstrap bootstrap = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent<MultiplayerBootstrap>(out var found)) bootstrap = found;
                if (bootstrap == null) throw new InvalidOperationException("Missing multiplayer bootstrap.");
                if (bootstrap.GetComponent<LocalGameplayInputGate>() == null) bootstrap.gameObject.AddComponent<LocalGameplayInputGate>();
                var ui = bootstrap.GetComponentInChildren<InventoryEquipmentPresenter>(true);
                if (ui == null)
                {
                    var go = new GameObject("Inventory and Equipment UI"); go.transform.SetParent(bootstrap.transform, false);
                    go.AddComponent<UIDocument>().panelSettings = settings;
                    go.AddComponent<InventoryEquipmentView>(); ui = go.AddComponent<InventoryEquipmentPresenter>();
                }
                var document = ui.GetComponent<UIDocument>(); document.panelSettings = settings; document.sortingOrder = 10;
                if (ui.GetComponent<InventoryUiAcceptanceProbe>() == null) ui.gameObject.AddComponent<InventoryUiAcceptanceProbe>();
                var serialized = new SerializedObject(ui); serialized.FindProperty("bootstrap").objectReferenceValue = bootstrap; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save inventory UI scene.");
                AssetDatabase.SaveAssets();
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
