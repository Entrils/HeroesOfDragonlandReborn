using HeroesOfDragonland.Combat;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Weapon Attack Animation Set")]
    public sealed class WeaponAttackAnimationSet : ScriptableObject
    {
        [SerializeField] private MeleeAttackDefinition combatProfile;
        [Tooltip("Optional until authored character animations replace procedural swings.")]
        [SerializeField] private AnimationClip lightAttack;
        [SerializeField] private AnimationClip heavyAttack;
        public MeleeAttackDefinition CombatProfile => combatProfile;
        public AnimationClip LightAttack => lightAttack;
        public AnimationClip HeavyAttack => heavyAttack;
        public bool TryValidate(out string error)
        {
            error = combatProfile == null ? "Attack set requires a melee combat profile." : null;
            return error == null;
        }
    }
}
