using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    // Temporary procedural presentation; hit detection does not depend on this mesh.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(MeleeAttack))]
    public sealed class MeleeSwingVisual : MonoBehaviour
    {
        [SerializeField] private Transform swingPivot;
        private MeleeAttack attack;
        private void Awake() => attack = GetComponent<MeleeAttack>();
        private void LateUpdate()
        {
            if (swingPivot == null) return;
            swingPivot.gameObject.SetActive(attack.IsSwinging);
            if (attack.IsSwinging)
                swingPivot.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-65f, 65f, attack.SwingProgress), 0f);
        }
        private void OnDisable()
        {
            if (swingPivot != null) swingPivot.gameObject.SetActive(false);
        }
    }
}
