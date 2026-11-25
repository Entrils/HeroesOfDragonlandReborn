using System;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HeroesOfDragonland.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyModelAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip move;
        [SerializeField] private AnimationClip block;
        [SerializeField] private AnimationClip[] attacks = Array.Empty<AnimationClip>();
        [SerializeField] private AnimationClip[] phaseTwo = Array.Empty<AnimationClip>();
        [SerializeField] private float[] contacts = Array.Empty<float>();
        [SerializeField] private float[] phaseTwoContacts = Array.Empty<float>();
        [SerializeField] private bool useAttackReadyPose;
        [SerializeField] private bool faceAttackDirection;

        private EnemyMeleeAttack attack;
        private NetworkEnemy network;
        private Combat.CombatDefense defense;
        private CharacterStats stats;
        private SkinnedMeshRenderer bow;
        private int bowDraw = -1;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable playable;
        private AnimationClip currentClip;
        private AnimationClip attackClip;
        private float contact;
        private float windup;
        private float lastAge;
        private float recoveryTime;
        private float castPoseRemaining;
        private bool wasInFlight;
        private bool wasWindingUp;
        private Vector3 previousPosition;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool presentedDeath;
        private bool canRecoil;
        private Vector3 recoil, appliedRecoil;
        private double recoilUntil, freezeUntil, nextFreeze;

        internal void PresentContact(Vector3 direction, float distance, float pause)
        {
            if (!isActiveAndEnabled || !canRecoil || presentedDeath) return;
            double now = Time.unscaledTimeAsDouble;
            recoil = animator.transform.parent.InverseTransformDirection(direction.normalized) * Mathf.Min(distance, .08f);
            recoilUntil = now + .14;
            if (pause <= 0 || now < nextFreeze) return;
            freezeUntil = now + Mathf.Min(pause, .085f);
            nextFreeze = now + .13;
        }

        private void ApplyRecoil()
        {
            if (!canRecoil) return;
            float weight = Mathf.Clamp01((float)(recoilUntil - Time.unscaledTimeAsDouble) / .14f);
            appliedRecoil = recoil * (weight * weight);
            animator.transform.localPosition += appliedRecoil;
        }

        private void Awake()
        {
            attack = GetComponent<EnemyMeleeAttack>();
            network = GetComponent<NetworkEnemy>();
            defense = GetComponent<Combat.CombatDefense>();
            stats = GetComponent<CharacterStats>();
            if (animator == null)
            {
                enabled = false;
                return;
            }
            canRecoil = animator.transform != transform && animator.transform.IsChildOf(transform)
                && animator.GetComponentInChildren<Collider>(true) == null;
            restPosition = animator.transform.localPosition;
            restRotation = animator.transform.localRotation;
            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index = renderer.sharedMesh.GetBlendShapeIndex("BowDraw");
                if (index < 0) continue;
                bow = renderer;
                bowDraw = index;
                break;
            }
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            animator.applyRootMotion = false;
            // The manually sampled clips can contain humanoid cues. Enemy presentation uses confirmed combat events.
            animator.fireEvents = false;
            graph = PlayableGraph.Create(name + " visual animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Model visual", animator);
            graph.Play();
        }

        private void OnDisable()
        {
            if (canRecoil && animator != null) animator.transform.localPosition -= appliedRecoil;
            appliedRecoil = Vector3.zero;
            recoilUntil = freezeUntil = nextFreeze = 0;
            if (graph.IsValid()) graph.Destroy();
            currentClip = null;
            wasWindingUp = false;
            wasInFlight = false;
            recoveryTime = 0;
            castPoseRemaining = 0;
        }

        private void LateUpdate()
        {
            if (canRecoil) animator.transform.localPosition -= appliedRecoil;
            appliedRecoil = Vector3.zero;
            if (stats != null && stats.IsDead)
            {
                if (!presentedDeath)
                {
                    presentedDeath = true;
                    Sample(idle, 0);
                    if (bow != null) bow.SetBlendShapeWeight(bowDraw, 0);
                    animator.transform.localRotation = restRotation * Quaternion.Euler(0, 0, 90);
                    animator.transform.localPosition = restPosition + Vector3.up * .45f;
                }
                return;
            }
            if (presentedDeath)
            {
                presentedDeath = false;
                animator.transform.SetLocalPositionAndRotation(restPosition, restRotation);
                previousPosition = transform.position;
                wasWindingUp = wasInFlight = false;
                recoveryTime = castPoseRemaining = 0;
            }
            if (Time.unscaledTimeAsDouble < freezeUntil) { ApplyRecoil(); return; }
            bool replicated = network != null && network.Object != null && network.Object.IsValid;
            bool inFlight = replicated ? network.ProjectileInFlight : attack != null && attack.ProjectileInFlight;
            float age = 0;
            bool windingUp = false;
            int index = 0;
            bool secondPhase = false;
            if (replicated && network.AttackSequence > 0)
            {
                double time = network.HasStateAuthority ? network.Runner.LocalRenderTime : network.Runner.RemoteRenderTime;
                age = (float)(time - network.AttackStartTick * (double)network.Runner.DeltaTime);
                windup = network.AttackWindup;
                windingUp = age >= 0 && age < windup &&
                    (network.AttackEndTick == 0 || time < network.AttackEndTick * (double)network.Runner.DeltaTime);
                index = network.AttackIndex;
                secondPhase = network.AttackPhase == BossPhase.PhaseTwo;
            }
            else if (attack != null && attack.IsBusy)
            {
                age = attack.AttackElapsed;
                windup = attack.Windup;
                windingUp = age < windup;
                index = attack.AttackIndex;
                secondPhase = GetComponent<BossEncounter>() is { Phase: BossPhase.PhaseTwo };
            }
            windingUp &= !inFlight;

            bool moving = (transform.position - previousPosition).sqrMagnitude > .000001f;
            previousPosition = transform.position;
            float progress = windingUp ? Mathf.Clamp01(age / windup) : 0;
            if (windingUp)
            {
                var clips = secondPhase && phaseTwo.Length > 0 ? phaseTwo : attacks;
                var timing = secondPhase && phaseTwo.Length > 0 ? phaseTwoContacts : contacts;
                attackClip = clips.Length == 0 ? null : clips[index % clips.Length];
                contact = attackClip == null ? 0 : attackClip.length * (timing.Length == 0 ? 1 : timing[index % timing.Length]);
                if (faceAttackDirection && attack != null)
                {
                    Vector3 forward = attack.AttackForward;
                    forward.y = 0;
                    if (forward.sqrMagnitude > .001f) animator.transform.rotation = Quaternion.LookRotation(forward);
                }
                Sample(attackClip ?? idle, contact * progress);
                recoveryTime = 0;
            }
            else
            {
                if (inFlight && !wasInFlight && bow == null && useAttackReadyPose && attackClip != null)
                    castPoseRemaining = .12f;
                if (wasWindingUp && attackClip != null && windup - lastAge <= Mathf.Max(Time.deltaTime * 2, .08f))
                    recoveryTime = contact;
                if (castPoseRemaining > 0)
                {
                    Sample(attackClip, contact);
                    castPoseRemaining -= Time.deltaTime;
                }
                else if (recoveryTime > 0 && attackClip != null && recoveryTime < attackClip.length)
                {
                    Sample(attackClip, recoveryTime);
                    recoveryTime += Time.deltaTime;
                }
                else
                {
                    bool blocking = replicated ? network.Blocking : defense != null && defense.IsBlocking;
                    var clip = blocking && block != null ? block : moving && move != null ? move : idle;
                    bool ready = useAttackReadyPose && !moving && !blocking && attacks.Length > 0;
                    if (ready) clip = attacks[0];
                    Sample(clip, ready || clip == null ? 0 : Time.time % clip.length);
                }
            }
            if (bow != null) bow.SetBlendShapeWeight(bowDraw, 100 * progress);
            wasWindingUp = windingUp;
            wasInFlight = inFlight;
            lastAge = age;
            ApplyRecoil();
        }

        private void Sample(AnimationClip clip, float time)
        {
            if (clip == null) return;
            if (clip != currentClip)
            {
                if (playable.IsValid()) graph.DestroyPlayable(playable);
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0);
                output.SetSourcePlayable(playable);
                currentClip = clip;
            }
            playable.SetTime(time);
            graph.Evaluate(0);
        }
    }
}
