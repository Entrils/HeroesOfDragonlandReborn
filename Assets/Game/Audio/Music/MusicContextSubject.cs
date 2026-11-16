using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    // Passive local registry. No update loop, authority callbacks, network fields or RPCs.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class MusicContextSubject : MonoBehaviour
    {
        private static readonly HashSet<MusicContextSubject> active = new();
        public static IEnumerable<MusicContextSubject> Active => active;
        public NetworkObject Actor { get; private set; }
        public NetworkTestPlayer Player { get; private set; }
        public NetworkEnemy Enemy { get; private set; }
        public NetworkPlayerCombat Combat { get; private set; }
        public NetworkCharacterStats Stats { get; private set; }
        public NetworkKingObjective King { get; private set; }
        public NetworkFortressGate Gate { get; private set; }
        public NetworkFortressGuard Guard { get; private set; }
        public TeamId Team => VfxAudience.Team(gameObject);
        public bool Valid => isActiveAndEnabled && Actor != null && Actor.IsValid && Stats != null && Stats.MaxHealth > 0;
        public bool Alive => Valid && !Stats.Dead && (Player == null || !Player.IsSuspended);
        public double DamagedAt { get; private set; } = double.NegativeInfinity;
        private float health;
        private int round = -1;
        private bool seeded;
        private void Awake()
        {
            Actor = GetComponent<NetworkObject>(); Player = GetComponent<NetworkTestPlayer>(); Enemy = GetComponent<NetworkEnemy>();
            Stats = GetComponent<NetworkCharacterStats>(); Combat = GetComponent<NetworkPlayerCombat>();
            King = GetComponent<NetworkKingObjective>(); Gate = GetComponent<NetworkFortressGate>(); Guard = GetComponent<NetworkFortressGuard>();
        }
        private void OnEnable() { active.Add(this); Reset(); }
        private void OnDisable() { active.Remove(this); Reset(); }
        private void Reset() { seeded = false; round = -1; DamagedAt = double.NegativeInfinity; }
        public void SampleHealth(int currentRound, double now)
        {
            if (!Valid) { Reset(); return; }
            if (!seeded || round != currentRound) { seeded = true; DamagedAt = double.NegativeInfinity; }
            else if (Stats.Health < health - .001f) DamagedAt = now;
            // Respawn/round restoration cannot retain a previous-life damage lease.
            else if (Stats.Health > health + .001f) DamagedAt = double.NegativeInfinity;
            health = Stats.Health; round = currentRound;
        }
    }
}
