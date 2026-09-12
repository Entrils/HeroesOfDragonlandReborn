using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Multiplayer;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (Application.isPlaying) throw new Exception("Use Edit Mode");
        const string path = "Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            result.RegisterObjectModification(root);
            var inventory = root.GetComponent<Inventory>();
            if (inventory == null) inventory = root.AddComponent<Inventory>();
            var fields = new SerializedObject(inventory);
            fields.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
            fields.FindProperty("slotCount").intValue = 16;
            fields.FindProperty("hasAuthority").boolValue = false;
            fields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var type in new[] { typeof(Equipment), typeof(CharacterAttributes), typeof(NetworkInventory),
                typeof(NetworkEquipment), typeof(EquipmentVisualHooks), typeof(MultiplayerInventoryProbe) })
                if (root.GetComponent(type) == null) root.AddComponent(type);
            fields = new SerializedObject(root.GetComponent<MultiplayerInventoryProbe>());
            foreach (var pair in new[] { new[] { "sword", "IronSword" }, new[] { "potion", "ManaPotion" },
                new[] { "dragon", "Dragonblade" }, new[] { "armor", "ScoutArmor" } })
                fields.FindProperty(pair[0]).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/" + pair[1] + ".asset");
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        result.Log("Saved private inventory, public equipment, server attributes and visual hooks on network prefab");
    }
}
