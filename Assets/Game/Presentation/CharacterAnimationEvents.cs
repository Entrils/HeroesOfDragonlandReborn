using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    public enum CharacterAnimationCue { Footstep, WeaponTrailOn, WeaponTrailOff, Impact, DodgeWhoosh }
    // AnimationEvent receiver on the Animator object. Deliberately has no gameplay/physics authority.
    public sealed class CharacterAnimationEvents : MonoBehaviour
    {
        private CharacterVisual visual;
        private void Awake()=>visual=GetComponentInParent<CharacterVisual>();
        public void Footstep()=>visual?.EmitCue(CharacterAnimationCue.Footstep);
        public void WeaponTrailOn()=>visual?.EmitCue(CharacterAnimationCue.WeaponTrailOn);
        public void WeaponTrailOff()=>visual?.EmitCue(CharacterAnimationCue.WeaponTrailOff);
        public void Impact()=>visual?.EmitCue(CharacterAnimationCue.Impact);
        public void DodgeWhoosh()=>visual?.EmitCue(CharacterAnimationCue.DodgeWhoosh);
    }
}
