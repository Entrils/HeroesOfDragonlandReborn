using System;
using System.Linq;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Targeting;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class HumanoidPrefabSetup
    {
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        {
            var data=new SerializedObject(target); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("Heroes of Dragonland/Characters/Build Humanoid Visual Pipeline")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            var avatar=HumanoidAssetBuilder.BuildRigAndClips(out var clips);
            var controller=ProductionCharacterAnimatorBuilder.Build(clips);
            if(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath)==null)
            {
                var wrapper=new GameObject("CharacterVisual");
                try
                {
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.ModelPath));
                    model.transform.SetParent(wrapper.transform,false);
                    var animator=model.GetComponent<Animator>(); if(animator==null)animator=model.AddComponent<Animator>();
                    animator.avatar=avatar; animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    if(renderers.Length==0)throw new InvalidOperationException("Imported character must have a weighted SkinnedMeshRenderer.");
                    foreach(var renderer in renderers)
                    {
                        renderer.sharedMaterials=new[]{HumanoidAssetBuilder.Material("M_PrototypeHumanoid",new Color(.65f,.7f,.75f)),HumanoidAssetBuilder.Material("M_PrototypeHumanoidDark",new Color(.035f,.045f,.06f))};
                        renderer.updateWhenOffscreen=true;
                        renderer.localBounds=new Bounds(new Vector3(0,.9f,.3f),new Vector3(2.2f,2.1f,2.5f));
                    }
                    var visual=wrapper.AddComponent<CharacterVisual>(); Set(visual,"animator",animator);
                    var data=new SerializedObject(visual); var bodies=data.FindProperty("bodyRenderers"); bodies.arraySize=renderers.Length;
                    for(int i=0;i<renderers.Length;i++)bodies.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
                    data.ApplyModifiedPropertiesWithoutUndo();
                    Set(visual,"rightHand",Socket(animator,HumanBodyBones.RightHand,"Socket_RightHand",Vector3.zero,Quaternion.identity));
                    Set(visual,"leftHand",Socket(animator,HumanBodyBones.LeftHand,"Socket_LeftHand",Vector3.zero,Quaternion.identity));
                    Set(visual,"head",Socket(animator,HumanBodyBones.Head,"Socket_Head",new Vector3(0,.25f,0),Quaternion.identity));
                    Set(visual,"chest",Socket(animator,HumanBodyBones.Chest,"Socket_Chest",Vector3.zero,Quaternion.identity));
                    var back=Socket(animator,HumanBodyBones.Chest,"Socket_Back",Vector3.zero,Quaternion.identity);
                    back.position=wrapper.transform.TransformPoint(new Vector3(0,1.25f,-.17f)); back.rotation=Quaternion.Euler(-90,0,-20);
                    Set(visual,"back",back);
                    PrefabUtility.SaveAsPrefabAsset(wrapper,HumanoidAssetBuilder.VisualPath);
                }
                finally{UnityEngine.Object.DestroyImmediate(wrapper);}
            }
            var visualRoot=PrefabUtility.LoadPrefabContents(HumanoidAssetBuilder.VisualPath);
            try
            {
                var visual=visualRoot.GetComponent<CharacterVisual>();visual.Animator.runtimeAnimatorController=controller;visual.Animator.applyRootMotion=false;
                Set(visual,"animationLibrary",AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrary>(ProductionCharacterAnimatorBuilder.LibraryPath));
                if(visual.Animator.GetComponent<CharacterAnimationEvents>()==null)visual.Animator.gameObject.AddComponent<CharacterAnimationEvents>();
                PrefabUtility.SaveAsPrefabAsset(visualRoot,HumanoidAssetBuilder.VisualPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(visualRoot);}
            int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Data"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab.GetComponent<NetworkTestPlayer>()==null)continue;
                InstallPlayer(path); count++;
            }
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable(); AssetDatabase.SaveAssets();
            Debug.Log("[HumanoidPipeline] Saved valid Humanoid Avatar, production animation modules, five sockets and visual setup on "+count+" network player prefabs.");
        }
        private static Transform Socket(Animator animator,HumanBodyBones bone,string name,Vector3 offset,Quaternion rotation)
        {
            var parent=animator.GetBoneTransform(bone); if(parent==null)throw new InvalidOperationException("Missing humanoid bone "+bone);
            var socket=new GameObject(name).transform; socket.SetParent(parent,false); socket.localPosition=offset;
            socket.rotation=rotation; return socket;
        }
        private static void InstallPlayer(string path)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual=root.GetComponentInChildren<CharacterVisual>(true);
                if(visual==null)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath),root.scene);
                    instance.name="CharacterVisual"; instance.transform.SetParent(root.transform,false); instance.transform.localPosition=Vector3.down;
                    visual=instance.GetComponent<CharacterVisual>();
                }
                var old=root.GetComponent<NetworkPlayerPresentation>();
                if(old!=null)
                {
                    var body=new SerializedObject(old).FindProperty("body").objectReferenceValue as Renderer;
                    if(body!=null)
                    {
                        body.enabled=false;
                        if(body.transform!=root.transform)foreach(var collider in body.GetComponents<Collider>())collider.enabled=false;
                    }
                }
                var adapter=root.GetComponent<NetworkCharacterVisual>(); if(adapter==null)adapter=root.AddComponent<NetworkCharacterVisual>();
                if(root.GetComponent<NetworkCharacterAnimationState>()==null)root.AddComponent<NetworkCharacterAnimationState>();
                Set(adapter,"visual",visual);
                if(root.GetComponent<Equipment>()!=null)
                {
                    var equipment=root.GetComponent<CharacterEquipmentVisual>(); if(equipment==null)equipment=root.AddComponent<CharacterEquipmentVisual>();
                    Set(equipment,"visual",visual);
                }
                var target=root.GetComponent<Targetable>(); if(target!=null)Set(target,"aimPoint",visual.GetSocket(CharacterSocket.Head));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
