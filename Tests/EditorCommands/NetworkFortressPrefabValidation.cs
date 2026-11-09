using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    public void Execute(ExecutionResult result)
    {
        const string folder = "Assets/Game/Data/MultiplayerFortress/";
        foreach (string name in new[] { "NetworkMatch", "TeamAKing", "TeamBKing", "TeamAGuard", "TeamBGuard" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
            Check(root != null, "Saved " + name);
            var obj = root.GetComponent("NetworkObject");
            Check(obj != null, "NetworkObject " + name);
            var baked = new SerializedObject(obj).FindProperty("NetworkedBehaviours");
            string expected = name == "NetworkMatch" ? "NetworkMatch" : name.EndsWith("King") ? "NetworkKingObjective" : "NetworkEnemy";
            bool found = false;
            for (int i = 0; i < baked.arraySize; i++) found |= baked.GetArrayElementAtIndex(i).objectReferenceValue == root.GetComponent(expected);
            Check(found, "Fusion bake " + expected);
            if (name == "NetworkMatch")
            {
                var data = new SerializedObject(root.GetComponent(expected));
                foreach (string reference in new[] { "teamAKingPrefab", "teamBKingPrefab", "teamAGuardPrefab", "teamBGuardPrefab" })
                    Check(data.FindProperty(reference).objectReferenceValue != null, "Match reference " + reference);
            }
            else
            {
                var identity = root.GetComponent<TeamIdentity>();
                Check(identity != null && identity.Team == (name.StartsWith("TeamA") ? TeamId.TeamA : TeamId.TeamB)
                    && identity.Kind == CombatantKind.NPC && !identity.AllowFriendlyFire, "Team identity " + name);
                Check(root.GetComponent("NetworkCharacterStats") != null, "Replicated health " + name);
                var material = root.GetComponentInChildren<Renderer>().sharedMaterial;
                Check(material != null && AssetDatabase.GetAssetPath(material).StartsWith("Assets/Art/"), "Saved fortress art " + name);
                if (name.EndsWith("King"))
                {
                    var profile = (CharacterStatsConfig)new SerializedObject(root.GetComponent<CharacterStats>()).FindProperty("configuration").objectReferenceValue;
                    Check(profile != null && profile.MaxHealth == 1000, "King health profile");
                    Check(root.GetComponent("KingObjective") == null, "No offline match adapter");
                }
                else
                {
                    Check(root.GetComponent<NetworkFortressGuard>() != null, "Guard team policy");
                    Check(!root.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "Saved agent disabled on clients");
                }
            }
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerFortressTest.unity", OpenSceneMode.Additive);
        try
        {
            bool found = false, navigation = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<NetworkFortressSpawner>(out var spawner))
                {
                    found = spawner.enabled && new SerializedObject(spawner).FindProperty("matchPrefab").objectReferenceValue != null;
                    Check(root.GetComponent<NetworkFortressPresentation>() != null && root.GetComponent<MultiplayerFortressProbe>() != null, "UI and opt-in acceptance probe");
                    Check(!root.GetComponent<MultiplayerCampProbe>().enabled && !root.GetComponent<MultiplayerLootProbe>().enabled, "Other probes disabled");
                }
                var surface = root.GetComponentInChildren<Unity.AI.Navigation.NavMeshSurface>();
                navigation |= surface != null && surface.navMeshData != null;
            }
            Check(found, "Saved fortress spawner"); Check(navigation, "Saved NavMesh");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        result.Log("PASS: " + checks + " fortress prefab/reference checks");
    }
}
