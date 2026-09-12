using HeroesOfDragonland.Combat;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    // Opt-in target policy. Attach to players, not other neutral enemies/dummies.
    [DisallowMultipleComponent, RequireComponent(typeof(DamageReceiver))]
    public sealed class EnemyThreatTarget : MonoBehaviour
    {
        [Tooltip("Offset from this root to the navigation/feet position (center-rooted network players use y=-1).")]
        [SerializeField] private Vector3 navigationOffset;
        public Vector3 NavigationPosition => transform.position + navigationOffset;
        public DamageReceiver Receiver => GetComponent<DamageReceiver>();
        public bool IsAlive => isActiveAndEnabled && Receiver.IsAlive;
        public Vector3 AimPosition => transform.position + Vector3.up;
    }
}
