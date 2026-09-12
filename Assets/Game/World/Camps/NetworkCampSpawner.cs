using System;
using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(MultiplayerBootstrap))]
    public sealed class NetworkCampSpawner : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            [SerializeField] private NetworkCamp prefab;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private float yaw;
            internal NetworkCamp Prefab => prefab;
            internal Vector3 LocalPosition => localPosition;
            internal float Yaw => yaw;
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private MultiplayerBootstrap bootstrap;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void OnEnable() => bootstrap.SessionStarted += Spawn;
        private void OnDisable() => bootstrap.SessionStarted -= Spawn;
        private void Spawn(NetworkRunner runner)
        {
            if (!runner.IsServer) return;
            foreach (var entry in entries)
                if (entry.Prefab == null || entry.Prefab.Definition == null || !entry.Prefab.Definition.TryValidate(out _))
                    throw new InvalidOperationException("Assign valid network camps to the spawner.");
            foreach (var entry in entries)
                runner.Spawn(entry.Prefab.GetComponent<NetworkObject>(), transform.TransformPoint(entry.LocalPosition),
                    transform.rotation * Quaternion.Euler(0, entry.Yaw, 0), PlayerRef.None);
        }
    }
}
