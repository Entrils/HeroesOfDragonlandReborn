using System;
using UnityEngine;

namespace HeroesOfDragonland.Stats
{
    // Private runtime state shared by all three resources; never stored in an asset.
    internal sealed class ResourcePool
    {
        public float Current { get; private set; }
        public float Maximum { get; }
        private readonly float regenerationRate;
        private readonly float regenerationDelay;
        private float delayRemaining;

        public ResourcePool(float maximum, float regenerationRate = 0f, float regenerationDelay = 0f)
        {
            if (!ValidAmount(maximum) || !ValidAmount(regenerationRate) || !ValidAmount(regenerationDelay))
                throw new ArgumentOutOfRangeException(nameof(maximum), "Resource settings must be finite and nonnegative.");
            Maximum = maximum;
            Current = maximum;
            this.regenerationRate = regenerationRate;
            this.regenerationDelay = regenerationDelay;
        }

        public static bool ValidAmount(float amount) => amount >= 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);

        public float Remove(float amount)
        {
            if (!ValidAmount(amount)) return 0f;
            float removed = Mathf.Min(Current, amount);
            Current -= removed;
            if (removed > 0f) delayRemaining = regenerationDelay;
            return removed;
        }

        public bool TrySpend(float amount)
        {
            if (!ValidAmount(amount) || amount > Current) return false;
            Remove(amount);
            return true;
        }

        public float Restore(float amount)
        {
            if (!ValidAmount(amount)) return 0f;
            float restored = Mathf.Min(Maximum - Current, amount);
            Current += restored;
            return restored;
        }

        public void Tick(float deltaTime)
        {
            if (!ValidAmount(deltaTime) || deltaTime == 0f) return;
            // Only the part of this frame after the delay expires regenerates.
            float regenerationTime = Mathf.Max(0f, deltaTime - delayRemaining);
            delayRemaining = Mathf.Max(0f, delayRemaining - deltaTime);
            Current = Mathf.Min(Maximum, Current + regenerationRate * regenerationTime);
        }

        public void ResetToFull()
        {
            Current = Maximum;
            delayRemaining = 0f;
        }
    }
}
