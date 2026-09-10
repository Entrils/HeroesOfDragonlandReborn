using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Targeting;

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
            var dodgeType = typeof(NetworkActionPresentation).Assembly.GetType("HeroesOfDragonland.Multiplayer.NetworkPlayerDodge", true);
            var dodge = root.GetComponent(dodgeType);
            if (dodge == null) dodge = root.AddComponent(dodgeType);
            var fields = new SerializedObject(dodge);
            fields.FindProperty("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DodgeDefinition>("Assets/Game/Data/DefaultDodge.asset");
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (root.GetComponent<Targetable>() == null) root.AddComponent<Targetable>();
            if (root.GetComponent<NetworkLocalTargeting>() == null) root.AddComponent<NetworkLocalTargeting>();
            root.GetComponent<TargetLock>().enabled = false;
            var body = root.transform.Find("Body");
            // Keep the existing collider component/metadata, disabled. Physics stays on root.
            body.GetComponent<Collider>().enabled = false;
            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f; capsule.height = 2f; capsule.center = Vector3.zero;
            var pivot = root.transform.Find("Network Swing");
            if (pivot == null)
            {
                pivot = new GameObject("Network Swing").transform;
                result.RegisterObjectCreation(pivot.gameObject);
                pivot.SetParent(root.transform, false);
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                result.RegisterObjectCreation(blade);
                blade.name = "Practice Blade";
                blade.transform.SetParent(pivot, false);
                blade.transform.localPosition = new Vector3(0, 0.4f, 1.1f);
                blade.transform.localScale = new Vector3(0.15f, 0.15f, 1.6f);
                blade.GetComponent<Collider>().enabled = false;
                blade.GetComponent<Renderer>().sharedMaterial = body.GetComponent<Renderer>().sharedMaterial;
            }
            pivot.gameObject.SetActive(false);
            var view = root.GetComponent<NetworkActionPresentation>();
            if (view == null) view = root.AddComponent<NetworkActionPresentation>();
            fields = new SerializedObject(view);
            fields.FindProperty("body").objectReferenceValue = body;
            fields.FindProperty("swingPivot").objectReferenceValue = pivot;
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            result.Log("Saved network actions, local targeting, visual-only blade/body and authoritative root collider");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
