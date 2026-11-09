using Fusion;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterStats))]
    public sealed class NetworkCharacterStats : NetworkBehaviour, ICharacterStatsAuthority, IBeforeTick
    {
        [Networked] public float Health { get; private set; }
        [Networked] public float MaxHealth { get; private set; }
        [Networked] public NetworkBool Dead { get; private set; }
        private CharacterStats stats;
        private bool initializing;
        private bool IsServerOwner => Object != null && Object.IsValid && Runner.IsServer && HasStateAuthority;
        public bool CanMutateStats => IsServerOwner && (initializing || NetworkMatch.AllowsGameplay(Runner));

        private void Awake() => stats = GetComponent<CharacterStats>();
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                stats.HealthChanged += PublishHealth;
                InitializeForSpawn();
                PublishHealth(stats.CurrentHealth, stats.MaxHealth);
            }
            else ApplySnapshot();
        }
        internal void InitializeForSpawn(CharacterStatsConfig configuration = null)
        {
            if (!IsServerOwner) return;
            initializing = true;
            try
            {
                if (configuration != null) stats.ConfigureForSpawn(configuration);
                else stats.ResetToFull();
            }
            finally { initializing = false; }
        }
        public void BeforeTick()
        {
            if (CanMutateStats) stats.TickRegeneration(Runner.DeltaTime);
        }
        private void PublishHealth(float current, float maximum)
        {
            // The lethal health snapshot must still publish after its death event ends the match.
            if (!IsServerOwner) return;
            Health = current; MaxHealth = maximum; Dead = stats.IsDead;
        }
        private void ApplySnapshot()
        {
            if (MaxHealth > 0f) stats.ApplyReplicatedHealth(Health, MaxHealth);
        }
        public override void Render() { if (!HasStateAuthority) ApplySnapshot(); }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            stats.HealthChanged -= PublishHealth;
        }
    }
}
