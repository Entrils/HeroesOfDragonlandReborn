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
        private Collider framingCollider;
        public Transform Owner => stats != null ? stats.transform : transform;
        public float FramingHeight => framingCollider != null ? framingCollider.bounds.size.y : 0;
        public Vector3 FramingCenter => framingCollider != null && framingCollider.enabled ? framingCollider.bounds.center : AimPosition;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.TransformPoint(localAimOffset);
        public bool IsValid => isActiveAndEnabled && (stats == null || !stats.IsDead);
        private void Awake()
        {
            if (stats == null) stats = GetComponentInParent<CharacterStats>();
            framingCollider = Owner.GetComponent<Collider>();
            if (framingCollider == null && Owner != transform) framingCollider = GetComponent<Collider>();
        }
    }
}
