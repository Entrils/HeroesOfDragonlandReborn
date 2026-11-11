using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.Player
{
    // Input-layer ownership, independent of any UI. Never pauses the network simulation.
    [DisallowMultipleComponent]
    public sealed class LocalGameplayInputGate : MonoBehaviour
    {
        private readonly HashSet<Object> owners = new();
        private bool awaitingRelease;
        public bool IsBlocked => owners.Count > 0 || awaitingRelease;
        public void SetBlocked(Object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) owners.Add(owner);
            else if (owners.Remove(owner)) awaitingRelease = true;
        }
        private void Update()
        {
            if (owners.RemoveWhere(owner => owner == null) > 0) awaitingRelease = true;
            if (owners.Count > 0 || !awaitingRelease) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            // Closing a panel with mouse/Space must not leak that held press into combat.
            awaitingRelease = (mouse != null && (mouse.leftButton.isPressed || mouse.middleButton.isPressed || mouse.rightButton.isPressed))
                || (keyboard != null && (keyboard.spaceKey.isPressed || keyboard.leftShiftKey.isPressed
                    || keyboard.rKey.isPressed || keyboard.cKey.isPressed || keyboard.qKey.isPressed || keyboard.eKey.isPressed));
        }
        private void OnDisable() { owners.Clear(); awaitingRelease = false; }
    }
}
