using System;

namespace HeroesOfDragonland.Audio
{
    // Engine-independent policy: higher priorities win; equal priorities never steal.
    public static class AudioMath
    {
        public static float Sanitize(float linear)
        { return float.IsNaN(linear) || float.IsInfinity(linear) ? 0 : Math.Max(0, Math.Min(1, linear)); }
        public static float Decibels(float linear)
        { return Math.Max(-80, 20 * (float)Math.Log10(Math.Max(.0001f, Sanitize(linear)))); }
    }

    public sealed class AudioVoiceBudget
    {
        private struct Slot { internal bool Active; internal int Cue, Priority, Generation; }
        private struct Recent { internal long Owner; internal int Cue; internal double Until; }
        private readonly Slot[] slots;
        private readonly Recent[] recent = new Recent[128];
        private int cursor;
        public AudioVoiceBudget(int capacity) { slots = new Slot[Math.Max(1, capacity)]; }
        public int Capacity { get { return slots.Length; } }
        public int ActiveCount { get { int count = 0; foreach (var slot in slots) if (slot.Active) count++; return count; } }
        public bool IsAlive(int slot, int generation)
        { return slot >= 0 && slot < slots.Length && slots[slot].Active && slots[slot].Generation == generation; }
        public void Release(int slot, int generation) { if (IsAlive(slot, generation)) slots[slot].Active = false; }
        public int Acquire(int cue, long owner, int priority, int maximum, double now, double cooldown, out int generation)
        {
            generation = 0;
            foreach (var entry in recent) if (entry.Owner == owner && entry.Cue == cue && entry.Until > now) return -1;
            int count = 0, free = -1, weakest = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Active) { if (free < 0) free = i; continue; }
                if (slots[i].Cue == cue) count++;
                if (weakest < 0 || slots[i].Priority < slots[weakest].Priority) weakest = i;
            }
            if (count >= Math.Max(1, maximum)) return -1;
            int chosen = free >= 0 ? free : weakest;
            if (chosen < 0 || (free < 0 && slots[chosen].Priority >= priority)) return -1;
            generation = unchecked(slots[chosen].Generation + 1);
            slots[chosen] = new Slot { Active = true, Cue = cue, Priority = priority, Generation = generation };
            recent[cursor++ % recent.Length] = new Recent { Owner = owner, Cue = cue, Until = now + Math.Max(0, Math.Min(10, cooldown)) };
            if (cursor == int.MaxValue) cursor = 0;
            return chosen;
        }
    }
}
