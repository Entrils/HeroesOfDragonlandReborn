using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    public enum WeaponArchetype { Sword = 0, Greatsword = 1, Spear = 2, Staff = 3 }

    [Serializable]
    public struct WeaponScalingPlaceholder
    {
        [SerializeField] private ItemStat stat;
        [Tooltip("Reserved for future balancing; does not modify combat damage.")]
        [SerializeField, Min(0)] private float coefficient;
        public ItemStat Stat => stat;
        public float Coefficient => coefficient;
        public bool IsValid => Enum.IsDefined(typeof(ItemStat), stat)
            && coefficient >= 0 && !float.IsInfinity(coefficient);
    }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private WeaponArchetype archetype;
        [SerializeField, Min(0)] private float baseDamage = 25;
        [SerializeField, Min(0)] private float staminaCost = 25;
        [Tooltip("Timing multiplier: 1 is normal; 0.75 is slower; 1.25 is faster. Applies to attack and recovery.")]
        [SerializeField, Min(0.05f)] private float attackSpeed = 1;
        [Tooltip("Maximum forward reach in world units, measured from the character origin.")]
        [SerializeField, Min(0.1f)] private float range = 2.1f;
        [SerializeField] private EquipmentRequirement[] statRequirements = Array.Empty<EquipmentRequirement>();
        [SerializeField] private WeaponScalingPlaceholder[] scalingPlaceholders = Array.Empty<WeaponScalingPlaceholder>();
        [SerializeField] private WeaponAttackAnimationSet attackAnimationSet;
        [SerializeField] private GameObject weaponPrefab;

        public WeaponArchetype Archetype => archetype;
        public float BaseDamage => baseDamage;
        public float StaminaCost => staminaCost;
        public float AttackSpeed => attackSpeed;
        public float Range => range;
        public IReadOnlyList<EquipmentRequirement> StatRequirements => Array.AsReadOnly(statRequirements ?? Array.Empty<EquipmentRequirement>());
        public IReadOnlyList<WeaponScalingPlaceholder> ScalingPlaceholders => Array.AsReadOnly(scalingPlaceholders ?? Array.Empty<WeaponScalingPlaceholder>());
        public WeaponAttackAnimationSet AttackAnimationSet => attackAnimationSet;
        public GameObject WeaponPrefab => weaponPrefab;
        public bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(WeaponArchetype), archetype)) error = "Unknown weapon archetype.";
            else if (!Finite(baseDamage, 0) || !Finite(staminaCost, 0) || !Finite(attackSpeed, 0.05f) || !Finite(range, 0.1f))
                error = "Weapon damage, stamina, speed and range must be finite and within their allowed ranges.";
            else if (attackAnimationSet == null || !attackAnimationSet.TryValidate(out _)) error = "Weapon requires a valid attack animation set and combat profile.";
            else if (weaponPrefab == null) error = "Weapon prefab is required.";
            else
            {
                foreach (var requirement in StatRequirements)
                    if (!requirement.IsValid) { error = "Invalid weapon stat requirement."; return false; }
                foreach (var placeholder in ScalingPlaceholders)
                    if (!placeholder.IsValid) { error = "Invalid weapon scaling placeholder."; return false; }
                error = null; return true;
            }
            return false;
        }
        private static bool Finite(float value, float minimum) => value >= minimum && !float.IsInfinity(value);
    }
}
