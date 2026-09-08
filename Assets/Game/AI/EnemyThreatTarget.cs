using HeroesOfDragonland.Combat;
using UnityEngine;

namespace HeroesOfDragonland.AI
{
    // Opt-in target policy. Attach to players, not other neutral enemies/dummies.
    [DisallowMultipleComponent, RequireComponent(typeof(DamageReceiver))]
    public sealed class EnemyThreatTarget : MonoBehaviour
    {
        public DamageReceiver Receiver => GetComponent<DamageReceiver>();
        public bool IsAlive => isActiveAndEnabled && Receiver.IsAlive;
        public Vector3 AimPosition => transform.position + Vector3.up;
    }
}
