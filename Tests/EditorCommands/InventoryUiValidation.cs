using System;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
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
        int count = 0;
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var ui in root.GetComponentsInChildren<InventoryEquipmentPresenter>(true))
                {
                    count++;
                    var bootstrap = ui.GetComponentInParent<MultiplayerBootstrap>();
                    if (bootstrap == null || bootstrap.GetComponent<LocalGameplayInputGate>() == null
                        || new SerializedObject(ui).FindProperty("bootstrap").objectReferenceValue != bootstrap)
                        throw new Exception("Inventory UI must have its own bootstrap and input gate");
                    if (ui.GetComponent<InventoryEquipmentView>() == null || ui.GetComponent<InventoryUiAcceptanceProbe>() == null)
                        throw new Exception("View and opt-in validation missing");
                    var document = ui.GetComponent<UIDocument>();
                    if (document.panelSettings == null || document.panelSettings.themeStyleSheet == null || document.sortingOrder != 10)
                        throw new Exception("Inventory theme or overlay ordering missing");
                }
            if (count != 1) throw new Exception("Expected exactly one inventory UI");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }

        var actor = new GameObject("Temporary inventory UI requirement validation");
        actor.SetActive(false);
        result.RegisterObjectCreation(actor);
        var item = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/Dragonblade.asset"));
        try
        {
            actor.AddComponent<CharacterAttributes>();
            var equipment = actor.AddComponent<Equipment>();
            var data = new SerializedObject(item);
            var requirements = data.FindProperty("equipmentRequirements"); requirements.arraySize = 2;
            requirements.GetArrayElementAtIndex(1).FindPropertyRelative("stat").enumValueIndex = (int)ItemStat.Dexterity;
            requirements.GetArrayElementAtIndex(1).FindPropertyRelative("minimumValue").floatValue = 30;
            data.ApplyModifiedPropertiesWithoutUndo();
            string text = InventoryItemText.Describe(item, 1, equipment, out bool met);
            if (met || !text.Contains("MISSING  Strength") || !text.Contains("MISSING  Dexterity")
                || !text.Contains("need +10") || !text.Contains("need +20"))
                throw new Exception("Must list every missing attribute, including exact deficits");
            if (!equipment.TryGetRequirementValue(ItemStat.Strength, out float strength) || strength != 10)
                throw new Exception("Preview must use canonical requirement source");
            var potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ManaPotion.asset");
            if (!InventoryItemText.Describe(potion, 3, equipment, out _).Contains("Use unavailable"))
                throw new Exception("Unsupported consumables must be explained");
            result.Log("PASS: saved inventory UI composition, input gate, overlay, all missing requirements and unsupported consumable details");
        }
        finally { result.DestroyObject(item); result.DestroyObject(actor); }
    }
}
