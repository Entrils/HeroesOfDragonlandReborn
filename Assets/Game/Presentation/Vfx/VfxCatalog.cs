using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    public enum VfxKind { MeleeHit, BlockedHit, Parry, GuardBreak, HeavyAttack, PlayerDeath,
        FireProjectile, Healing, MagicShield, ManaCast, LevelUp, ItemPickup, ChestOpening, CampCleared, BossDeath, KingDeath, Victory }
    public enum VfxRelation { Neutral, Friendly, Hostile }
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Art/VFX Catalog")]
    public sealed class VfxCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            [SerializeField] private VfxKind kind;
            [SerializeField] private PooledVfx prefab;
            [SerializeField, Range(0, 3)] private int priority;
            [SerializeField, Min(1)] private int maximumConcurrent = 6;
            [SerializeField, Range(.1f, 3)] private float duration = .5f;
            public VfxKind Kind => kind;
            public PooledVfx Prefab => prefab;
            public int Priority => priority;
            public int MaximumConcurrent => maximumConcurrent;
            public float Duration => duration;
        }
        [SerializeField] private Entry[] effects = Array.Empty<Entry>();
        public Entry Find(VfxKind kind)
        {
            Entry found = null;
            foreach (var entry in effects ?? Array.Empty<Entry>())
                if (entry != null && entry.Kind == kind) { if (found != null) return null; found = entry; }
            return found;
        }
        public bool TryValidate(out string reason)
        {
            reason = null;
            var seen = new HashSet<VfxKind>();
            foreach (var entry in effects ?? Array.Empty<Entry>())
            {
                if (entry == null || !Enum.IsDefined(typeof(VfxKind), entry.Kind) || !seen.Add(entry.Kind)
                    || entry.Prefab == null || entry.Priority < 0 || entry.Priority > 3 || entry.MaximumConcurrent < 1 || entry.MaximumConcurrent > 8
                    || float.IsNaN(entry.Duration) || float.IsInfinity(entry.Duration) || entry.Duration < .1f || entry.Duration > 3)
                { reason = "Invalid, missing or duplicate VFX entry."; return false; }
                int systems = 0;
                foreach (var component in entry.Prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component is ParticleSystem system)
                    {
                        systems++;
                        if (system.main.maxParticles > 16 || system.main.playOnAwake || system.main.stopAction != ParticleSystemStopAction.None
                            || system.collision.enabled || system.lights.enabled)
                        { reason = "Pooled particles exceed budget or can change their own lifecycle/physics."; return false; }
                    }
                    else if (!(component is Transform || component is PooledVfx || component is ParticleSystemRenderer
                        || component is LineRenderer || component is MeshFilter || component is MeshRenderer))
                    { reason = "VFX prefab contains a missing, gameplay or non-presentation component."; return false; }
                }
                if (systems > 1 || entry.Prefab.GetComponentsInChildren<PooledVfx>(true).Length != 1)
                { reason = "One bounded particle system/pooled owner is supported per prefab."; return false; }
            }
            if (seen.Count != Enum.GetValues(typeof(VfxKind)).Length) reason = "All 17 initial VFX kinds must be configured.";
            return reason == null;
        }
    }
}
