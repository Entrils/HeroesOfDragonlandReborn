using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.UI.Minimap
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class TacticalMapSubject : MonoBehaviour
    {
        private static readonly HashSet<TacticalMapSubject> active = new();
        public static IEnumerable<TacticalMapSubject> Active => active;
        public NetworkObject Actor { get; private set; }
        public NetworkTestPlayer Player { get; private set; }
        public NetworkCharacterStats Stats { get; private set; }
        public NetworkCamp Camp { get; private set; }
        public NetworkFortressGate Gate { get; private set; }
        public bool Valid => isActiveAndEnabled && Actor != null && Actor.IsValid;
        private void Awake()
        {
            Actor = GetComponent<NetworkObject>(); Player = GetComponent<NetworkTestPlayer>(); Stats = GetComponent<NetworkCharacterStats>();
            Camp = GetComponent<NetworkCamp>(); Gate = GetComponent<NetworkFortressGate>();
        }
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
    }
}
