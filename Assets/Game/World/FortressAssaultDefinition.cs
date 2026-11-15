using System;
using System.Collections.Generic;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.World
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Fortress Assault")]
    public sealed class FortressAssaultDefinition : ScriptableObject
    {
        [Serializable]
        public struct GateEntry
        {
            [SerializeField] private NetworkFortressGate prefab;
            [SerializeField] private Vector3 position;
            [SerializeField] private float yaw;
            public NetworkFortressGate Prefab => prefab;
            public Vector3 Position => position;
            public Quaternion Rotation => Quaternion.Euler(0, yaw, 0);
        }
        [Serializable]
        public struct GuardPost
        {
            [SerializeField] private NetworkEnemy prefab;
            [SerializeField] private Vector3 position;
            [SerializeField] private float yaw;
            public NetworkEnemy Prefab => prefab;
            public Vector3 Position => position;
            public Quaternion Rotation => Quaternion.Euler(0, yaw, 0);
        }
        [SerializeField] private GateEntry[] gates = Array.Empty<GateEntry>();
        [SerializeField] private GuardPost[] guards = Array.Empty<GuardPost>();
        public IReadOnlyList<GateEntry> Gates => gates;
        public IReadOnlyList<GuardPost> Guards => guards;
    }
}
