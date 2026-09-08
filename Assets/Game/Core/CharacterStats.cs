using System;
using UnityEngine;

namespace HeroesOfDragonland.Stats
{
    [DisallowMultipleComponent]
    public sealed class CharacterStats : MonoBehaviour
    {
        [SerializeField] private CharacterStatsConfig configuration;
        private ResourcePool health;
        private ResourcePool stamina;
        private ResourcePool mana;

        // Changed events provide (current, maximum). Read properties for initial UI state.
        public event Action<float, float> HealthChanged;
        public event Action<float, float> StaminaChanged;
        public event Action<float, float> ManaChanged;
        public event Action Died;

        public float CurrentHealth { get { EnsureInitialized(); return health.Current; } }
        public float MaxHealth { get { EnsureInitialized(); return health.Maximum; } }
        public float CurrentStamina { get { EnsureInitialized(); return stamina.Current; } }
        public float MaxStamina { get { EnsureInitialized(); return stamina.Maximum; } }
        public float CurrentMana { get { EnsureInitialized(); return mana.Current; } }
        public float MaxMana { get { EnsureInitialized(); return mana.Maximum; } }
        public bool IsDead => CurrentHealth <= 0f;

        private void Awake() => EnsureInitialized();

        // Explicit spawn initialization for data-driven actors. This resets resources;
        // never call it for an ordinary equipment change or during combat.
        public void ConfigureForSpawn(CharacterStatsConfig config)
        {
            configuration = config;
            health = null;
            stamina = null;
            mana = null;
            ResetToFull();
        }

        private void EnsureInitialized()
        {
            if (health != null) return;
            // Defaults allow the component to work on any actor without a required asset.
            health = new ResourcePool(configuration != null ? Mathf.Max(1f, configuration.MaxHealth) : 100f);
            stamina = new ResourcePool(configuration != null ? configuration.MaxStamina : 100f,
                configuration != null ? configuration.StaminaRegenPerSecond : 20f,
                configuration != null ? configuration.StaminaRegenDelay : 1f);
            mana = new ResourcePool(configuration != null ? configuration.MaxMana : 100f,
                configuration != null ? configuration.ManaRegenPerSecond : 5f,
                configuration != null ? configuration.ManaRegenDelay : 2f);
        }

        public float ApplyDamage(float amount)
        {
            if (IsDead) return 0f;
            foreach (var provider in GetComponents<HeroesOfDragonland.Combat.IInvulnerabilityProvider>())
                if (provider.IsInvulnerable) return 0f;
            float removed = health.Remove(amount);
            if (removed <= 0f) return 0f;
            bool died = health.Current == 0f;
            HealthChanged?.Invoke(health.Current, health.Maximum);
            if (died) Died?.Invoke();
            return removed;
        }

        // Healing does not revive. ResetToFull is the explicit spawn/reset operation.
        public float Heal(float amount)
        {
            if (IsDead) return 0f;
            float restored = health.Restore(amount);
            if (restored > 0f) HealthChanged?.Invoke(health.Current, health.Maximum);
            return restored;
        }

        public bool TrySpendStamina(float amount)
        {
            if (IsDead) return false;
            float before = stamina.Current;
            bool success = stamina.TrySpend(amount);
            if (stamina.Current != before) StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            return success;
        }

        public bool TrySpendMana(float amount)
        {
            if (IsDead) return false;
            float before = mana.Current;
            bool success = mana.TrySpend(amount);
            if (mana.Current != before) ManaChanged?.Invoke(mana.Current, mana.Maximum);
            return success;
        }

        private void Update() => TickRegeneration(Time.deltaTime);

        // Uses scaled game time. Do not call manually while automatic Update is running.
        public void TickRegeneration(float deltaTime)
        {
            if (IsDead) return;
            float oldStamina = stamina.Current;
            float oldMana = mana.Current;
            stamina.Tick(deltaTime);
            mana.Tick(deltaTime);
            if (stamina.Current != oldStamina) StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            if (mana.Current != oldMana) ManaChanged?.Invoke(mana.Current, mana.Maximum);
        }

        public void ResetToFull()
        {
            EnsureInitialized();
            health.ResetToFull();
            stamina.ResetToFull();
            mana.ResetToFull();
            HealthChanged?.Invoke(health.Current, health.Maximum);
            StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            ManaChanged?.Invoke(mana.Current, mana.Maximum);
        }
    }
}
