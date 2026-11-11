using Fusion;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Private resource snapshots are sent only to the owning player.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkCharacterStats))]
    public sealed class NetworkPlayerResources : NetworkBehaviour
    {
        [Networked] public float Stamina { get; private set; }
        [Networked] public float MaxStamina { get; private set; }
        [Networked] public float Mana { get; private set; }
        [Networked] public float MaxMana { get; private set; }
        private CharacterStats stats;
        private void Awake() => stats = GetComponent<CharacterStats>();
        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            ReplicateToAll(false);
            if (Object.InputAuthority != PlayerRef.None) ReplicateTo(Object.InputAuthority, true);
            stats.StaminaChanged += PublishStamina;
            stats.ManaChanged += PublishMana;
            PublishStamina(stats.CurrentStamina, stats.MaxStamina);
            PublishMana(stats.CurrentMana, stats.MaxMana);
        }
        private void PublishStamina(float current, float maximum)
        {
            if (!HasStateAuthority) return;
            Stamina = current; MaxStamina = maximum;
        }
        private void PublishMana(float current, float maximum)
        {
            if (!HasStateAuthority) return;
            Mana = current; MaxMana = maximum;
        }
        public override void Render()
        {
            if (!HasStateAuthority && HasInputAuthority)
                stats.ApplyReplicatedResources(Stamina, MaxStamina, Mana, MaxMana);
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            stats.StaminaChanged -= PublishStamina;
            stats.ManaChanged -= PublishMana;
        }
    }
}
