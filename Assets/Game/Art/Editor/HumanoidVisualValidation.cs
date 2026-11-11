using System;
using System.Collections;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class HumanoidVisualValidation
    {
        public static string Status {get;private set;}="Not run";
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Humanoid: "+reason);}
        public static void ValidateAssets()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath);
            var visual=prefab.GetComponent<CharacterVisual>(); Check(visual!=null,"visual component");
            Check(visual.Animator!=null&&visual.Animator.avatar.isValid&&visual.Animator.isHuman&&!visual.Animator.applyRootMotion,"valid Humanoid Animator without root motion");
            foreach(CharacterSocket socket in Enum.GetValues(typeof(CharacterSocket)))Check(visual.GetSocket(socket)!=null&&visual.GetSocket(socket).IsChildOf(visual.transform),socket+" socket");
            Check(prefab.GetComponentsInChildren<Collider>().Length==0&&prefab.GetComponentsInChildren<NetworkObject>().Length==0,"visual has no gameplay collider or network authority");
            foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())Check(renderer.bones.Length>=15&&renderer.bones.All(b=>b!=null)&&renderer.sharedMesh!=null,"weighted skin references");
            foreach(string kind in HumanoidAssetBuilder.Clips)
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Animations/PrototypeHumanoid/FBX/AN_PrototypeHumanoid_"+kind+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                Check(clip.humanMotion&&clip.length>.1f,kind+" humanoid motion");
            }
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab");
            Check(player.GetComponent<NetworkCharacterVisual>().Visual!=null&&player.GetComponent<CapsuleCollider>().enabled,"gameplay root preserved");
            var aim=new SerializedObject(player.GetComponent<Targetable>()).FindProperty("aimPoint").objectReferenceValue;
            Check(aim==player.GetComponentInChildren<CharacterVisual>().GetSocket(CharacterSocket.Head),"target lock uses head socket");
            Debug.Log("[HumanoidValidation] PASS Avatar, six humanoid clips, skin, five sockets, root-motion policy, root collision and lock aim references.");
        }
        public static void Start()
        {
            Check(Application.isPlaying,"Use Play Mode");ValidateAssets();Status="RUNNING";
            UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>().StartCoroutine(Run());
        }
        private static IEnumerator Run()
        {
            var bootstrap=UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>();
            var actor=bootstrap.Avatars.First(); var player=actor.GetComponent<NetworkTestPlayer>();
            var adapter=actor.GetComponent<NetworkCharacterVisual>(); var visual=adapter.Visual;
            GameObject fixture=null,lockSource=null,lockCamera=null;
            try
            {
                Vector3 position=actor.transform.position; var collider=actor.GetComponent<CapsuleCollider>(); var colliderCenter=collider.center; float radius=collider.radius;
                fixture=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath));
                fixture.transform.position=new Vector3(0,0,40);
                var test=fixture.GetComponent<CharacterVisual>();
                test.Apply(new CharacterVisualState{Speed=5,LocalVelocity=Vector2.up*5,ActionSpeed=1},.1f);
                yield return new WaitForSeconds(.3f);
                Check(test.Animator.GetFloat("Speed")>1&&test.Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),"locomotion hook");
                var sword=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Weapons/Sword.prefab");
                var attachment=test.Attach(sword,CharacterSocket.RightHand);
                Check(attachment!=null&&attachment.transform.parent==test.GetSocket(CharacterSocket.RightHand),"right-hand weapon attachment");
                Check(attachment.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"weapon visuals cannot add collision");
                test.MoveAttachment(CharacterSocket.RightHand,CharacterSocket.Back);
                Check(test.GetAttachment(CharacterSocket.Back).transform.parent==test.GetSocket(CharacterSocket.Back),"back weapon socket");
                var left=test.Attach(sword,CharacterSocket.LeftHand);Check(left.transform.parent==test.GetSocket(CharacterSocket.LeftHand),"left hand socket");
                float headY=test.Animator.GetBoneTransform(HumanBodyBones.Head).position.y;
                test.Apply(new CharacterVisualState{Dead=true,ActionSpeed=1},.1f);
                yield return new WaitForSeconds(1.1f);
                Check(test.Animator.GetCurrentAnimatorStateInfo(CharacterAnimationModel.ActionLayer).IsName("Death")&&test.Animator.GetBoneTransform(HumanBodyBones.Head).position.y<headY-.3f,"death pose changes humanoid bones");
                Check(fixture.transform.position==new Vector3(0,0,40),"animation cannot translate root");
                test.Apply(new CharacterVisualState{ActionSpeed=1},.1f);yield return new WaitForSeconds(.2f);
                Check(test.Action==CharacterAction.None&&test.Animator.GetLayerWeight(CharacterAnimationModel.ActionLayer)==0,"revive resets death state");
                var target=fixture.AddComponent<Targetable>();
                var targetData=new SerializedObject(target);targetData.FindProperty("aimPoint").objectReferenceValue=test.GetSocket(CharacterSocket.Head);targetData.ApplyModifiedPropertiesWithoutUndo();
                lockSource=new GameObject("Temporary humanoid lock source");lockSource.transform.position=new Vector3(0,1,35);
                var targeting=lockSource.AddComponent<TargetLock>();
                lockCamera=new GameObject("Temporary humanoid lock camera");var camera=lockCamera.AddComponent<Camera>();camera.enabled=false;
                camera.transform.position=new Vector3(0,1.6f,35);camera.transform.LookAt(target.AimPosition);
                targeting.SetViewCamera(camera);Physics.SyncTransforms();targeting.Toggle();
                Check(targeting.CurrentTarget==target,"existing lock acquires humanoid head socket");
                fixture.transform.rotation=Quaternion.Euler(0,45,0);targeting.Tick(false);
                Check(targeting.CurrentTarget==target,"lock survives visual rotation");
                Check(collider.center==colliderCenter&&Mathf.Approximately(collider.radius,radius),"visual pipeline leaves gameplay capsule intact");
                Check(visual.transform.localPosition==Vector3.down&&visual.transform.localRotation==Quaternion.identity,"visual offset isolated below motor");
                Status="PASS humanoid assets, locomotion parameters, five sockets, hand/back attachments, animated death, revival, lock acquisition/rotation and root separation.";
                Debug.Log("[HumanoidValidation] "+Status);
            }
            finally {if(fixture!=null)UnityEngine.Object.Destroy(fixture);if(lockSource!=null)UnityEngine.Object.Destroy(lockSource);if(lockCamera!=null)UnityEngine.Object.Destroy(lockCamera);if(Status=="RUNNING")Status="FAILED; see Console";}
        }
        public static void BuildPlayer()
        {
            ValidateAssets();
            System.IO.Directory.CreateDirectory("Builds/HumanoidValidation");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/DragonlandBattlefield.unity"},locationPathName="Builds/HumanoidValidation/DragonlandHumanoid.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Humanoid player build failed: "+report.summary.result);
            Debug.Log("[HumanoidValidation] Development player build succeeded, errors="+report.summary.totalErrors);
        }
    }
}
