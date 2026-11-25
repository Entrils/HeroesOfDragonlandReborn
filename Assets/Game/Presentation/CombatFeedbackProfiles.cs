using System;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Presentation.Vfx;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Combat Feedback")]
    public sealed class CombatFeedbackProfiles : ScriptableObject
    {
        [Serializable]
        public sealed class Profile
        {
            public VfxKind effect;
            public AudioCue sound = AudioCue.Hit;
            [Range(.65f, 1.4f)] public float pitch = 1;
            [Range(.5f, 1.6f)] public float effectScale = 1;
            [Range(0, .085f)] public float hitStop = .035f;
            [Range(0, .03f)] public float shake = .009f;
            [Range(0, .08f)] public float recoil = .035f;
            [Range(0, 1)] public float rumble = .22f;
            [Range(.05f, .22f)] public float trail = .12f;
            [Range(.15f, .55f)] public float contactPose = .4f;
            [Range(.56f, .9f)] public float followThroughPose = .7f;
            public Color trailColor = new Color(1, .83f, .53f, .75f);

            public Profile() { }
            public Profile(float stop, float shake, float recoil, float rumble, float pitch, float scale,
                VfxKind effect = VfxKind.MeleeHit, AudioCue sound = AudioCue.Hit, float trail = .12f)
            {
                hitStop = stop; this.shake = shake; this.recoil = recoil; this.rumble = rumble;
                this.pitch = pitch; effectScale = scale; this.effect = effect; this.sound = sound; this.trail = trail;
            }
        }

        [SerializeField] private Profile sword = new(.035f, .008f, .032f, .22f, 1.04f, .9f);
        [SerializeField] private Profile greatsword = new(.065f, .017f, .063f, .48f, .79f, 1.38f, trail: .19f);
        [SerializeField] private Profile spear = new(.032f, .006f, .045f, .24f, 1.15f, .85f, trail: .09f);
        [SerializeField] private Profile bow = new(0, .003f, .023f, .12f, 1.24f, .65f);
        [SerializeField] private Profile magic = new(0, .009f, .048f, .3f, .89f, 1.25f, VfxKind.ManaCast);
        [SerializeField] private Profile block = new(.025f, .005f, .014f, .17f, 1, .9f, VfxKind.BlockedHit, AudioCue.Block);
        [SerializeField] private Profile parry = new(.05f, .009f, .043f, .37f, 1.13f, 1.1f, VfxKind.Parry, AudioCue.Parry);
        [SerializeField] private Profile guardBreak = new(.06f, .018f, .068f, .5f, .74f, 1.25f, VfxKind.GuardBreak, AudioCue.Block);

        [SerializeField] private Material trailMaterial;
        public Material TrailMaterial => trailMaterial;

        private static CombatFeedbackProfiles current;
        public static CombatFeedbackProfiles Current => current != null ? current
            : current = Resources.Load<CombatFeedbackProfiles>("Dragonland/CombatFeedbackProfiles");

        public Profile Resolve(DamageImpactCue cue, ImpactWeapon weapon) => cue switch
        {
            DamageImpactCue.Blocked => block,
            DamageImpactCue.Parried => parry,
            DamageImpactCue.GuardBreak => guardBreak,
            _ => weapon switch
            {
                ImpactWeapon.Greatsword => greatsword,
                ImpactWeapon.Spear => spear,
                ImpactWeapon.Bow => bow,
                ImpactWeapon.Magic => magic,
                _ => sword
            }
        };
    }
}
