using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Targeting
{
    // Opt-in enemy target marker. No AI or combat implementation is required.
    [DisallowMultipleComponent]
    public sealed class Targetable : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Vector3 localAimOffset = Vector3.up;
        [SerializeField] private CharacterStats stats;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.TransformPoint(localAimOffset);
        public bool IsValid => isActiveAndEnabled && (stats == null || !stats.IsDead);
        private void Awake()
        {
            if (stats == null) stats = GetComponentInParent<CharacterStats>();
        }
    }
}
