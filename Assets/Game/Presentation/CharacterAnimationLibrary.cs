using System;
using UnityEngine;
namespace HeroesOfDragonland.Presentation
{
    [CreateAssetMenu(menuName="Heroes of Dragonland/Character Animation Library")]
    public sealed class CharacterAnimationLibrary : ScriptableObject
    {
        [Serializable] private struct Entry { [SerializeField] internal CharacterMotion motion; [SerializeField] internal AnimationClip clip; }
        [SerializeField] private Entry[] entries=Array.Empty<Entry>();
        public AnimationClip Get(CharacterMotion motion)
        {
            foreach(var entry in entries)if(entry.motion==motion)return entry.clip;
            return null;
        }
        public float Duration(CharacterMotion motion)=>Get(motion)!=null?Mathf.Max(.05f,Get(motion).length):1;
    }
}
