using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    // Carries accepted contact metadata only. Replicas consume it after simulation.
    [DisallowMultipleComponent, RequireComponent(typeof(DamageReceiver))]
    public sealed class NetworkImpactVfx : NetworkBehaviour, IAfterTick
    {
        private DamageReceiver receiver;
        private CombatDefense defense;
        private int sequence;
        private int guardBreak;
        private int received;
        private readonly Queue<Contact> pending = new();

        private readonly struct Contact
        {
            public readonly DamageImpactCue Cue;
            public readonly DamageImpact Impact;
            public readonly NetworkId Source;
            public readonly TeamId Team;
            public readonly bool FriendlyFire;
            public readonly int Tick;
            public readonly double Expires;
            public Contact(DamageImpactCue cue, DamageImpact impact, NetworkId source, TeamId team, bool friendlyFire, int tick)
            { Tick = tick; Expires = Time.unscaledTimeAsDouble + .12; Cue = cue; Impact = impact; Source = source; Team = team; FriendlyFire = friendlyFire; }
        }

        private void Awake()
        {
            receiver = GetComponent<DamageReceiver>();
            defense = GetComponent<CombatDefense>();
        }

        public override void Spawned()
        {
            sequence = received = 0;
            pending.Clear();
            if (!Runner.IsServer || !HasStateAuthority) return;
            receiver.ContactResolved += Impact;
            guardBreak = defense != null ? defense.GuardBreakSequence : 0;
        }

        private void Impact(DamageImpactCue cue, GameObject source, DamageImpact impact)
        {
            if (Object == null || !Object.IsValid || !Runner.IsServer || !HasStateAuthority || !Runner.IsForward) return;
            if (cue == DamageImpactCue.GuardBreak) guardBreak = defense.GuardBreakSequence;
            var origin = source != null ? source.GetComponent<NetworkObject>() : null;
            RPC_Impact(++sequence, Runner.Tick.Raw, cue, impact.Weapon, impact.Point, impact.Direction, impact.Heavy,
                origin != null && origin.IsValid ? origin.Id : default, VfxAudience.Team(source),
                TeamIdentity.Find(source)?.AllowFriendlyFire == true);
        }

        public void AfterTick()
        {
            if (defense == null || Object == null || !Object.IsValid || !Runner.IsServer || !HasStateAuthority || !Runner.IsForward) return;
            if (defense.GuardBreakSequence > guardBreak)
                Impact(DamageImpactCue.GuardBreak, null, new DamageImpact(ImpactWeapon.Unarmed,
                    transform.position + Vector3.up * .8f, Vector3.zero));
            guardBreak = defense.GuardBreakSequence;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Impact(int number, int tick, DamageImpactCue cue, ImpactWeapon weapon, Vector3 point, Vector3 direction,
            bool heavy, NetworkId source, TeamId team, bool friendlyFire)
        {
            if (number <= received) return;
            received = number;
            if (pending.Count >= 16) pending.Dequeue();
            pending.Enqueue(new Contact(cue, new DamageImpact(weapon, point, direction, heavy), source, team, friendlyFire, tick));
        }

        public override void Render()
        {
            while (pending.TryPeek(out var contact))
            {
                NetworkObject source = null;
                if (contact.Source != default) Runner.TryFindObject(contact.Source, out source);
                bool remote = source != null ? !source.HasStateAuthority : !HasStateAuthority;
                double renderTime = remote ? Runner.RemoteRenderTime : Runner.LocalRenderTime;
                if (renderTime < contact.Tick * (double)Runner.DeltaTime && Time.unscaledTimeAsDouble < contact.Expires) break;
                pending.Dequeue();
                CombatFeedback.Present(Runner, gameObject, source != null ? source.gameObject : null,
                    contact.Cue, contact.Impact, contact.Team, contact.FriendlyFire);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (receiver != null) receiver.ContactResolved -= Impact;
            pending.Clear();
        }
    }
}
