using System;
using HeroesOfDragonland.Player;
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
        public event Action<bool, string> InteractionResolved;
        public string Prompt => CanInteract ? pending != null ? "Opening chest…"
            : target != null ? "Open loot chest  [F / Y]" : "" : "";
        private bool CanInteract => owner != null && owner.CanRequest && Application.isFocused
            && NetworkMatch.AllowsGameplay(owner.Runner) && !GetComponent<NetworkCharacterStats>().Dead
            && owner.Runner.GetComponentInParent<LocalGameplayInputGate>() is not { IsBlocked: true };
        private void Awake() => owner = GetComponent<NetworkInventory>();
        private void Update()
        {
            if (!owner.CanRequest) return;
            if (pending != null && Time.unscaledTime - sentAt > 3f)
            { Unsubscribe(); Show("No chest response. Try again."); }
            target = null;
            if (!CanInteract) return;
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
            if (pending == null && target != null
                && ((Keyboard.current?.fKey.wasPressedThisFrame ?? false)
                    || (Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false)))
            {
                pending = target;
                sentAt = Time.unscaledTime;
                pending.OpenResolved += Resolved;
                if (!pending.RequestOpen()) { Unsubscribe(); Show("Chest is unavailable."); }
            }
        }
        private void Resolved(bool accepted, string result) { Unsubscribe(); Show(result, accepted); }
        private void Show(string result, bool accepted = false)
        {
            message = result;
            messageUntil = Time.unscaledTime + 3f;
            InteractionResolved?.Invoke(accepted, result);
        }
        private void Unsubscribe()
        {
            if (pending != null) pending.OpenResolved -= Resolved;
            pending = null;
        }
        private void OnDisable() => Unsubscribe();
        private void OnGUI()
        {
            if (InteractionResolved != null || !CanInteract) return;
            string text = Time.unscaledTime < messageUntil ? message : Prompt;
            if (!string.IsNullOrEmpty(text)) GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height - 65, 520, 30), text);
        }
    }
}
