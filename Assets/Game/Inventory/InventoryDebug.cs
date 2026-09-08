using UnityEngine;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.InventorySystem
{
    // Inspector context menus only; no UI or input dependencies in inventory logic.
    [DisallowMultipleComponent, RequireComponent(typeof(Inventory))]
    public sealed class InventoryDebug : MonoBehaviour
    {
        [SerializeField] private ItemDefinition testItem;
        [SerializeField, Min(1)] private int amount = 1;
        private bool CanRun => Application.isPlaying && (Application.isEditor || Debug.isDebugBuild);
        [ContextMenu("Inventory/Add Test Item")]
        private void Add() { if (CanRun) Debug.Log("Inventory add: " + GetComponent<Inventory>().TryAdd(testItem, amount), this); }
        [ContextMenu("Inventory/Remove Test Item")]
        private void Remove() { if (CanRun) Debug.Log("Inventory remove: " + GetComponent<Inventory>().TryRemove(testItem, amount), this); }
        [ContextMenu("Inventory/Log Snapshot")]
        private void Dump() { if (CanRun) Debug.Log(JsonUtility.ToJson(GetComponent<Inventory>().CaptureSnapshot(), true), this); }
        [ContextMenu("Inventory/Clear Session Items")]
        private void Clear() { if (CanRun) GetComponent<Inventory>().Clear(); }
    }
}
