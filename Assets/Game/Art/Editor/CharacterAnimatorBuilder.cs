using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class CharacterAnimatorBuilder
    {
        public const string Path = "Assets/Art/Animations/PrototypeHumanoid/AC_PrototypeHumanoid.controller";
        public static AnimatorController Build(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
            // Preserve reviewed controller edits on subsequent imports.
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(Path);
            foreach (string name in new[] { "Speed", "MoveX", "MoveZ", "ActionSpeed" }) controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in new[] { "Dead", "Attacking", "Dodging", "Blocking", "Staggered", "LockedOn" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion"); machine.defaultState = locomotion;
            var blend = new BlendTree { name="Locomotion", blendType=BlendTreeType.Simple1D, blendParameter="Speed", useAutomaticThresholds=false };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(Clip(clips,"Idle"),0); blend.AddChild(Clip(clips,"Walk"),1.8f); blend.AddChild(Clip(clips,"Run"),5);
            locomotion.motion = blend;
            var attack = machine.AddState("Attack"); attack.motion = Clip(clips,"Attack"); attack.speedParameter="ActionSpeed"; attack.speedParameterActive=true;
            var dodge = machine.AddState("Dodge"); dodge.motion = Clip(clips,"Dodge"); dodge.speedParameter="ActionSpeed"; dodge.speedParameterActive=true;
            var death = machine.AddState("Death"); death.motion = Clip(clips,"Death");
            var die = machine.AddAnyStateTransition(death); die.hasExitTime=false; die.duration=.08f; die.canTransitionToSelf=false;
            die.AddCondition(AnimatorConditionMode.If,0,"Dead");
            var revive = death.AddTransition(locomotion); revive.hasExitTime=false; revive.duration=.08f; revive.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
            Link(locomotion,attack,"Attacking",true); Link(attack,locomotion,"Attacking",false);
            Link(locomotion,dodge,"Dodging",true); Link(dodge,locomotion,"Dodging",false);
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets(); return controller;
        }
        private static AnimationClip Clip(AnimationClip[] clips,string suffix) => clips.First(c=>c.name.EndsWith("_"+suffix));
        private static void Link(AnimatorState from,AnimatorState to,string condition,bool value)
        {
            var transition=from.AddTransition(to); transition.hasExitTime=false; transition.duration=.06f;
            transition.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,condition);
            transition.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
        }
    }
}
