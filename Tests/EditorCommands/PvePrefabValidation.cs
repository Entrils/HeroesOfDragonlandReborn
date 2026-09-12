using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        foreach (string name in new[] { "Wolf", "Bear", "Knight" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/MultiplayerPve/Network" + name + ".prefab");
            if (root == null) throw new Exception("Missing prefab " + name);
            foreach (string component in new[] { "NetworkObject", "NetworkTransform", "NetworkEnemy", "NetworkCharacterStats", "EnemyBrain", "EnemyNavigation", "EnemyPerception", "EnemyMeleeAttack", "NetworkEnemyPresentation" })
                if (root.GetComponent(component) == null) throw new Exception("Missing " + component);
            if (root.GetComponent<NavMeshAgent>().enabled || root.GetComponent<EnemyDebugVisual>().enabled)
                throw new Exception("Agent or offline presentation enabled on prefab " + name);
            var definition = root.GetComponent<EnemyBrain>().Definition;
            if (definition == null || definition.Stats == null
                || new SerializedObject(root.GetComponent<CharacterStats>()).FindProperty("configuration").objectReferenceValue != definition.Stats)
                throw new Exception("Stats definition mismatch");
            var view = new SerializedObject(root.GetComponent<NetworkEnemyPresentation>());
            if (view.FindProperty("body").objectReferenceValue == null || view.FindProperty("label").objectReferenceValue == null)
                throw new Exception("Missing presentation references");
            var baked = new SerializedObject(root.GetComponent("NetworkObject")).FindProperty("NetworkedBehaviours");
            foreach (string component in new[] { "NetworkTransform", "NetworkEnemy", "NetworkCharacterStats" })
            {
                bool found = false;
                for (int i = 0; i < baked.arraySize; i++) found |= baked.GetArrayElementAtIndex(i).objectReferenceValue == root.GetComponent(component);
                if (!found) throw new Exception("Unbaked " + component);
            }
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab");
        var threat = player.GetComponent<EnemyThreatTarget>();
        if (threat == null || new SerializedObject(threat).FindProperty("navigationOffset").vector3Value != Vector3.down)
            throw new Exception("Player threat/navigation point missing");
        result.Log("PASS: three PvE prefabs, shared definitions, disabled client agent, public stats, presentation and Fusion bake; player threat point");
    }
}
