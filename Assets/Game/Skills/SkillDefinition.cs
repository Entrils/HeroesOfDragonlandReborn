using System;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Skills
{
    public enum SkillKind { Active, Passive }
    public enum SkillBranch { Warrior, Agility, Magic, Survival }
    public enum SkillEffect
    {
        MeleeStrike, Guard, MeleeTraining, Ability, GuardEfficiency, Poise, DodgeEfficiency,
        Stamina, AttackSpeed, Mana, SpellEfficiency, SpellCooldown, Health, PotionEfficiency, Defense
    }

    [Serializable]
    public struct SkillPrerequisite
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField, Min(1)] private int rank;
        public SkillDefinition Skill => skill;
        public int Rank => rank;
    }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Skill")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SkillEffect effect;
        [SerializeField] private SkillBranch branch;
        [SerializeField, Range(1, 10)] private int maxRank = 1;
        [SerializeField] private SkillPrerequisite[] prerequisites = Array.Empty<SkillPrerequisite>();
        [SerializeField] private string exclusiveGroup;
        [SerializeField] private HeroesOfDragonland.Abilities.AbilityDefinition ability;
        public HeroesOfDragonland.Abilities.AbilityDefinition Ability => ability;
        [SerializeField, Min(1)] private int requiredLevel = 1;
        [SerializeField] private EquipmentRequirement[] requirements = Array.Empty<EquipmentRequirement>();
        [SerializeField, Min(1)] private int pointCost = 1;
        [SerializeField, Min(0)] private float manaCost;
        [SerializeField, Min(0)] private float cooldown;
        [Tooltip("Per rank. Resource maxima, poise and melee damage are flat. Efficiency, speed and defense are fractions.")]
        [SerializeField, Min(0)] private float magnitude = 1;
        public string DisplayName => displayName;
        public string Description => description;
        public SkillEffect Effect => effect;
        public SkillKind Kind => effect is SkillEffect.MeleeStrike or SkillEffect.Guard or SkillEffect.Ability
            ? SkillKind.Active : SkillKind.Passive;
        public SkillBranch Branch => branch;
        public int MaxRank => maxRank;
        public System.Collections.Generic.IReadOnlyList<SkillPrerequisite> Prerequisites => prerequisites;
        public string ExclusiveGroup => exclusiveGroup;
        public int RequiredLevel => requiredLevel;
        public System.Collections.Generic.IReadOnlyList<EquipmentRequirement> Requirements => requirements;
        public int PointCost => pointCost;
        public float ManaCost => effect == SkillEffect.Ability && ability != null ? ability.ManaCost : manaCost;
        public float Cooldown => effect == SkillEffect.Ability && ability != null ? ability.Cooldown : cooldown;
        public float Magnitude => magnitude;
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(displayName) || !Enum.IsDefined(typeof(SkillEffect), effect) || !Enum.IsDefined(typeof(SkillBranch), branch)
                    || requiredLevel < 1 || maxRank < 1 || maxRank > 10 || (Kind == SkillKind.Active && maxRank != 1)
                    || prerequisites == null || pointCost < 1 || !FiniteNonnegative(manaCost)
                    || !FiniteNonnegative(cooldown) || !FiniteNonnegative(magnitude) || requirements == null) return false;
                foreach (var requirement in requirements) if (!requirement.IsValid) return false;
                if (effect == SkillEffect.Ability && (ability == null || !ability.IsValid)) return false;
                return Kind != SkillKind.Passive || (manaCost == 0 && cooldown == 0);
            }
        }
        private static bool FiniteNonnegative(float value) => value >= 0 && !float.IsInfinity(value);
    }
}
