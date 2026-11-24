using HeroesOfDragonland.Combat;
using UnityEngine;

namespace HeroesOfDragonland.Items
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Bow Profile")]
    public sealed class BowProfile : ScriptableObject
    {
        [SerializeField, Min(.05f)] private float drawSeconds = .65f;
        [SerializeField, Min(.05f)] private float releaseSeconds = .28f;
        [SerializeField, Min(1)] private float projectileSpeed = 34;
        [SerializeField, Range(0, 12)] private float assistanceDegrees = 6;
        [SerializeField] private ArrowDefinition basicArrow;
        [SerializeField] private GameObject projectileVisual;

        public float DrawSeconds => drawSeconds;
        public float ReleaseSeconds => releaseSeconds;
        public float ProjectileSpeed => projectileSpeed;
        public float AssistanceDegrees => assistanceDegrees;
        public ArrowDefinition BasicArrow => basicArrow;
        public GameObject ProjectileVisual => projectileVisual;
        public bool IsValid => float.IsFinite(drawSeconds) && drawSeconds >= .05f
            && float.IsFinite(releaseSeconds) && releaseSeconds >= .05f
            && float.IsFinite(projectileSpeed) && projectileSpeed >= 1
            && assistanceDegrees >= 0 && assistanceDegrees <= 12
            && basicArrow != null && basicArrow.IsValid && projectileVisual != null;
    }
}
