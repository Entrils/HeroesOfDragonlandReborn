using UnityEngine;

namespace HeroesOfDragonland.World
{
    [DisallowMultipleComponent, RequireComponent(typeof(LootChest))]
    public sealed class LootChestVisual : MonoBehaviour
    {
        [SerializeField] private Transform lidPivot;
        [SerializeField] private Transform label;
        private LootChest chest;
        private void Awake() => chest = GetComponent<LootChest>();
        private void OnEnable() { chest.Changed += Refresh; Refresh(); }
        private void OnDisable() => chest.Changed -= Refresh;
        private void Refresh()
        {
            if (lidPivot != null) lidPivot.localRotation = Quaternion.Euler(chest.IsOpen ? 105 : 0, 0, 0);
        }
        private void LateUpdate()
        {
            if (label != null && Camera.main != null) label.rotation = Camera.main.transform.rotation;
        }
    }
}
