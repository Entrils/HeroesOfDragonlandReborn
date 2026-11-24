using System;
using System.Linq;
using Fusion;
using Fusion.Editor;
using HeroesOfDragonland.Presentation.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    public static class IntegrationBakeValidation
    {
        public static void BakeAndSaveLoadedScene()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Bake in Edit Mode.");
            var scene=SceneManager.GetActiveScene();
            if(UnityEngine.Object.FindObjectsByType<VfxPool>(FindObjectsInactive.Include).Length!=1)
                throw new InvalidOperationException("Expected exactly one scene VFX pool.");
            int count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Data"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var impact=root.GetComponent<NetworkImpactVfx>();
                if(impact==null)continue;
                var network=root.GetComponent<NetworkObject>();
                if(network==null||!network.NetworkedBehaviours.Contains(impact))
                    throw new InvalidOperationException("Unbaked impact behavior: "+path);
                count++;
            }
            NetworkObjectPostprocessor.BakeScene(scene);
            foreach(var network in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkObject>(true)))
            {
                var impact=network.GetComponent<NetworkImpactVfx>();
                if(impact!=null&&!network.NetworkedBehaviours.Contains(impact))throw new InvalidOperationException("Unbaked scene impact behavior.");
            }
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Save failed.");
            AssetDatabase.SaveAssets();Debug.Log("[Integration] Verified impact behavior baking on "+count+" prefabs; baked and saved "+scene.path);
        }
    }
}
