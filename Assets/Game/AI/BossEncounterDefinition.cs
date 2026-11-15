using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Boss Encounter")]
    public sealed class BossEncounterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Stone Warden";
        [SerializeField] private EnemyAttackSet phaseTwoAttacks;
        [SerializeField, Range(.1f, .9f)] private float phaseTwoHealth = .5f;
        [SerializeField, Min(.5f)] private float transitionDuration = 2.5f;
        [SerializeField, Min(1)] private float targetSwitchInterval = 7f;
        [SerializeField, Min(1)] private float enragedSpeedMultiplier = 1.25f;
        [SerializeField, Min(5)] private float healthBarDistance = 24f;
        public string DisplayName => displayName;
        public EnemyAttackSet PhaseTwoAttacks => phaseTwoAttacks;
        public float PhaseTwoHealth => Mathf.Clamp(phaseTwoHealth, .1f, .9f);
        public float TransitionDuration => Mathf.Max(.5f, transitionDuration);
        public float TargetSwitchInterval => Mathf.Max(1, targetSwitchInterval);
        public float EnragedSpeedMultiplier => Mathf.Max(1, enragedSpeedMultiplier);
        public float HealthBarDistance => Mathf.Max(5, healthBarDistance);
    }
}
