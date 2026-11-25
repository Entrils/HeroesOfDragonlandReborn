using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    // Scene/bootstrap scoped presentation service. No static global pool or simulation authority.
    [DisallowMultipleComponent]
    public sealed class VfxPool : MonoBehaviour
    {
        [SerializeField] private VfxCatalog catalog;
        [SerializeField, Range(16, 32)] private int capacity = 32;
        [SerializeField] private bool renderInBatchMode;
        private VfxBudget budget;
        private PooledVfx[] active;
        private readonly Dictionary<VfxKind, Stack<PooledVfx>> idle = new();
        private readonly Dictionary<VfxKind, int> created = new();
        public int ActiveCount => budget != null ? budget.ActiveCount : 0;
        public int Dropped { get; private set; }
        public VfxCatalog Catalog => catalog;
        private void Awake()
        {
            budget = new VfxBudget(Mathf.Clamp(capacity, 16, 32)); active = new PooledVfx[Mathf.Clamp(capacity, 16, 32)];
            if (catalog == null || !catalog.TryValidate(out _)) { Debug.LogError("Assign a complete VFX catalog before enabling the pool.", this); enabled = false; }
        }
        internal bool IsLive(VfxTicket value) => budget != null && budget.IsLive(value);
        internal void Release(VfxTicket value)
        {
            if (!budget.Release(value)) return;
            var effect = active[value.Index]; active[value.Index] = null;
            if (effect == null) return;
            var kind = effect.Kind;
            effect.End();
            if (!idle.TryGetValue(kind, out var stack)) idle[kind] = stack = new Stack<PooledVfx>();
            stack.Push(effect);
        }
        public VfxLease Play(VfxKind kind, Vector3 position, VfxRelation relation = VfxRelation.Neutral,
            Vector3 direction = default, Transform follow = null, float lifetime = 0, float scale = 1)
        {
            if (!isActiveAndEnabled || catalog == null || budget == null || (Application.isBatchMode && !renderInBatchMode)) return default;
            var entry = catalog.Find(kind);
            if (entry == null || entry.Prefab == null) return default;
            if (!budget.TryAcquire((int)kind, entry.Priority, entry.MaximumConcurrent, out var ticket, out var evicted)) { Dropped++; return default; }
            // The policy has already retired this ticket; release only the old Unity instance.
            if (evicted.Generation > 0)
            {
                var previous = active[evicted.Index];
                if (previous != null)
                {
                    var previousKind = previous.Kind;
                    previous.End();
                    if (!idle.TryGetValue(previousKind, out var previousStack)) idle[previousKind] = previousStack = new Stack<PooledVfx>();
                    previousStack.Push(previous);
                }
                active[evicted.Index] = null;
            }
            if (!idle.TryGetValue(kind, out var stack)) idle[kind] = stack = new Stack<PooledVfx>();
            PooledVfx instance = null;
            while (stack.Count > 0 && instance == null)
            {
                instance = stack.Pop();
                if (instance == null && created.TryGetValue(kind, out int lost)) created[kind] = Mathf.Max(0, lost - 1);
            }
            if (instance == null)
            {
                created.TryGetValue(kind, out int count);
                // Per-kind creation is bounded as well as simultaneous leases.
                if (count >= entry.MaximumConcurrent) { budget.Release(ticket); Dropped++; return default; }
                instance = Instantiate(entry.Prefab, transform, false);
                created[kind] = count + 1;
            }
            active[ticket.Index] = instance;
            instance.gameObject.SetActive(false);
            instance.transform.SetPositionAndRotation(follow != null ? follow.position : position,
                direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity);
            instance.transform.localScale = Vector3.one * Mathf.Clamp(scale, .5f, 1.6f);
            instance.gameObject.SetActive(true);
            instance.Begin(this, ticket, kind, relation, Mathf.Clamp(lifetime > 0 ? lifetime : entry.Duration, .05f, 30), follow);
            return new VfxLease(instance, ticket);
        }
        private void OnDisable()
        {
            if (active == null) return;
            for (int i = 0; i < active.Length; i++)
            {
                var effect = active[i];
                if (effect == null) continue;
                // Instances are retained inactive, but every outstanding lease is invalidated.
                effect.StopLiveForPoolReset();
            }
        }
    }
}
