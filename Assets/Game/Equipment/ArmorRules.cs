using UnityEngine;

namespace HeroesOfDragonland.EquipmentSystem
{
    public enum EquipmentWeightClass { Light, Medium, Heavy }

    [System.Serializable]
    public struct WeightDodgeModifiers
    {
        [Min(0.01f)] public float distance;
        [Min(0.01f)] public float duration;
        [Min(0.01f)] public float recovery;
        [Min(0.01f)] public float stamina;
        public WeightDodgeModifiers(float distance, float duration, float recovery, float stamina)
        { this.distance = distance; this.duration = duration; this.recovery = recovery; this.stamina = stamina; }
        public static WeightDodgeModifiers Light => new WeightDodgeModifiers(1, 1, 1, 1);
        public static WeightDodgeModifiers Medium => new WeightDodgeModifiers(0.85f, 1.1f, 1.25f, 1.2f);
        public static WeightDodgeModifiers Heavy => new WeightDodgeModifiers(0.65f, 1.25f, 1.6f, 1.5f);
    }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Armor Rules")]
    public sealed class ArmorRules : ScriptableObject
    {
        [SerializeField, Min(0)] private float mediumWeight = 15;
        [SerializeField, Min(0)] private float heavyWeight = 30;
        [Tooltip("Physical damage = incoming * scale / (scale + physical defense).")]
        [SerializeField, Min(0.01f)] private float defenseScale = 100;
        [SerializeField] private WeightDodgeModifiers light = WeightDodgeModifiers.Light;
        [SerializeField] private WeightDodgeModifiers medium = WeightDodgeModifiers.Medium;
        [SerializeField] private WeightDodgeModifiers heavy = WeightDodgeModifiers.Heavy;
        public EquipmentWeightClass Classify(float weight) => weight >= Mathf.Max(mediumWeight, heavyWeight)
            ? EquipmentWeightClass.Heavy : weight >= mediumWeight ? EquipmentWeightClass.Medium : EquipmentWeightClass.Light;
        public WeightDodgeModifiers GetDodgeModifiers(float weight) => Classify(weight) switch
        { EquipmentWeightClass.Heavy => heavy, EquipmentWeightClass.Medium => medium, _ => light };
        public float ReducePhysicalDamage(float amount, float defense)
            => amount * (Mathf.Max(0.01f, defenseScale) / (Mathf.Max(0.01f, defenseScale) + Mathf.Max(0, defense)));
    }
}
