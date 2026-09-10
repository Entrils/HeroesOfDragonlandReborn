using HeroesOfDragonland.Combat;
using HeroesOfDragonland.World;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(KingObjective))]
    public sealed class KingObjectiveDebug : MonoBehaviour
    {
        [SerializeField] private GameObject attacker;
        [SerializeField, Min(1)] private float damage = 250;
        [ContextMenu("Match/Apply Debug Damage To King")]
        private void ApplyDebugDamage()
        {
            if (!Application.isPlaying || !(Application.isEditor || Debug.isDebugBuild)) return;
            float applied = GetComponent<DamageReceiver>().TakeDamage(damage, attacker);
            Debug.Log("King damage applied: " + applied + " | Health: " + GetComponent<KingObjective>().Stats.CurrentHealth, this);
        }
    }
}
