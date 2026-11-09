using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    public void Execute(ExecutionResult result)
    {
        const string folder = "Assets/Game/Data/MultiplayerFortress/";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "NetworkFortressPlayer.prefab");
        Check(prefab != null && prefab.GetComponent("NetworkPlayerRespawn") != null, "Saved respawn component");
        var networkObject = prefab.GetComponent("NetworkObject");
        var baked = new SerializedObject(networkObject).FindProperty("NetworkedBehaviours");
        bool found = false;
        for (int i = 0; i < baked.arraySize; i++) found |= baked.GetArrayElementAtIndex(i).objectReferenceValue == prefab.GetComponent("NetworkPlayerRespawn");
        Check(found, "Respawn Fusion bake");
        Check(!new SerializedObject(prefab.GetComponent("DamageReceiver")).FindProperty("deactivateOnDeath").boolValue, "Death keeps avatar active");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerFortressTest.unity", OpenSceneMode.Additive);
        try
        {
            found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.TryGetComponent<NetworkFortressSpawner>(out var fortress)) continue;
                found = true;
                Check(fortress.RespawnConfig != null && fortress.RespawnConfig.Delay == 5 && fortress.RespawnConfig.ProtectionDuration == 3, "Saved timing defaults");
                Check(new SerializedObject(root.GetComponent<MultiplayerBootstrap>()).FindProperty("playerPrefab").objectReferenceValue == networkObject, "Scene uses respawn avatar");
                for (int lane = 0; lane < 3; lane++)
                {
                    Check(root.GetComponent<TeamSpawnService>().GetArea(HeroesOfDragonland.Teams.TeamId.TeamA).TryGetPoint(lane, out var a, out _) && a.x < -10, "Team A fortress");
                    Check(root.GetComponent<TeamSpawnService>().GetArea(HeroesOfDragonland.Teams.TeamId.TeamB).TryGetPoint(lane, out var b, out _) && b.x > 10, "Team B fortress");
                }
                Check(root.GetComponent<TeamSpawnService>().GetArea(HeroesOfDragonland.Teams.TeamId.Unassigned) == null, "Invalid team rejected");
            }
            Check(found, "Fortress spawner exists");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        foreach (RespawnResourcePolicy mana in Enum.GetValues(typeof(RespawnResourcePolicy)))
            foreach (RespawnResourcePolicy stamina in Enum.GetValues(typeof(RespawnResourcePolicy)))
            {
                var actor = new GameObject("Temporary respawn policy check");
                result.RegisterObjectCreation(actor);
                try
                {
                    var stats = actor.AddComponent<CharacterStats>();
                    stats.TrySpendMana(25); stats.TrySpendStamina(40); stats.ApplyDamage(stats.MaxHealth);
                    stats.RestoreForRespawn(mana, stamina);
                    Check(stats.CurrentHealth == stats.MaxHealth && !stats.IsDead, "Revive full health");
                    Check(stats.CurrentMana == (mana == RespawnResourcePolicy.Full ? 100 : mana == RespawnResourcePolicy.Empty ? 0 : 75), "Mana " + mana);
                    Check(stats.CurrentStamina == (stamina == RespawnResourcePolicy.Full ? 100 : stamina == RespawnResourcePolicy.Empty ? 0 : 60), "Stamina " + stamina);
                }
                finally { result.DestroyObject(actor); }
            }
        result.Log("PASS: " + checks + " respawn asset, team pose and resource policy checks");
    }
}
