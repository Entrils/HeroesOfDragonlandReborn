using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Deployable Definition")]
    public sealed class DeployableDefinition : ScriptableObject
    {
        [SerializeField] private NetworkDeployable prefab;
        [SerializeField, Min(.1f)] private float placementRange = 5;
        [SerializeField, Min(.1f)] private float minimumSpacing = 2;
        [SerializeField, Min(.05f)] private float footprintRadius = .45f;
        [SerializeField, Min(.05f)] private float clearanceHeight = .35f;
        [SerializeField, Min(.05f)] private float triggerRadius = .8f;
        [SerializeField, Min(.05f)] private float effectRadius = .8f;
        [SerializeField, Min(0)] private float armingSeconds = .65f;
        [SerializeField, Min(1)] private float lifetime = 45;
        [SerializeField, Min(0)] private float damage;
        [SerializeField, Min(0)] private float movementSeconds;
        [SerializeField, Range(0, 1)] private float movementMultiplier = 1;
        [SerializeField] private bool friendlyFire;

        public NetworkDeployable Prefab => prefab;
        public float PlacementRange => placementRange;
        public float MinimumSpacing => minimumSpacing;
        public float FootprintRadius => footprintRadius;
        public float ClearanceHeight => clearanceHeight;
        public float TriggerRadius => triggerRadius;
        public float EffectRadius => effectRadius;
        public float ArmingSeconds => armingSeconds;
        public float Lifetime => lifetime;
        public float Damage => damage;
        public float MovementSeconds => movementSeconds;
        public float MovementMultiplier => movementMultiplier;
        public bool FriendlyFire => friendlyFire;
        public bool IsValid => prefab != null && prefab.Definition == this
            && Valid(placementRange, .1f) && Valid(minimumSpacing, .1f) && Valid(footprintRadius, .05f)
            && Valid(clearanceHeight, .05f) && Valid(triggerRadius, .05f) && Valid(effectRadius, triggerRadius)
            && Valid(armingSeconds, 0) && Valid(lifetime, 1) && lifetime > armingSeconds
            && Valid(damage, 0) && Valid(movementSeconds, 0) && Valid(movementMultiplier, 0) && movementMultiplier <= 1
            && (damage > 0 || (movementSeconds > 0 && movementMultiplier < 1));

        private static bool Valid(float value, float minimum) => float.IsFinite(value) && value >= minimum;
    }
}
