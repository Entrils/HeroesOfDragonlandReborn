using System;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterStats))]
    public sealed class CharacterPoise : MonoBehaviour
    {
        private CharacterStats stats;
        private float current, delay;
        private bool initialized;
        public float Maximum => Stats.MaxPoise;
        public float Current { get { EnsureInitialized(); return Mathf.Clamp(current, 0, Maximum); } }
        public float RegenDelayRemaining => delay;
        public float RegenDelay => Stats.PoiseRegenDelay;
        public float RegenPerSecond => Stats.PoiseRegenPerSecond;
        public event Action<float, float> Changed;
        private CharacterStats Stats => stats != null ? stats : stats = GetComponent<CharacterStats>();
        private void EnsureInitialized() { if (!initialized) { initialized = true; current = Maximum; } }
        internal bool ApplyDamage(float amount)
        {
            if (!isActiveAndEnabled || !Stats.CanMutate || Stats.IsDead || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            EnsureInitialized(); current = Mathf.Max(0, Current - amount); delay = RegenDelay;
            Changed?.Invoke(current, Maximum); return current <= 0;
        }
        internal void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled || !Stats.CanMutate || Stats.IsDead) return;
            EnsureInitialized();
            float regenTime = Mathf.Max(0, deltaTime - delay); delay = Mathf.Max(0, delay - deltaTime);
            float previous = current; current = Mathf.Min(Maximum, current + RegenPerSecond * regenTime);
            if (previous != current) Changed?.Invoke(current, Maximum);
        }
        internal void ResetForSpawn()
        {
            if (!Stats.CanMutate) return;
            initialized = true; current = Maximum; delay = 0; Changed?.Invoke(current, Maximum);
        }
    }
}
