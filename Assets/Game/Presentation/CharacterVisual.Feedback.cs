using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    public sealed partial class CharacterVisual
    {
        private WeaponDefinition feedbackWeapon;
        private MeleeAttackDefinition feedbackMove;
        private GameObject tracedWeapon;
        private TrailRenderer swingTrail;
        private Vector3 trailTip;
        private int trailSequence, swingCueSequence, lastAttackPhase = -1;
        private bool resampleAttack;
        private double freezeUntil, recoverFromFreeze, contactUntil;
        private float previousAnimatorSpeed;
        private bool frozen;
        private Vector3 contactOffset, appliedOffset;

        internal void PresentContact(Vector3 direction, float recoil, float pause)
        {
            if (!isActiveAndEnabled || animator == null || State.Dead) return;
            var now = Time.unscaledTimeAsDouble;
            if (pause > 0 && now >= recoverFromFreeze)
            {
                if (!frozen) previousAnimatorSpeed = animator.speed;
                freezeUntil = now + Mathf.Min(pause, .085f);
                recoverFromFreeze = now + .13;
                frozen = true;
                animator.speed = 0;
            }

            if (recoil > 0 && transform.parent != null && GetComponentInParent<DamageReceiver>() is { } receiver
                && receiver.transform != transform && GetComponentInChildren<Collider>(true) == null)
            {
                contactOffset = transform.parent.InverseTransformDirection(direction.normalized) * Mathf.Min(recoil, .08f);
                contactUntil = now + .14;
            }
        }

        private void LateUpdate()
        {
            double now = Time.unscaledTimeAsDouble;
            if (frozen && (now >= freezeUntil || State.Dead)) RestoreSpeed();
            transform.localPosition -= appliedOffset;
            float weight = State.Dead ? 0 : Mathf.Clamp01((float)(contactUntil - now) / .14f);
            appliedOffset = contactOffset * (weight * weight);
            transform.localPosition += appliedOffset;
        }

        private void RestoreSpeed()
        {
            if (frozen && animator != null) animator.speed = previousAnimatorSpeed;
            resampleAttack |= frozen;
            frozen = false;
        }

        private void OnDisable()
        {
            RestoreSpeed();
            transform.localPosition -= appliedOffset;
            appliedOffset = contactOffset = Vector3.zero;
            contactUntil = recoverFromFreeze = 0;
            if (swingTrail != null) { swingTrail.emitting = false; swingTrail.Clear(); }
            tracedWeapon = null;
            swingCueSequence = trailSequence = 0;
            lastAttackPhase = -1;
        }

        private void SetFeedbackMove(WeaponDefinition weapon, bool heavy, int index)
        {
            feedbackWeapon = weapon;
            feedbackMove = weapon != null ? weapon.Moveset?.GetMove(heavy, index)?.Profile
                ?? weapon.AttackAnimationSet?.CombatProfile : null;
        }

        private float AttackSample(CharacterVisualState state, out float rate, out int phase)
        {
            rate = 1;
            phase = -1;
            if (feedbackMove == null || feedbackMove.AttackDuration <= 0 || state.ActionDuration <= 0) return -1;
            float progress = Mathf.Clamp01(state.ActionAge / state.ActionDuration);
            float start = feedbackMove.ActiveStartTime / feedbackMove.AttackDuration;
            float end = feedbackMove.ActiveEndTime / feedbackMove.AttackDuration;
            // Map the authored contact and follow-through poses onto the accepted active window.
            var profile = CombatFeedbackProfiles.Current?.Resolve(DamageImpactCue.MeleeHit, WeaponKind());
            float contactPose = Mathf.Clamp(profile?.contactPose ?? .4f, .15f, .55f);
            float followThroughPose = Mathf.Clamp(profile?.followThroughPose ?? .7f, .56f, .9f);
            if (progress < start)
            {
                phase = 0;
                rate = contactPose / Mathf.Max(.01f, start);
                return progress * rate;
            }
            if (progress < end)
            {
                phase = 1;
                rate = (followThroughPose - contactPose) / Mathf.Max(.01f, end - start);
                return contactPose + (progress - start) * rate;
            }
            phase = 2;
            rate = (1 - followThroughPose) / Mathf.Max(.01f, 1 - end);
            return followThroughPose + (progress - end) * rate;
        }

        private void UpdateTrail(CharacterVisualState state, bool attacking)
        {
            bool draw = attacking && feedbackMove != null && feedbackWeapon != null
                && feedbackWeapon.Archetype != WeaponArchetype.Bow && state.ActionDuration > 0;
            if (draw)
            {
                float progress = state.ActionAge / state.ActionDuration;
                draw = progress >= feedbackMove.ActiveStartTime / feedbackMove.AttackDuration
                    && progress < feedbackMove.ActiveEndTime / feedbackMove.AttackDuration;
            }

            if (draw && swingCueSequence != state.ActionSequence)
            {
                swingCueSequence = state.ActionSequence;
                PresentationCue?.Invoke(CharacterAnimationCue.Swing);
            }

            var weapon = draw ? GetAttachment(CharacterSocket.RightHand) : tracedWeapon;
            if (draw && weapon != null)
            {
                if (swingTrail == null)
                {
                    var material = CombatFeedbackProfiles.Current?.TrailMaterial;
                    if (material == null) return;
                    var tip = new GameObject("Weapon Swing Trail");
                    tip.transform.SetParent(transform, false);
                    swingTrail = tip.AddComponent<TrailRenderer>();
                    swingTrail.emitting = false;
                    swingTrail.sharedMaterial = material;
                    swingTrail.startWidth = .085f;
                    swingTrail.endWidth = 0;
                    swingTrail.minVertexDistance = .035f;
                    swingTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    swingTrail.receiveShadows = false;
                }
                if (weapon != tracedWeapon || trailSequence != state.ActionSequence)
                {
                    swingTrail.Clear();
                    tracedWeapon = weapon;
                    trailSequence = state.ActionSequence;
                    trailTip = FindWeaponTip(weapon);
                    var profile = CombatFeedbackProfiles.Current.Resolve(DamageImpactCue.MeleeHit, WeaponKind());
                    swingTrail.time = profile.trail;
                    swingTrail.startColor = profile.trailColor;
                    swingTrail.endColor = new Color(profile.trailColor.r, profile.trailColor.g, profile.trailColor.b, 0);
                }
            }
            if (swingTrail == null) return;
            if (weapon != null) swingTrail.transform.position = weapon.transform.TransformPoint(trailTip);
            swingTrail.emitting = draw && weapon != null && !frozen;
            if (state.Dead || state.Dodging || state.Staggered || state.HitReacting || weapon == null || weapon.transform.parent != rightHand)
            {
                swingTrail.Clear();
                tracedWeapon = null;
            }
        }

        private ImpactWeapon WeaponKind() => feedbackWeapon?.Archetype switch
        {
            WeaponArchetype.Greatsword => ImpactWeapon.Greatsword,
            WeaponArchetype.Spear => ImpactWeapon.Spear,
            _ => ImpactWeapon.Sword
        };

        private static Vector3 FindWeaponTip(GameObject weapon)
        {
            var grip = weapon.transform.position;
            Vector3 tip = grip;
            foreach (var renderer in weapon.GetComponentsInChildren<MeshRenderer>())
            {
                var bounds = renderer.localBounds;
                var extents = bounds.extents;
                Vector3 axis = extents.x > extents.y
                    ? (extents.x > extents.z ? Vector3.right * extents.x : Vector3.forward * extents.z)
                    : (extents.y > extents.z ? Vector3.up * extents.y : Vector3.forward * extents.z);
                Vector3 first = renderer.transform.TransformPoint(bounds.center + axis);
                Vector3 second = renderer.transform.TransformPoint(bounds.center - axis);
                if ((first - grip).sqrMagnitude > (tip - grip).sqrMagnitude) tip = first;
                if ((second - grip).sqrMagnitude > (tip - grip).sqrMagnitude) tip = second;
            }
            return weapon.transform.InverseTransformPoint(tip);
        }
    }
}
