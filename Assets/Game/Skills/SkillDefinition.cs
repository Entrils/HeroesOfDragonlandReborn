using System;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Skills
{
    public enum SkillKind { Active, Passive }
    public enum SkillEffect { MeleeStrike, Guard, MeleeTraining }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Skill")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SkillEffect effect;
        [SerializeField, Min(1)] private int requiredLevel = 1;
        [SerializeField] private EquipmentRequirement[] requirements = Array.Empty<EquipmentRequirement>();
        [SerializeField, Min(1)] private int pointCost = 1;
        [SerializeField, Min(0)] private float manaCost;
        [SerializeField, Min(0)] private float cooldown;
        [Tooltip("Strike: damage multiplier. Guard: seconds. Training: flat melee damage.")]
        [SerializeField, Min(0)] private float magnitude = 1;
        public string DisplayName => displayName;
        public string Description => description;
        public SkillEffect Effect => effect;
        public SkillKind Kind => effect == SkillEffect.MeleeTraining ? SkillKind.Passive : SkillKind.Active;
        public int RequiredLevel => requiredLevel;
        public System.Collections.Generic.IReadOnlyList<EquipmentRequirement> Requirements => requirements;
        public int PointCost => pointCost;
        public float ManaCost => manaCost;
        public float Cooldown => cooldown;
        public float Magnitude => magnitude;
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(displayName) || !Enum.IsDefined(typeof(SkillEffect), effect)
                    || requiredLevel < 1 || pointCost < 1 || !FiniteNonnegative(manaCost)
                    || !FiniteNonnegative(cooldown) || !FiniteNonnegative(magnitude) || requirements == null) return false;
                foreach (var requirement in requirements) if (!requirement.IsValid) return false;
                return Kind != SkillKind.Passive || (manaCost == 0 && cooldown == 0);
            }
        }
        private static bool FiniteNonnegative(float value) => value >= 0 && !float.IsInfinity(value);
    }
}
