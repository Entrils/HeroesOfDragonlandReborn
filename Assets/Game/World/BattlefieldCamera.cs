using Fusion;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroesOfDragonland.World
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class BattlefieldCamera : MonoBehaviour
    {
        [SerializeField] private MultiplayerBootstrap bootstrap;
        [SerializeField] private Vector3 followOffset = new Vector3(0, 21, -16);
        [SerializeField] private float localSize = 13;
        [SerializeField] private float overviewSize = 50;
        private Camera view;
        private bool overview;
        private void Awake() => view = GetComponent<Camera>();
        private void Update()
        {
            // Live play uses attack angles, not concealment. The overview is a lobby inspection tool.
            if (IsPlaying) overview = false;
            else if (Application.isFocused && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) overview = !overview;
        }
        private bool IsPlaying => bootstrap != null && bootstrap.GetComponent<NetworkFortressSpawner>()?.Match != null
            && bootstrap.GetComponent<NetworkFortressSpawner>().Match.State == MatchPhase.Playing;
        private void LateUpdate()
        {
            var runner = bootstrap != null ? bootstrap.Runner : null;
            bool hasLocal = runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None
                && runner.TryGetPlayerObject(runner.LocalPlayer, out var actor) && actor != null && actor.IsValid;
            Vector3 focus = Vector3.zero;
            if (hasLocal && !overview && runner.TryGetPlayerObject(runner.LocalPlayer, out var local)) focus = local.transform.position;
            bool wide = !IsPlaying && (overview || !hasLocal);
            view.orthographicSize = wide ? overviewSize : localSize;
            transform.position = focus + (wide ? new Vector3(0, 82, -56) : followOffset);
            transform.LookAt(focus);
        }
    }
}
