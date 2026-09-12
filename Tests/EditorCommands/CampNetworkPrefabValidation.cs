using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    public void Execute(ExecutionResult result)
    {
        HeroesOfDragonland.World.Editor.MultiplayerCampSceneBuilder.ValidateRegistration();
        const string folder = "Assets/Game/Data/MultiplayerCamps/";
        foreach (string name in new[] { "NetworkCampPlayer", "NetworkCampChest", "NetworkNeutralCamp" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
            Check(root != null, "Missing " + name);
            var obj = root.GetComponent("NetworkObject");
            Check(obj != null, "Missing NetworkObject: " + name);
            var baked = new SerializedObject(obj).FindProperty("NetworkedBehaviours");
            Check(baked.arraySize > 0, "Missing Fusion bake: " + name);
            string expected = name == "NetworkCampPlayer" ? "NetworkSessionProgression" : name == "NetworkCampChest" ? "NetworkLootChest" : "NetworkCamp";
            bool found = false;
            for (int i = 0; i < baked.arraySize; i++) found |= baked.GetArrayElementAtIndex(i).objectReferenceValue == root.GetComponent(expected);
            Check(found, "Unbaked " + expected);
        }
        var camp = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "NetworkNeutralCamp.prefab");
        var definition = (NetworkCampDefinition)new SerializedObject(camp.GetComponent("NetworkCamp")).FindProperty("definition").objectReferenceValue;
        Check(definition.TryValidate(out _), "Invalid network camp definition");
        Check(definition.Enemies.Count == 3 && definition.ExperienceReward == 100, "Camp authored members/reward");
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "NetworkCampPlayer.prefab");
        Check(player.GetComponent<SessionProgression>() != null && player.GetComponent("NetworkSessionProgression") != null, "Player progression missing");
        var settings = new SerializedObject(player.GetComponent<SessionProgression>());
        Check(!settings.FindProperty("hasAuthority").boolValue && settings.FindProperty("definition").objectReferenceValue != null, "Player progression configuration");
        var chest = AssetDatabase.LoadAssetAtPath<LootChest>(folder + "NetworkCampChest.prefab");
        Check(chest.Table != null && chest.GetComponent("NetworkLootChest") != null, "Network chest contract");
        Check(!new SerializedObject(chest).FindProperty("hasAuthority").boolValue, "Chest offline flag disabled");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerCampTest.unity", OpenSceneMode.Additive);
        try
        {
            MultiplayerBootstrap bootstrap = null;
            bool navigation = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<MultiplayerBootstrap>(out var found)) bootstrap = found;
                var surface = root.GetComponentInChildren<Unity.AI.Navigation.NavMeshSurface>();
                navigation |= surface != null && surface.navMeshData != null;
            }
            Check(navigation, "Saved NavMesh missing");
            Check(bootstrap != null && bootstrap.GetComponent<NetworkCampSpawner>().enabled, "Camp spawner missing");
            Check(!bootstrap.GetComponent<NetworkEnemySpawner>().enabled && !bootstrap.GetComponent<MultiplayerPveProbe>().enabled, "Duplicate enemy spawner/probe enabled");
            Check(new SerializedObject(bootstrap).FindProperty("playerPrefab").objectReferenceValue == player.GetComponent("NetworkObject"), "Scene player prefab mismatch");
            Check(bootstrap.GetComponent<MultiplayerCampProbe>() != null, "Camp probe missing");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        result.Log("PASS: " + checks + " network camp prefab/reference/bake/scene checks");
    }
}
