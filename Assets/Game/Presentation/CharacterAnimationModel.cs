using System;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    public enum CharacterMotion { Idle, Walk, Run, Backward, StrafeLeft, StrafeRight, Jump, Fall, Land, Dodge, LightAttack, HeavyAttack, Block, HitReaction, Stagger, Death }
    public enum CharacterAction { None, Dodge, LightAttack, HeavyAttack, Block, HitReaction, Stagger, Death }
    public enum CharacterAirState { Grounded, Jump, Fall, Land }

    public static class CharacterAnimationModel
    {
        public const int CombatLayer = 1;
        public const int ActionLayer = 2;
        public static int Layer(CharacterAction action)=>action==CharacterAction.LightAttack||action==CharacterAction.HeavyAttack||action==CharacterAction.Block||action==CharacterAction.HitReaction?CombatLayer:ActionLayer;
        public static CharacterAction Action(CharacterVisualState state)
        {
            if(state.Dead)return CharacterAction.Death;
            if(state.Staggered)return CharacterAction.Stagger;
            if(state.Dodging)return CharacterAction.Dodge;
            if(state.HitReacting)return CharacterAction.HitReaction;
            if(state.Attacking)return state.HeavyAttack?CharacterAction.HeavyAttack:CharacterAction.LightAttack;
            return state.Blocking?CharacterAction.Block:CharacterAction.None;
        }
        public static CharacterMotion Motion(CharacterAction action) => action switch {
            CharacterAction.Dodge=>CharacterMotion.Dodge, CharacterAction.LightAttack=>CharacterMotion.LightAttack,
            CharacterAction.HeavyAttack=>CharacterMotion.HeavyAttack, CharacterAction.Block=>CharacterMotion.Block,
            CharacterAction.HitReaction=>CharacterMotion.HitReaction, CharacterAction.Stagger=>CharacterMotion.Stagger,
            CharacterAction.Death=>CharacterMotion.Death, _=>CharacterMotion.Idle
        };
        public static string ActionPath(CharacterAction action) => action switch {
            CharacterAction.Dodge=>"Actions.Abilities.Dodge", CharacterAction.LightAttack=>"Combat.Attacks.LightAttack",
            CharacterAction.HeavyAttack=>"Combat.Attacks.HeavyAttack", CharacterAction.Block=>"Combat.Defense.Block",
            CharacterAction.HitReaction=>"Combat.Reactions.HitReaction", CharacterAction.Stagger=>"Actions.Reactions.Stagger",
            CharacterAction.Death=>"Actions.Life.Death", _=>"Actions.Empty"
        };
        public static CharacterMotion GroundMotion(CharacterVisualState state)
        {
            if(state.Speed<.15f)return CharacterMotion.Idle;
            if(state.LockedOn)
            {
                if(Mathf.Abs(state.LocalVelocity.x)>Mathf.Abs(state.LocalVelocity.y))return state.LocalVelocity.x<0?CharacterMotion.StrafeLeft:CharacterMotion.StrafeRight;
                if(state.LocalVelocity.y<-.15f)return CharacterMotion.Backward;
            }
            return state.Speed>3?CharacterMotion.Run:CharacterMotion.Walk;
        }
    }

}
