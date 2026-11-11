using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    public enum CharacterSocket { RightHand, LeftHand, Back, Head, Chest }
    public struct CharacterVisualState
    {
        public float Speed;
        public Vector2 LocalVelocity;
        public bool Dead, Attacking, Dodging, Blocking, Staggered, LockedOn;
        public float ActionSpeed;
        public bool Airborne, HeavyAttack, HitReacting;
        public float VerticalSpeed, ActionAge, ActionDuration;
        public int ActionSequence;
    }

    // Owns presentation only. This component never moves the gameplay root or applies damage.
    [DisallowMultipleComponent]
    public sealed class CharacterVisual : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterAnimationLibrary animationLibrary;
        [SerializeField] private SkinnedMeshRenderer[] bodyRenderers;
        [SerializeField] private Transform rightHand, leftHand, back, head, chest;
        private readonly Dictionary<CharacterSocket, GameObject> attachments = new();
        private MaterialPropertyBlock tint;
        public Animator Animator => animator;
        public CharacterVisualState State { get; private set; }
        public CharacterMotion Motion {get;private set;}
        public CharacterAction Action {get;private set;}
        public CharacterAirState AirState {get;private set;}
        public CharacterAnimationLibrary AnimationLibrary=>animationLibrary;
        public event Action<CharacterAnimationCue> PresentationCue;
        private float landing;
        private bool hadSnapshot,wasAirborne;
        private int lastSequence;
        private RuntimeAnimatorController baseController;
        private AnimatorOverrideController weaponController;
        private AnimationClip shownLight, shownHeavy;
        public void SetWeaponPresentation(HeroesOfDragonland.Items.WeaponDefinition weapon, bool heavy, int index)
        {
            if (animator == null || animationLibrary == null) return;
            var light = weapon != null ? weapon.Moveset != null ? weapon.Moveset.GetAnimation(false, heavy ? 0 : index)
                : weapon.AttackAnimationSet?.LightAttack : null;
            var strong = weapon != null ? weapon.Moveset != null ? weapon.Moveset.GetAnimation(true, 0)
                : weapon.AttackAnimationSet?.HeavyAttack : null;
            if (shownLight == light && shownHeavy == strong) return;
            baseController ??= animator.runtimeAnimatorController;
            if (weaponController == null)
            {
                weaponController = new AnimatorOverrideController(baseController);
                animator.runtimeAnimatorController = weaponController;
            }
            shownLight = light; shownHeavy = strong;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            var originalLight = animationLibrary.Get(CharacterMotion.LightAttack);
            var originalHeavy = animationLibrary.Get(CharacterMotion.HeavyAttack);
            if (originalLight != null) overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalLight, light ?? originalLight));
            if (originalHeavy != null) overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalHeavy, strong ?? originalHeavy));
            weaponController.ApplyOverrides(overrides);
        }
        private void OnDestroy() { if (weaponController != null) Destroy(weaponController); }
        public Transform GetSocket(CharacterSocket socket) => socket switch
        {
            CharacterSocket.RightHand => rightHand, CharacterSocket.LeftHand => leftHand,
            CharacterSocket.Back => back, CharacterSocket.Head => head, _ => chest
        };
        private void Awake()
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            { Debug.LogError("CharacterVisual needs a valid Humanoid Animator/Avatar.", this); enabled = false; return; }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        public void Apply(CharacterVisualState state, float deltaTime)
        {
            if (!isActiveAndEnabled || animator == null || animator.runtimeAnimatorController == null) return;
            State = state;
            if(!hadSnapshot){hadSnapshot=true;wasAirborne=state.Airborne;}
            landing=state.Airborne||state.Dead ? 0 : wasAirborne ? .16f : Mathf.Max(0,landing-deltaTime);
            wasAirborne=state.Airborne;
            AirState=state.Airborne?(state.VerticalSpeed>.1f?CharacterAirState.Jump:CharacterAirState.Fall)
                :landing>0?CharacterAirState.Land:CharacterAirState.Grounded;
            var next=CharacterAnimationModel.Action(state);
            if((Action==CharacterAction.LightAttack||Action==CharacterAction.HeavyAttack)&&next!=Action)
                PresentationCue?.Invoke(CharacterAnimationCue.WeaponTrailOff);
            Motion=next!=CharacterAction.None?CharacterAnimationModel.Motion(next):AirState==CharacterAirState.Jump?CharacterMotion.Jump
                :AirState==CharacterAirState.Fall?CharacterMotion.Fall:AirState==CharacterAirState.Land?CharacterMotion.Land:CharacterAnimationModel.GroundMotion(state);
            animator.SetInteger("Action",(int)next);animator.SetInteger("AirState",(int)AirState);
            animator.SetInteger("CombatAction",next!=CharacterAction.None&&CharacterAnimationModel.Layer(next)==1?(int)next:0);
            animator.SetInteger("FullBodyAction",next!=CharacterAction.None&&CharacterAnimationModel.Layer(next)==2?(int)next:0);
            animator.SetBool("Grounded",!state.Airborne);animator.SetFloat("VerticalSpeed",state.VerticalSpeed);
            animator.SetBool("HeavyAttack",state.HeavyAttack);animator.SetBool("HitReacting",state.HitReacting);
            animator.SetFloat("Speed", state.Dead ? 0 : state.Speed, .1f, Mathf.Max(.0001f, deltaTime));
            animator.SetFloat("MoveX", state.LocalVelocity.x);
            animator.SetFloat("MoveZ", state.LocalVelocity.y);
            float clipLength=animationLibrary!=null?animationLibrary.Duration(CharacterAnimationModel.Motion(next)):1;
            var attackClip = next == CharacterAction.LightAttack ? shownLight : next == CharacterAction.HeavyAttack ? shownHeavy : null;
            if (attackClip != null) clipLength = Mathf.Max(.05f, attackClip.length);
            animator.SetFloat("ActionSpeed",state.ActionDuration>0?Mathf.Clamp(clipLength/state.ActionDuration,.1f,8):1);
            if(animator.layerCount>CharacterAnimationModel.ActionLayer)
            {
                for(int layer=1;layer<=CharacterAnimationModel.ActionLayer;layer++)
                {
                    float weight=animator.GetLayerWeight(layer);
                    float desired=next!=CharacterAction.None&&CharacterAnimationModel.Layer(next)==layer?1:0;
                    animator.SetLayerWeight(layer,Mathf.MoveTowards(weight,desired,Mathf.Max(0,deltaTime)*15));
                }
                if(next!=CharacterAction.None&&(next!=Action||state.ActionSequence!=lastSequence))
                {
                    float normalized=state.ActionAge/Mathf.Max(.05f,state.ActionDuration>0?state.ActionDuration:clipLength);
                    var clip=animationLibrary!=null?animationLibrary.Get(CharacterAnimationModel.Motion(next)):null;
                    if (attackClip != null) clip = attackClip;
                    normalized=clip!=null&&clip.isLooping?Mathf.Repeat(normalized,1):Mathf.Clamp01(normalized);
                    animator.CrossFadeInFixedTime(CharacterAnimationModel.ActionPath(next),.04f,CharacterAnimationModel.Layer(next),normalized*clipLength);
                }
            }
            Action=next;lastSequence=state.ActionSequence;
            animator.SetBool("Dead", state.Dead); animator.SetBool("Attacking", !state.Dead && state.Attacking);
            animator.SetBool("Dodging", !state.Dead && state.Dodging); animator.SetBool("Blocking", !state.Dead && state.Blocking);
            animator.SetBool("Staggered", !state.Dead && state.Staggered); animator.SetBool("LockedOn", state.LockedOn);
        }
        public void EmitCue(CharacterAnimationCue cue)
        {
            if(State.Dead)return;
            bool attack=Action==CharacterAction.LightAttack||Action==CharacterAction.HeavyAttack;
            if(cue==CharacterAnimationCue.Footstep&&(!State.Airborne&&State.Speed>.15f&&(Action==CharacterAction.None||CharacterAnimationModel.Layer(Action)==1))
                ||cue==CharacterAnimationCue.DodgeWhoosh&&Action==CharacterAction.Dodge
                ||attack&&(cue==CharacterAnimationCue.WeaponTrailOn||cue==CharacterAnimationCue.WeaponTrailOff||cue==CharacterAnimationCue.Impact))
                PresentationCue?.Invoke(cue);
        }
        public void SetTint(Color color)
        {
            tint ??= new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
            foreach (var renderer in bodyRenderers)
                if (renderer != null) renderer.SetPropertyBlock(tint, 0); // Keep dark secondary material untinted.
        }
        public GameObject GetAttachment(CharacterSocket socket) => attachments.TryGetValue(socket, out var item) ? item : null;
        public GameObject Attach(GameObject prefab, CharacterSocket socket)
        {
            ClearAttachment(socket);
            var parent = GetSocket(socket);
            if (prefab == null || parent == null) return null;
            var visual = Instantiate(prefab, parent, false);
            visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = Vector3.one;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.detectCollisions = false; }
            attachments[socket] = visual;
            return visual;
        }
        public void ClearAttachment(CharacterSocket socket)
        {
            if (!attachments.Remove(socket, out var previous) || previous == null) return;
            previous.SetActive(false); Destroy(previous);
        }
        public void MoveAttachment(CharacterSocket from, CharacterSocket to)
        {
            var visual = GetAttachment(from); var parent = GetSocket(to);
            if (visual == null || parent == null || from == to) return;
            ClearAttachment(to); attachments.Remove(from); attachments[to] = visual;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
        }
    }
}
