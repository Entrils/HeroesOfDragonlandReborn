using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class NetworkFortressSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkMatch matchPrefab;
        [SerializeField] private SessionRespawnConfig respawnConfig;
        public SessionRespawnConfig RespawnConfig => respawnConfig;
        public bool TryChooseRespawnPose(NetworkRunner runner, NetworkTestPlayer player, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            var service = GetComponent<TeamSpawnService>();
            return service != null && player != null && player.Runner == runner
                && service.TryChooseSpawn(runner, player.Team, player.transform, out position, out rotation);
        }
        private MultiplayerBootstrap bootstrap;
        public NetworkMatch Match { get; private set; }
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void OnEnable() => bootstrap.SessionStarted += Spawn;
        private void OnDisable() => bootstrap.SessionStarted -= Spawn;
        internal void Bind(NetworkMatch match) { if (match.Runner == bootstrap.Runner) Match = match; }
        internal void Unbind(NetworkMatch match) { if (Match == match) Match = null; }
        private void Spawn(NetworkRunner runner)
        {
            if (!runner.IsServer) return;
            if (matchPrefab == null) throw new System.InvalidOperationException("Assign the network fortress match prefab.");
            runner.Spawn(matchPrefab, Vector3.zero, Quaternion.identity, PlayerRef.None);
        }
    }
}
