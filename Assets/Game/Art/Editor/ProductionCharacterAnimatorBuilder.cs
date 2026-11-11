using System;
using System.Linq;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class ProductionCharacterAnimatorBuilder
    {
        public const string Path="Assets/Art/Animations/PrototypeHumanoid/AC_CharacterProduction.controller";
        public const string LibraryPath="Assets/Art/Animations/PrototypeHumanoid/CharacterAnimationLibrary.asset";
        public static AnimatorController Build(AnimationClip[] clips)
        {
            Library(clips);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
            if(controller!=null)return controller;
            controller=AnimatorController.CreateAnimatorControllerAtPath(Path);
            foreach(string name in new[]{"Speed","MoveX","MoveZ","ActionSpeed","VerticalSpeed"})controller.AddParameter(name,AnimatorControllerParameterType.Float);
            foreach(string name in new[]{"Dead","Attacking","HeavyAttack","Dodging","Blocking","Staggered","HitReacting","LockedOn","Grounded"})controller.AddParameter(name,AnimatorControllerParameterType.Bool);
            controller.AddParameter("Action",AnimatorControllerParameterType.Int);controller.AddParameter("AirState",AnimatorControllerParameterType.Int);
            controller.AddParameter("CombatAction",AnimatorControllerParameterType.Int);controller.AddParameter("FullBodyAction",AnimatorControllerParameterType.Int);
            var root=controller.layers[0].stateMachine;
            var free=State(root,"Locomotion",null);root.defaultState=free;
            var freeTree=Tree(controller,"Free movement",BlendTreeType.Simple1D,"Speed");
            freeTree.AddChild(Clip(clips,"Idle"),0);freeTree.AddChild(Clip(clips,"Walk"),1.8f);freeTree.AddChild(Clip(clips,"Run"),5);free.motion=freeTree;
            var locked=State(root,"LockOnLocomotion",null);
            var lockTree=Tree(controller,"Lock-on movement",BlendTreeType.FreeformDirectional2D,"MoveX");lockTree.blendParameterY="MoveZ";
            lockTree.AddChild(Clip(clips,"Idle"),Vector2.zero);lockTree.AddChild(Clip(clips,"Walk"),new Vector2(0,1.8f));lockTree.AddChild(Clip(clips,"Run"),new Vector2(0,5));
            lockTree.AddChild(Clip(clips,"Backward"),new Vector2(0,-2));lockTree.AddChild(Clip(clips,"StrafeLeft"),new Vector2(-2,0));lockTree.AddChild(Clip(clips,"StrafeRight"),new Vector2(2,0));locked.motion=lockTree;
            var air=root.AddStateMachine("Airborne");
            foreach(var pair in new[]{(CharacterAirState.Jump,"Jump"),(CharacterAirState.Fall,"Fall"),(CharacterAirState.Land,"Land")})
            {
                var node=State(air,pair.Item2,Clip(clips,pair.Item2));
                Route(root,node,"AirState",(int)pair.Item1);
            }
            var toFree=Route(root,free,"AirState",0);toFree.AddCondition(AnimatorConditionMode.IfNot,0,"LockedOn");
            var toLock=Route(root,locked,"AirState",0);toLock.AddCondition(AnimatorConditionMode.If,0,"LockedOn");
            controller.AddLayer("Combat");controller.AddLayer("Actions");
            var mask=new AvatarMask{name="Upper body combat mask"};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var part in new[]{AvatarMaskBodyPart.Body,AvatarMaskBodyPart.Head,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})mask.SetHumanoidBodyPartActive(part,true);
            AssetDatabase.AddObjectToAsset(mask,controller);
            var layers=controller.layers;
            for(int layer=1;layer<=2;layer++){layers[layer].defaultWeight=0;layers[layer].blendingMode=AnimatorLayerBlendingMode.Override;}
            layers[1].avatarMask=mask;controller.layers=layers;
            for(int layer=1;layer<=2;layer++)
            {
                var actions=controller.layers[layer].stateMachine;string parameter=layer==1?"CombatAction":"FullBodyAction";
                var empty=State(actions,"Empty",null);actions.defaultState=empty;Route(actions,empty,parameter,0);
                foreach(string group in (layer==1?new[]{"Attacks","Defense","Reactions"}:new[]{"Abilities","Reactions","Life"}))
                {
                    var module=actions.AddStateMachine(group);
                    foreach(CharacterAction action in Enum.GetValues(typeof(CharacterAction)))
                    {
                        if(action==CharacterAction.None||CharacterAnimationModel.Layer(action)!=layer||!CharacterAnimationModel.ActionPath(action).Contains("."+group+"."))continue;
                        var node=State(module,action.ToString(),Clip(clips,action.ToString()));
                        node.speedParameter="ActionSpeed";node.speedParameterActive=true;
                        Route(actions,node,parameter,(int)action);
                    }
                }
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return controller;
        }
        private static BlendTree Tree(AnimatorController controller,string name,BlendTreeType type,string parameter)
        {
            var tree=new BlendTree{name=name,blendType=type,blendParameter=parameter,useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);return tree;
        }
        private static AnimationClip Clip(AnimationClip[] clips,string kind)=>clips.First(c=>c.name=="AN_PrototypeHumanoid_"+kind);
        private static AnimatorState State(AnimatorStateMachine machine,string name,Motion clip)
        {
            var state=machine.AddState(name);state.motion=clip;state.writeDefaultValues=false;return state;
        }
        private static AnimatorStateTransition Route(AnimatorStateMachine machine,AnimatorState state,string parameter,int value)
        {
            var transition=machine.AddAnyStateTransition(state);transition.hasExitTime=false;transition.duration=.05f;transition.canTransitionToSelf=false;
            transition.AddCondition(AnimatorConditionMode.Equals,value,parameter);return transition;
        }
        public static CharacterAnimationLibrary Library(AnimationClip[] clips)
        {
            var library=AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrary>(LibraryPath);
            if(library==null){library=ScriptableObject.CreateInstance<CharacterAnimationLibrary>();AssetDatabase.CreateAsset(library,LibraryPath);}
            var data=new SerializedObject(library);var entries=data.FindProperty("entries");var motions=(CharacterMotion[])Enum.GetValues(typeof(CharacterMotion));entries.arraySize=motions.Length;
            for(int i=0;i<motions.Length;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);entry.FindPropertyRelative("motion").enumValueIndex=i;entry.FindPropertyRelative("clip").objectReferenceValue=Clip(clips,motions[i].ToString());
            }
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(library);return library;
        }
    }
}
