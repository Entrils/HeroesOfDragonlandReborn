using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Authority-side continuity for missing Fusion input ticks. No client timestamps are used.
    internal sealed class ServerInputContinuity
    {
        internal const float HoldSeconds = .05f;
        internal const float ResumeBaselineSeconds = .2f;
        private NetworkPlayerInput last;
        private int missingTicks;

        internal NetworkPlayerInput Resolve(NetworkPlayerInput sample, float tickSeconds, out bool rebaseline)
        {
            int holdTicks = Mathf.FloorToInt((HoldSeconds + .00001f) / tickSeconds);
            int baselineTicks = Mathf.CeilToInt((ResumeBaselineSeconds - .00001f) / tickSeconds);
            rebaseline = false;
            if (sample.HasSample)
            {
                rebaseline = missingTicks >= baselineTicks;
                missingTicks = 0;
                if (rebaseline) sample.Dodge = false;
                if (sample.SuppressAttacks)
                {
                    sample.Move = sample.Facing = Vector2.zero;
                    sample.Block = sample.Dodge = sample.LockedOn = false;
                }
                last = sample;
                return sample;
            }

            if (missingTicks < baselineTicks) missingTicks++;
            var held = last;
            held.HasSample = false;
            held.Attack = held.HeavyAttack = held.Dodge = false;
            // Never reuse a cached projectile aim or assistance target.
            held.BowTarget = default;
            held.BowAim = Vector3.zero;
            if (!last.HasSample || missingTicks > holdTicks)
            {
                held.Move = held.Facing = Vector2.zero;
                held.Block = held.LockedOn = false;
                held.SuppressAttacks = true;
            }
            return held;
        }

        internal void Reset()
        {
            last = default;
            missingTicks = 0;
        }
    }
}
