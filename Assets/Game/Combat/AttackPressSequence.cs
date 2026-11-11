namespace HeroesOfDragonland.Combat
{
    // Monotonic counters retain a short tap across network input ticks. Counter jumps represent at most one intention.
    public sealed class AttackPressSequence
    {
        private uint light, heavy;
        private bool initialized;
        public bool TryRead(uint nextLight, uint nextHeavy, out AttackInputKind input)
        {
            input = AttackInputKind.Light;
            if (!initialized) { initialized = true; light = nextLight; heavy = nextHeavy; return false; }
            bool newLight = unchecked((int)(nextLight - light)) > 0;
            bool newHeavy = unchecked((int)(nextHeavy - heavy)) > 0;
            if (newLight) light = nextLight;
            if (newHeavy) heavy = nextHeavy;
            input = newHeavy ? AttackInputKind.Heavy : AttackInputKind.Light;
            return newLight || newHeavy;
        }
    }
}
