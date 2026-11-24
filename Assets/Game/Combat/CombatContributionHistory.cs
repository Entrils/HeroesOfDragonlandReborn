using System;
using System.Collections.Generic;

namespace HeroesOfDragonland.Combat
{
    // The server supplies accepted damage and time. Use individual hits so a small
    // recent hit cannot refresh the entire lifetime contribution of an attacker.
    public sealed class CombatContributionHistory<T>
    {
        public readonly struct Contribution
        {
            public readonly T Actor;
            public readonly double Damage;
            internal readonly long Order;
            internal Contribution(T actor, double damage, long order)
            { Actor = actor; Damage = damage; Order = order; }
        }

        private readonly Queue<(T actor, float damage, double time, long order)> hits = new();
        private long order;

        public void Record(T actor, float damage, double time, double window)
        {
            if (!float.IsFinite(damage) || damage <= 0 || !double.IsFinite(time)
                || (hits.Count > 0 && time < lastTime)) return;
            Expire(time, window);
            hits.Enqueue((actor, damage, time, ++order));
            lastTime = time;
        }

        private double lastTime;

        private void Expire(double now, double window)
        {
            while (hits.Count > 0 && hits.Peek().time < now - window) hits.Dequeue();
        }

        public List<Contribution> Recent(double now, double window, float minimumDamage, Predicate<T> eligible)
        {
            Expire(now, window);
            var totals = new Dictionary<T, Contribution>();
            foreach (var hit in hits)
            {
                if (hit.time > now || !eligible(hit.actor)) continue;
                totals.TryGetValue(hit.actor, out var previous);
                totals[hit.actor] = new Contribution(hit.actor, previous.Damage + hit.damage, hit.order);
            }
            var result = new List<Contribution>();
            foreach (var entry in totals.Values)
                if (entry.Damage >= minimumDamage) result.Add(entry);
            result.Sort((a, b) =>
            {
                int damage = b.Damage.CompareTo(a.Damage);
                return damage != 0 ? damage : b.Order.CompareTo(a.Order);
            });
            return result;
        }

        public bool Resolve(T finisher, bool eligibleFinisher, double now, double window, float minimumDamage,
            Predicate<T> eligible, Func<T, T, bool> allied, out T killer, out List<T> assists)
        {
            var recent = Recent(now, window, minimumDamage, eligible);
            assists = new List<T>();
            if (eligibleFinisher && eligible(finisher)) killer = finisher;
            else if (recent.Count > 0) killer = recent[0].Actor;
            else { killer = default; return false; }

            foreach (var contribution in recent)
                if (!EqualityComparer<T>.Default.Equals(contribution.Actor, killer) && allied(contribution.Actor, killer))
                    assists.Add(contribution.Actor);
            return true;
        }

        public void Clear() { hits.Clear(); lastTime = 0; }
    }
}
