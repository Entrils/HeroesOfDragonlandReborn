using System;
using UnityEngine;

namespace HeroesOfDragonland.Progression
{
    [RequireComponent(typeof(SessionProgression))]
    public sealed class SessionProgressionDebug : MonoBehaviour
    {
        [SerializeField, Min(1)] private int debugXP = 500;
        [SerializeField] private HeroesOfDragonland.Items.ItemStat attribute = HeroesOfDragonland.Items.ItemStat.Vitality;
        [ContextMenu("Progression/Spend Point On Attribute")]
        private void SpendAttributePoint()
        {
            if (!Application.isPlaying) return;
            if (!Progression.TryIncreaseAttribute(attribute, Progression.SessionId, out string reason))
                Debug.LogWarning(reason, this);
            LogProgression();
        }
        private SessionProgression Progression => GetComponent<SessionProgression>();
        private void OnEnable() => Progression.LeveledUp += OnLevelUp;
        private void OnDisable() => Progression.LeveledUp -= OnLevelUp;
        private void OnLevelUp(LevelUpEvent value)
            => Debug.Log("Level " + value.Level + ": +" + value.PointsAwarded + " progression points.", this);
        [ContextMenu("Progression/Grant Debug XP")]
        private void GrantDebugXP()
        {
            if (!Application.isPlaying) return;
            if (!Progression.TryAddExperience(debugXP, Progression.SessionId, "debug:" + Guid.NewGuid().ToString("N"), out string reason))
                Debug.LogWarning(reason, this);
            LogProgression();
        }
        [ContextMenu("Progression/Log Session Progression")]
        private void LogProgression()
            => Debug.Log("Level " + Progression.Level + " | XP " + Progression.CurrentXP + "/"
                + Progression.XPRequiredForNextLevel + " | Points " + Progression.ProgressionPoints, this);
    }
}
