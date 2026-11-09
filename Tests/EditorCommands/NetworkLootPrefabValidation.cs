using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.World;
using HeroesOfDragonland.InventorySystem;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; }
    public void Execute(ExecutionResult result)
    {
        const string folder = "Assets/Game/Data/MultiplayerCamps/";
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "NetworkCampPlayer.prefab");
        Check(player.GetComponent<NetworkChestInteractor>() != null, "Saved local chest interactor");
        Check(player.GetComponent<Inventory>() != null && player.GetComponent("NetworkInventory") != null, "Authoritative inventory composition");
        var chest = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "NetworkCampChest.prefab");
        var loot = chest.GetComponent<LootChest>();
        Check(chest.GetComponent("NetworkLootChest") != null && chest.GetComponent("NetworkObject") != null, "Server chest composition");
        Check(loot.InteractionRange > 0 && loot.Table.TryValidate(player.GetComponent<Inventory>().Catalog, out _), "Range and catalog-compatible loot table");
        var visual = chest.GetComponent<LootChestVisual>();
        Check(visual != null && new SerializedObject(visual).FindProperty("lidPivot").objectReferenceValue != null, "Assigned replicated lid visual");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerCampTest.unity", OpenSceneMode.Additive);
        try
        {
            bool found = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<MultiplayerLootProbe>(out var probe))
                    found = new SerializedObject(probe).FindProperty("chestPrefab").objectReferenceValue == chest.GetComponent("NetworkLootChest");
            Check(found, "Saved opt-in probe chest reference");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        result.Log("PASS: " + checks + " network loot prefab/reference checks");
    }
}
