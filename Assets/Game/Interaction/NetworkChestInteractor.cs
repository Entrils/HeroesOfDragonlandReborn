using HeroesOfDragonland.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Multiplayer
{
    // Local intent/presentation only. The chest RPC resolves the actual avatar on the server.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkInventory))]
    public sealed class NetworkChestInteractor : MonoBehaviour
    {
        private NetworkInventory owner;
        private NetworkLootChest target;
        private NetworkLootChest pending;
        private float sentAt;
        private string message;
        private float messageUntil;
        private void Awake() => owner = GetComponent<NetworkInventory>();
        private void Update()
        {
            if (!owner.CanRequest) return;
            if (pending != null && Time.unscaledTime - sentAt > 3f)
            { Unsubscribe(); Show("No chest response. Try again."); }
            target = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<NetworkLootChest>())
            {
                if (candidate.Object == null || !candidate.Object.IsValid || candidate.Runner != owner.Runner || candidate.IsOpen) continue;
                var chest = candidate.GetComponent<LootChest>();
                float distance = (chest.InteractionPoint - (transform.position + Vector3.up)).sqrMagnitude;
                if (distance >= nearest || !HeroesOfDragonland.Interaction.InteractionReach.CanReach(
                    transform, chest.transform, chest.InteractionPoint, chest.InteractionRange)) continue;
                target = candidate;
                nearest = distance;
            }
            if (pending == null && target != null && !GetComponent<NetworkCharacterStats>().Dead
                && ((Keyboard.current?.fKey.wasPressedThisFrame ?? false)
                    || (Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false)))
            {
                pending = target;
                sentAt = Time.unscaledTime;
                pending.OpenResolved += Resolved;
                if (!pending.RequestOpen()) { Unsubscribe(); Show("Chest is unavailable."); }
            }
        }
        private void Resolved(bool accepted, string result) { Unsubscribe(); Show(result); }
        private void Show(string result) { message = result; messageUntil = Time.unscaledTime + 3f; }
        private void Unsubscribe()
        {
            if (pending != null) pending.OpenResolved -= Resolved;
            pending = null;
        }
        private void OnDisable() => Unsubscribe();
        private void OnGUI()
        {
            if (owner == null || !owner.CanRequest) return;
            string text = Time.unscaledTime < messageUntil ? message : pending != null ? "Opening chest..."
                : target != null ? "F / Y: Open chest — first opener gets the loot (either team)" : null;
            if (text != null) GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height - 65, 520, 30), text);
        }
    }
}
