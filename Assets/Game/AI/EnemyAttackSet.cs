using System;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Enemy Attack Set")]
    public sealed class EnemyAttackSet : ScriptableObject
    {
        [Serializable]
        public struct Attack
        {
            [SerializeField] private string displayName;
            [SerializeField, Min(.1f)] private float radius;
            [SerializeField, Range(1, 360)] private float angle;
            [SerializeField, Min(.1f)] private float windup;
            [SerializeField, Min(.1f)] private float recovery;
            [SerializeField, Min(0)] private float damage;
            public string DisplayName => displayName;
            public float Radius => Mathf.Max(.1f, radius);
            public float Angle => Mathf.Clamp(angle, 1, 360);
            public float Windup => Mathf.Max(.1f, windup);
            public float Recovery => Mathf.Max(.1f, recovery);
            public float Damage => Mathf.Max(0, damage);

            public bool Contains(Vector3 origin, Vector3 forward, Vector3 point)
            {
                var delta = point - origin;
                if (Mathf.Abs(delta.y) > 3) return false;
                delta.y = 0; forward.y = 0;
                return delta.sqrMagnitude <= Radius * Radius
                    && (Angle >= 359 || delta.sqrMagnitude < .001f || Vector3.Angle(forward, delta) <= Angle * .5f);
            }
        }
        [SerializeField] private Attack[] attacks = Array.Empty<Attack>();
        public int Count => attacks?.Length ?? 0;
        public Attack Get(int index) => attacks[Mathf.Clamp(index, 0, Count - 1)];
    }
}
