using System;

namespace HeroesOfDragonland.Presentation.Vfx
{
    // Pure admission/ownership policy: no Unity, network or gameplay dependencies.
    public struct VfxTicket
    {
        public int Index { get; private set; }
        public int Generation { get; private set; }
        internal VfxTicket(int index, int generation) : this() { Index = index; Generation = generation; }
    }
    public sealed class VfxBudget
    {
        private struct Entry { internal bool Active; internal int Kind, Priority, Generation; internal long Order; }
        private readonly Entry[] entries;
        private long order;
        public int ActiveCount { get; private set; }
        public VfxBudget(int capacity)
        {
            if (capacity < 1 || capacity > 256) throw new ArgumentOutOfRangeException("capacity");
            entries = new Entry[capacity];
        }
        public bool IsLive(VfxTicket ticket)
        {
            return ticket.Generation > 0 && ticket.Index >= 0 && ticket.Index < entries.Length
                && entries[ticket.Index].Active && entries[ticket.Index].Generation == ticket.Generation;
        }
        public bool Release(VfxTicket ticket)
        {
            if (!IsLive(ticket)) return false;
            entries[ticket.Index].Active = false;
            ActiveCount--;
            return true;
        }
        public bool TryAcquire(int kind, int priority, int maximumForKind, out VfxTicket ticket, out VfxTicket evicted)
        {
            ticket = new VfxTicket(); evicted = new VfxTicket();
            if (kind < 0 || priority < 0 || priority > 3 || maximumForKind < 1) return false;
            int count = 0, free = -1, candidate = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!entry.Active) { if (free < 0) free = i; continue; }
                if (entry.Kind == kind) count++;
                if (entry.Priority >= priority) continue;
                if (candidate < 0 || entry.Priority < entries[candidate].Priority
                    || (entry.Priority == entries[candidate].Priority && entry.Order < entries[candidate].Order)) candidate = i;
            }
            if (count >= maximumForKind) return false;
            if (free < 0)
            {
                if (candidate < 0) return false;
                free = candidate;
                evicted = new VfxTicket(free, entries[free].Generation);
                Release(evicted);
            }
            int generation = entries[free].Generation == int.MaxValue ? 1 : entries[free].Generation + 1;
            entries[free] = new Entry { Active = true, Kind = kind, Priority = priority, Generation = generation, Order = ++order };
            ActiveCount++;
            ticket = new VfxTicket(free, generation);
            return true;
        }
    }
    public sealed class VfxSequence
    {
        private bool seeded;
        private long previous;
        public void Reset() { seeded = false; previous = 0; }
        public bool Observe(long value)
        {
            if (value < 0) return false;
            if (!seeded) { seeded = true; previous = value; return false; }
            if (value <= previous) return false;
            previous = value;
            return true;
        }
    }
}
