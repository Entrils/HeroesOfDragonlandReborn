using System;
using System.Collections;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class CharacterAnimationValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("Animation architecture: " + label); }

        public static void ValidateAssets()
        {
            HumanoidVisualValidation.ValidateAssets();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProductionCharacterAnimatorBuilder.Path);
            Check(controller.layers.Length == 3, "three bounded layers");
            var mask = controller.layers[1].avatarMask;
            Check(mask != null && !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root)
                && !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg)
                && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), "combat mask preserves movement legs/root");
            var library = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrary>(ProductionCharacterAnimatorBuilder.LibraryPath);
            foreach (CharacterMotion motion in Enum.GetValues(typeof(CharacterMotion)))
                Check(library.Get(motion) != null && library.Get(motion).humanMotion, motion + " retargetable clip");
            foreach (var layer in controller.layers) ValidateMachine(layer.stateMachine);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.GetComponent<NetworkTestPlayer>() == null) continue;
                Check(prefab.GetComponent<NetworkCharacterAnimationState>() != null, prefab.name + " replicated presentation metadata");
                Check(prefab.GetComponent<NetworkCharacterVisual>().Visual.AnimationLibrary == library, prefab.name + " library reference");
            }
            Debug.Log("[AnimationValidation] PASS 16 Humanoid motions, modular layers/mask, event allowlist and player references.");
        }
        private static void ValidateMachine(AnimatorStateMachine machine)
        {
            Check(machine.states.Length <= 3 && machine.stateMachines.Length <= 3, "small module " + machine.name);
            foreach (var state in machine.states)
            {
                Check(!state.state.writeDefaultValues, "consistent Write Defaults Off");
                Check(state.state.transitions.Length == 0, "no pairwise transition graph");
                if (state.state.motion is AnimationClip clip)
                    foreach (var cue in AnimationUtility.GetAnimationEvents(clip))
                        Check(new[] { "Footstep", "WeaponTrailOn", "WeaponTrailOff", "Impact", "DodgeWhoosh" }.Contains(cue.functionName), "presentation-only event " + cue.functionName);
            }
            foreach (var child in machine.stateMachines) ValidateMachine(child.stateMachine);
        }
        public static void Start()
        {
            Check(Application.isPlaying, "Play Mode required"); ValidateAssets(); Status = "RUNNING";
            UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>().StartCoroutine(Run());
        }
        private static IEnumerator Settle(CharacterVisual visual, CharacterVisualState state, float seconds = .14f)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                visual.Apply(state, Time.deltaTime); elapsed += Time.deltaTime;
                yield return null;
            }
        }
        private static IEnumerator Run()
        {
            var fixture = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath));
            fixture.transform.position = new Vector3(0, 0, 40);
            var visual = fixture.GetComponent<CharacterVisual>();
            var origin = fixture.transform.position;
            try
            {
                foreach (CharacterMotion motion in Enum.GetValues(typeof(CharacterMotion)))
                {
                    var state = new CharacterVisualState { ActionDuration = .8f, ActionSequence = 1 };
                    switch (motion)
                    {
                        case CharacterMotion.Walk: state.Speed = 1.8f; state.LocalVelocity = Vector2.up * 1.8f; break;
                        case CharacterMotion.Run: state.Speed = 5; state.LocalVelocity = Vector2.up * 5; break;
                        case CharacterMotion.Backward: state.Speed = 2; state.LockedOn = true; state.LocalVelocity = Vector2.down * 2; break;
                        case CharacterMotion.StrafeLeft: case CharacterMotion.StrafeRight:
                            state.Speed = 2; state.LockedOn = true; state.LocalVelocity = Vector2.right * (motion == CharacterMotion.StrafeLeft ? -2 : 2); break;
                        case CharacterMotion.Jump: state.Airborne = true; state.VerticalSpeed = 3; break;
                        case CharacterMotion.Fall: state.Airborne = true; state.VerticalSpeed = -3; break;
                        case CharacterMotion.Dodge: state.Dodging = true; break;
                        case CharacterMotion.LightAttack: state.Attacking = true; break;
                        case CharacterMotion.HeavyAttack: state.Attacking = state.HeavyAttack = true; break;
                        case CharacterMotion.Block: state.Blocking = true; break;
                        case CharacterMotion.HitReaction: state.HitReacting = true; break;
                        case CharacterMotion.Stagger: state.Staggered = true; break;
                        case CharacterMotion.Death: state.Dead = true; break;
                    }
                    yield return Settle(visual, state, motion == CharacterMotion.Land ? .08f : .14f);
                    Check(visual.Motion == motion, motion + " snapshot resolution (actual " + visual.Motion + ")");
                    if (visual.Action != CharacterAction.None)
                    {
                        var info = visual.Animator.GetCurrentAnimatorStateInfo(CharacterAnimationModel.Layer(visual.Action));
                        Check(info.fullPathHash == Animator.StringToHash(CharacterAnimationModel.ActionPath(visual.Action)), motion + " actual Animator state");
                    }
                    else if (motion <= CharacterMotion.StrafeRight)
                        Check(visual.Animator.GetCurrentAnimatorStateInfo(0).IsName(state.LockedOn ? "LockOnLocomotion" : "Locomotion"), "free/lock locomotion routing");
                    else Check(visual.Animator.GetCurrentAnimatorStateInfo(0).IsName(motion.ToString()), motion + " air state");
                }
                yield return Settle(visual, new CharacterVisualState(), .2f);
                Check(visual.Action == CharacterAction.None && visual.Animator.GetLayerWeight(2) == 0, "revival restores locomotion");
                var attack = new CharacterVisualState { Attacking = true, ActionSequence = 1, ActionAge = .4f, ActionDuration = .8f };
                yield return Settle(visual, attack, .08f);
                Check(visual.Animator.GetCurrentAnimatorStateInfo(1).normalizedTime >= .45f, "late action joins accepted phase");
                attack.ActionSequence = 2; attack.ActionAge = 0;
                yield return Settle(visual, attack, .07f);
                Check(visual.Animator.GetCurrentAnimatorStateInfo(1).normalizedTime < .4f, "repeated accepted attack restarts");
                int impacts = 0, stops = 0;
                visual.PresentationCue += cue => { if (cue == CharacterAnimationCue.Impact) impacts++; if (cue == CharacterAnimationCue.WeaponTrailOff) stops++; };
                visual.EmitCue(CharacterAnimationCue.Impact); Check(impacts == 1, "active attack presentation cue");
                yield return Settle(visual, new CharacterVisualState { Dead = true, Attacking = true, Dodging = true, Staggered = true });
                Check(visual.Action == CharacterAction.Death && stops > 0, "death interrupts actions and clears trail");
                visual.EmitCue(CharacterAnimationCue.Impact); Check(impacts == 1, "dead rejects stale impact event");
                yield return Settle(visual, new CharacterVisualState(), .2f);
                visual.EmitCue(CharacterAnimationCue.Impact); Check(impacts == 1, "idle rejects stale impact event");
                Check(fixture.transform.position == origin && !visual.Animator.applyRootMotion && fixture.GetComponentsInChildren<Collider>().Length == 0, "presentation cannot move root or create hitboxes");
                Status = "PASS all 16 actual Animator motions; free/lock locomotion, late phase, repeat, death interruption, revival and safe events.";
                Debug.Log("[AnimationValidation] " + Status);
            }
            finally { UnityEngine.Object.Destroy(fixture); if (Status == "RUNNING") Status = "FAILED; see Console"; }
        }
    }
}
