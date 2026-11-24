using System;
using System.Collections.Generic;
using HeroesOfDragonland.Abilities;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.Skills
{
    public readonly struct BuildSkillPriority
    {
        private readonly SkillEffect effect;
        private readonly AbilityEffect ability;

        public int TargetRank { get; }

        public BuildSkillPriority(SkillEffect effect, int targetRank = 1)
        {
            this.effect = effect;
            ability = default;
            TargetRank = targetRank;
        }

        public BuildSkillPriority(AbilityEffect ability)
        {
            effect = SkillEffect.Ability;
            this.ability = ability;
            TargetRank = 1;
        }

        public bool Matches(SkillDefinition skill) => skill != null && skill.Effect == effect
            && (effect != SkillEffect.Ability || (skill.Ability != null && skill.Ability.Effect == ability));
    }

    // Read by the local UI only. Preferences never enter simulation or equipment validation.
    public sealed class SessionBuildPreset
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ItemStat> Attributes { get; }
        public IReadOnlyList<BuildSkillPriority> Skills { get; }
        public IReadOnlyList<ConsumableEffect> QuickSlots { get; }
        public IReadOnlyList<WeaponArchetype> Weapons { get; }

        private SessionBuildPreset(string id, string displayName, ItemStat[] attributes,
            BuildSkillPriority[] skills, ConsumableEffect[] quickSlots, WeaponArchetype[] weapons)
        {
            Id = id;
            DisplayName = displayName;
            Attributes = Array.AsReadOnly(attributes);
            Skills = Array.AsReadOnly(skills);
            QuickSlots = Array.AsReadOnly(quickSlots);
            Weapons = Array.AsReadOnly(weapons);
        }

        public int SkillPriority(SkillDefinition skill, out int targetRank)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].Matches(skill))
                {
                    targetRank = Math.Min(Skills[i].TargetRank, skill.MaxRank);
                    return i + 1;
                }
            targetRank = 0;
            return 0;
        }

        public static IReadOnlyList<SessionBuildPreset> All { get; } = Array.AsReadOnly(new SessionBuildPreset[]
        {
            new("vanguard", "Vanguard", new[] { ItemStat.Vitality, ItemStat.Endurance, ItemStat.Strength },
                new BuildSkillPriority[] { new(SkillEffect.GuardEfficiency), new(SkillEffect.Poise), new(SkillEffect.MeleeTraining), new(SkillEffect.Health) },
                new[] { ConsumableEffect.Health, ConsumableEffect.Defense, ConsumableEffect.Stamina },
                new[] { WeaponArchetype.Greatsword, WeaponArchetype.Sword }),
            new("duelist", "Duelist", new[] { ItemStat.Endurance, ItemStat.Strength, ItemStat.Vitality },
                new BuildSkillPriority[] { new(SkillEffect.Stamina, 2), new(SkillEffect.AttackSpeed), new(SkillEffect.DodgeEfficiency) },
                new[] { ConsumableEffect.Stamina, ConsumableEffect.Health, ConsumableEffect.Damage },
                new[] { WeaponArchetype.Sword, WeaponArchetype.Spear }),
            new("battlemage", "Battlemage", new[] { ItemStat.Mind, ItemStat.Vitality, ItemStat.Endurance },
                new BuildSkillPriority[] { new(AbilityEffect.FireProjectile), new(SkillEffect.Mana), new(SkillEffect.SpellEfficiency), new(AbilityEffect.Shield) },
                new[] { ConsumableEffect.Mana, ConsumableEffect.Health, ConsumableEffect.Defense },
                new[] { WeaponArchetype.Staff, WeaponArchetype.Sword }),
            new("ranger", "Ranger", new[] { ItemStat.Dexterity, ItemStat.Endurance, ItemStat.Vitality },
                new BuildSkillPriority[] { new(SkillEffect.Stamina), new(SkillEffect.DodgeEfficiency), new(SkillEffect.Health), new(SkillEffect.PotionEfficiency) },
                new[] { ConsumableEffect.Stamina, ConsumableEffect.Health, ConsumableEffect.Damage },
                new[] { WeaponArchetype.Bow, WeaponArchetype.Spear })
        });

        public static SessionBuildPreset Find(string id)
        {
            foreach (var preset in All)
                if (string.Equals(preset.Id, id, StringComparison.Ordinal)) return preset;
            return null;
        }
    }
}
