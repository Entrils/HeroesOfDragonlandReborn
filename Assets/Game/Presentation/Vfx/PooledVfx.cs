using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    public readonly struct VfxLease
    {
        private readonly PooledVfx instance;
        private readonly VfxTicket ticket;
        internal VfxLease(PooledVfx instance, VfxTicket ticket) { this.instance = instance; this.ticket = ticket; }
        public bool IsAlive => instance != null && instance.IsLive(ticket);
        public void Stop() { if (IsAlive) instance.Stop(ticket); }
        public void Move(Vector3 position) { if (IsAlive) instance.transform.position = position; }
        public void Radius(float radius) { if (IsAlive && radius > 0 && !float.IsNaN(radius) && !float.IsInfinity(radius)) instance.ApplyRadius(radius); }
    }
    [DisallowMultipleComponent]
    public sealed class PooledVfx : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private bool faceCamera = true;
        [SerializeField, Min(.01f)] private float referenceRadius = .15f;
        [SerializeField] private LineRenderer[] friendly = System.Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] hostile = System.Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] neutral = System.Array.Empty<LineRenderer>();
        private VfxPool owner;
        private VfxTicket ticket;
        private Transform follow;
        private bool following;
        private float remaining;
        internal VfxKind Kind { get; private set; }
        internal bool IsLive(VfxTicket value) => owner != null && owner.IsLive(value) && ticket.Generation == value.Generation && ticket.Index == value.Index;
        internal void Begin(VfxPool pool, VfxTicket value, VfxKind kind, VfxRelation relation, float lifetime, Transform target)
        {
            owner = pool; ticket = value; Kind = kind; remaining = lifetime; follow = target; following = target != null;
            if (faceCamera && Camera.main != null) transform.rotation = Camera.main.transform.rotation;
            foreach (var line in friendly) if (line != null) line.enabled = relation == VfxRelation.Friendly;
            foreach (var line in hostile) if (line != null) line.enabled = relation == VfxRelation.Hostile;
            foreach (var line in neutral) if (line != null) line.enabled = relation == VfxRelation.Neutral;
            if (particles != null) { particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particles.Play(true); }
        }
        internal void End()
        {
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            owner = null; follow = null; following = false;
            gameObject.SetActive(false);
        }
        internal void Stop(VfxTicket value) { if (IsLive(value)) owner.Release(value); }
        internal void ApplyRadius(float radius) => transform.localScale = Vector3.one * (radius / Mathf.Max(.01f, referenceRadius));
        internal void StopLiveForPoolReset() { if (owner != null) owner.Release(ticket); }
        private void OnDisable() { if (owner != null) owner.Release(ticket); }
        private void Update()
        {
            if (owner == null) return;
            if (following && (follow == null || !follow.gameObject.activeInHierarchy)) { owner.Release(ticket); return; }
            if (following) transform.position = follow.position;
            if (faceCamera && Camera.main != null) transform.rotation = Camera.main.transform.rotation;
            remaining -= Time.deltaTime;
            if (remaining <= 0) owner.Release(ticket);
        }
    }
}
