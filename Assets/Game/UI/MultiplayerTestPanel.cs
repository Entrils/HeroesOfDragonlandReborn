using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class MultiplayerTestPanel : MonoBehaviour
    {
        [SerializeField] private string room = "dragonland-m1";
        private MultiplayerBootstrap bootstrap;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void OnGUI()
        {
            if (Application.isBatchMode) return;
            GUILayout.BeginArea(new Rect(12, 12, 400, 310), GUI.skin.box);
            GUILayout.Label("Dragonland / Network melee combat");
            GUILayout.Label(bootstrap.Status);
            GUI.enabled = !bootstrap.Busy;
            if (!bootstrap.Running)
            {
                room = GUILayout.TextField(room, 64);
                if (GUILayout.Button("Host (one player + server)")) _ = bootstrap.StartSession(GameMode.Host, room);
                if (GUILayout.Button("Server (no local player)")) _ = bootstrap.StartSession(GameMode.Server, room);
                if (GUILayout.Button("Join client")) _ = bootstrap.StartSession(GameMode.Client, room);
            }
            else if (GUILayout.Button("Disconnect")) _ = bootstrap.Disconnect();
            GUI.enabled = true;
            GUILayout.Label("WASD: move your player. Blue: A / Red: B.");
            GUILayout.Label("6 players maximum / 3 per team.");
            GUILayout.Label("Space / left mouse: attack. Friendly fire: off.");
            GUILayout.Label("Left Shift: dodge. Middle mouse: lock / unlock.");
            GUILayout.Label("Q / E: switch local target.");
            var runner = bootstrap.Runner;
            if (runner != null && runner.IsRunning && runner.TryGetPlayerObject(runner.LocalPlayer, out var actor) && actor != null)
            {
                var resources = actor.GetComponent<NetworkPlayerResources>();
                if (resources != null)
                    GUILayout.Label("Stamina " + Mathf.CeilToInt(resources.Stamina) + " / " + resources.MaxStamina
                        + "   Mana " + Mathf.CeilToInt(resources.Mana) + " / " + resources.MaxMana);
            }
            GUILayout.EndArea();
        }
    }
}
