using System;
using UnityEngine;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.Items;

namespace HeroesOfDragonland.Stats
{
    [DisallowMultipleComponent]
    public sealed class CharacterStats : MonoBehaviour
    {
        [SerializeField] private CharacterStatsConfig configuration;
        private ResourcePool health;
        private ResourcePool stamina;
        private ResourcePool mana;
        private CharacterAttributes attributes;
        public bool CanMutate => GetComponent<ICharacterStatsAuthority>()?.CanMutateStats ?? true;
        public bool UsesNetworkClock => GetComponent<ICharacterStatsAuthority>() != null;

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
        public float StaggerResistance => configuration != null ? configuration.StaggerResistance : 50f;
        public bool IsDead => CurrentHealth <= 0f;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            attributes = GetComponent<CharacterAttributes>();
            if (attributes != null) attributes.Changed += RefreshMaximums;
            RefreshMaximums();
        }
        private void OnDisable()
        {
            if (attributes != null) attributes.Changed -= RefreshMaximums;
        }
        private float CalculateMaximum(ItemStat stat, float baseline)
        {
            var source = GetComponent<CharacterAttributes>();
            return source != null ? source.CalculateResourceMaximum(stat, baseline) : baseline;
        }
        private void RefreshMaximums()
        {
            if (!CanMutate) return;
            EnsureInitialized();
            float hp = CalculateMaximum(ItemStat.MaxHealth, configuration != null ? configuration.MaxHealth : 100f);
            float sp = CalculateMaximum(ItemStat.MaxStamina, configuration != null ? configuration.MaxStamina : 100f);
            float mp = CalculateMaximum(ItemStat.MaxMana, configuration != null ? configuration.MaxMana : 100f);
            bool h = health.Maximum != hp, s = stamina.Maximum != sp, m = mana.Maximum != mp;
            health.SetMaximum(hp); stamina.SetMaximum(sp); mana.SetMaximum(mp);
            if (h) HealthChanged?.Invoke(health.Current, health.Maximum);
            if (s) StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            if (m) ManaChanged?.Invoke(mana.Current, mana.Maximum);
        }

        // Explicit spawn initialization for data-driven actors. This resets resources;
        // never call it for an ordinary equipment change or during combat.
        public void ConfigureForSpawn(CharacterStatsConfig config)
        {
            if (!CanMutate) return;
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
            health = new ResourcePool(CalculateMaximum(ItemStat.MaxHealth, configuration != null ? Mathf.Max(1f, configuration.MaxHealth) : 100f));
            stamina = new ResourcePool(CalculateMaximum(ItemStat.MaxStamina, configuration != null ? configuration.MaxStamina : 100f),
                configuration != null ? configuration.StaminaRegenPerSecond : 20f,
                configuration != null ? configuration.StaminaRegenDelay : 1f);
            mana = new ResourcePool(CalculateMaximum(ItemStat.MaxMana, configuration != null ? configuration.MaxMana : 100f),
                configuration != null ? configuration.ManaRegenPerSecond : 5f,
                configuration != null ? configuration.ManaRegenDelay : 2f);
        }

        public float ApplyDamage(float amount)
        {
            if (!CanMutate || IsDead) return 0f;
            foreach (var provider in GetComponents<HeroesOfDragonland.Combat.IInvulnerabilityProvider>())
                if (provider.IsInvulnerable) return 0f;
            float removed = health.Remove(amount);
            if (removed <= 0f) return 0f;
            bool died = health.Current == 0f;
            // Commit death before health/UI observers can issue another combat action.
            // One failing listener must not prevent objective/death listeners running.
            if (died && Died != null)
                foreach (Action handler in Died.GetInvocationList())
                    try { handler(); } catch (Exception exception) { Debug.LogException(exception, this); }
            HealthChanged?.Invoke(health.Current, health.Maximum);
            return removed;
        }

        // Healing does not revive. ResetToFull is the explicit spawn/reset operation.
        public float Heal(float amount)
        {
            if (!CanMutate || IsDead) return 0f;
            float restored = health.Restore(amount);
            if (restored > 0f) HealthChanged?.Invoke(health.Current, health.Maximum);
            return restored;
        }

        public bool TrySpendStamina(float amount)
        {
            if (!CanMutate || IsDead) return false;
            float before = stamina.Current;
            bool success = stamina.TrySpend(amount);
            if (stamina.Current != before) StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            return success;
        }

        public bool TrySpendMana(float amount)
        {
            if (!CanMutate || IsDead) return false;
            float before = mana.Current;
            bool success = mana.TrySpend(amount);
            if (mana.Current != before) ManaChanged?.Invoke(mana.Current, mana.Maximum);
            return success;
        }

        private void Update() { if (!UsesNetworkClock) TickRegeneration(Time.deltaTime); }

        // Uses scaled game time. Do not call manually while automatic Update is running.
        public void TickRegeneration(float deltaTime)
        {
            if (!CanMutate || IsDead) return;
            float oldStamina = stamina.Current;
            float oldMana = mana.Current;
            GetComponent<HeroesOfDragonland.Combat.CombatDefense>()?.Tick(deltaTime);
            stamina.Tick(deltaTime);
            mana.Tick(deltaTime);
            if (stamina.Current != oldStamina) StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            if (mana.Current != oldMana) ManaChanged?.Invoke(mana.Current, mana.Maximum);
        }

        public void ResetToFull()
        {
            if (!CanMutate) return;
            EnsureInitialized();
            health.ResetToFull();
            stamina.ResetToFull();
            mana.ResetToFull();
            HealthChanged?.Invoke(health.Current, health.Maximum);
            StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            ManaChanged?.Invoke(mana.Current, mana.Maximum);
        }

        public void RestoreForRespawn(HeroesOfDragonland.Multiplayer.RespawnResourcePolicy manaPolicy,
            HeroesOfDragonland.Multiplayer.RespawnResourcePolicy staminaPolicy)
        {
            if (!CanMutate || !IsDead) return;
            RestoreResource(mana, manaPolicy);
            RestoreResource(stamina, staminaPolicy);
            health.ResetToFull();
            HealthChanged?.Invoke(health.Current, health.Maximum);
            StaminaChanged?.Invoke(stamina.Current, stamina.Maximum);
            ManaChanged?.Invoke(mana.Current, mana.Maximum);
        }
        private static void RestoreResource(ResourcePool pool, HeroesOfDragonland.Multiplayer.RespawnResourcePolicy policy)
        {
            if (policy == HeroesOfDragonland.Multiplayer.RespawnResourcePolicy.Preserve) return;
            pool.ResetToFull();
            if (policy == HeroesOfDragonland.Multiplayer.RespawnResourcePolicy.Empty) pool.Remove(pool.Maximum);
        }

        // Passive replica hydration never emits Died or executes damage/reward logic.
        // The authoritative instance rejects this path, even if called locally.
        internal void ApplyReplicatedHealth(float current, float maximum)
        {
            if (!UsesNetworkClock || CanMutate) return;
            EnsureInitialized();
            if (health.ApplySnapshot(current, maximum)) HealthChanged?.Invoke(current, maximum);
        }

        internal void ApplyReplicatedResources(float sp, float maxSp, float mp, float maxMp)
        {
            if (!UsesNetworkClock || CanMutate) return;
            EnsureInitialized();
            if (stamina.ApplySnapshot(sp, maxSp)) StaminaChanged?.Invoke(sp, maxSp);
            if (mana.ApplySnapshot(mp, maxMp)) ManaChanged?.Invoke(mp, maxMp);
        }
    }
}
