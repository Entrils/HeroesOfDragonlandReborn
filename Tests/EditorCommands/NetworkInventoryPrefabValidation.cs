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
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab");
        if (root == null) throw new Exception("Missing network prefab");
        foreach (var type in new[] { typeof(Inventory), typeof(Equipment), typeof(CharacterAttributes),
            typeof(NetworkInventory), typeof(NetworkEquipment), typeof(EquipmentVisualHooks) })
            if (root.GetComponent(type) == null) throw new Exception("Missing " + type.Name);
        var inventory = root.GetComponent<Inventory>();
        if (inventory.Catalog == null || !inventory.Catalog.TryValidate(out _)) throw new Exception("Invalid catalog");
        int slots = new SerializedObject(inventory).FindProperty("slotCount").intValue;
        if (slots < 1 || slots > 32) throw new Exception("Network capacity mismatch");
        if (new SerializedObject(inventory).FindProperty("hasAuthority").boolValue) throw new Exception("Offline authority enabled");
        var baked = new SerializedObject(root.GetComponent("NetworkObject")).FindProperty("NetworkedBehaviours");
        foreach (string name in new[] { "NetworkInventory", "NetworkEquipment" })
        {
            bool found = false;
            for (int i = 0; i < baked.arraySize; i++) found |= baked.GetArrayElementAtIndex(i).objectReferenceValue == root.GetComponent(name);
            if (!found) throw new Exception("Unbaked network behaviour " + name);
        }
        result.Log("PASS: inventory catalog/capacity, equipment attributes/hooks, disabled offline authority and Fusion prefab bake");
    }
}
