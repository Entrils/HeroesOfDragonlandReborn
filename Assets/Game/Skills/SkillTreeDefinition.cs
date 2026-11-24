using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Skills
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Skill Tree")]
    public sealed class SkillTreeDefinition : ScriptableObject
    {
        [SerializeField] private SkillDefinition[] nodes = Array.Empty<SkillDefinition>();
        public int Count => nodes?.Length ?? 0;
        public SkillDefinition GetNode(int index) => index >= 0 && index < Count ? nodes[index] : null;
        public int IndexOf(SkillDefinition node) => nodes == null ? -1 : Array.IndexOf(nodes, node);

        public bool IsValid()
        {
            if (Count == 0 || Count > SessionSkills.Capacity) return false;
            var unique = new HashSet<SkillDefinition>();
            foreach (var node in nodes)
                if (node == null || !node.IsValid || !unique.Add(node)) return false;

            var completed = new HashSet<SkillDefinition>();
            var visiting = new HashSet<SkillDefinition>();
            foreach (var node in nodes)
                if (!ValidatePrerequisites(node, unique, completed, visiting)) return false;
            return true;
        }

        private static bool ValidatePrerequisites(SkillDefinition node, HashSet<SkillDefinition> nodes,
            HashSet<SkillDefinition> completed, HashSet<SkillDefinition> visiting)
        {
            if (completed.Contains(node)) return true;
            if (!visiting.Add(node)) return false;
            foreach (var prerequisite in node.Prerequisites)
            {
                var dependency = prerequisite.Skill;
                if (dependency == null || !nodes.Contains(dependency) || prerequisite.Rank < 1
                    || prerequisite.Rank > dependency.MaxRank
                    || (!string.IsNullOrEmpty(node.ExclusiveGroup) && node.ExclusiveGroup == dependency.ExclusiveGroup)
                    || !ValidatePrerequisites(dependency, nodes, completed, visiting)) return false;
            }
            visiting.Remove(node);
            completed.Add(node);
            return true;
        }
    }
}
