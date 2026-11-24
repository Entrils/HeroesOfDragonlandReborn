using UnityEngine;

namespace HeroesOfDragonland.World
{
    [DisallowMultipleComponent, RequireComponent(typeof(LootChest))]
    public sealed class LootChestVisual : MonoBehaviour
    {
        [SerializeField] private Transform lidPivot;
        [SerializeField] private float lidOpenAngle = 105f;
        [SerializeField] private Transform label;
        private LootChest chest;
        private Quaternion closedLidRotation;
        private void Awake()
        {
            chest = GetComponent<LootChest>();
            if (lidPivot != null) closedLidRotation = lidPivot.localRotation;
        }
        private void OnEnable() { chest.Changed += Refresh; Refresh(); }
        private void OnDisable() => chest.Changed -= Refresh;
        private void Refresh()
        {
            if (lidPivot != null)
                lidPivot.localRotation = Quaternion.Euler(chest.IsOpen ? lidOpenAngle : 0, 0, 0) * closedLidRotation;
        }
        private void LateUpdate()
        {
            if (label != null && Camera.main != null) label.rotation = Camera.main.transform.rotation;
        }
    }
}
