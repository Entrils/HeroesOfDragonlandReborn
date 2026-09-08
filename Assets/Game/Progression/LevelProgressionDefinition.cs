using System;
using UnityEngine;

namespace HeroesOfDragonland.Progression
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Level Progression")]
    public sealed class LevelProgressionDefinition : ScriptableObject
    {
        [Serializable]
        private struct LevelStep
        {
            [SerializeField, Min(1)] private int requiredXP;
            [SerializeField, Min(0)] private int progressionPoints;
            public int RequiredXP => requiredXP;
            public int ProgressionPoints => progressionPoints;
        }
        [Tooltip("Entry 0 is level 1 to 2. The last entry determines the level cap.")]
        [SerializeField] private LevelStep[] levels = Array.Empty<LevelStep>();
        public int MaximumLevel => levels == null ? 1 : levels.Length + 1;

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (levels == null || levels.Length == 0 || levels.Length > 999)
                reason = "Configure 1 to 999 level transitions.";
            else foreach (var step in levels)
                if (step.RequiredXP <= 0 || step.ProgressionPoints < 0)
                { reason = "Each threshold must be positive and progression points nonnegative."; break; }
            return reason == null;
        }

        // Session copies keep live asset edits from changing thresholds mid-reward.
        internal void CopyCurve(out int[] thresholds, out int[] points)
        {
            thresholds = new int[levels.Length];
            points = new int[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            { thresholds[i] = levels[i].RequiredXP; points[i] = levels[i].ProgressionPoints; }
        }
    }
}
