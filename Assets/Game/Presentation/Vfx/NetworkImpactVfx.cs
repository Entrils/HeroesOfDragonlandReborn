using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    // A compact confirmed outcome, not a networked particle, client hit report or damage request.
    [DisallowMultipleComponent, RequireComponent(typeof(DamageReceiver))]
    public sealed class NetworkImpactVfx : NetworkBehaviour, IAfterTick
    {
        // Local subscribers reuse this confirmed outcome; this adds no network payload.
        public event System.Action<VfxKind, Vector3> Presented;
        private DamageReceiver receiver;
        private CombatDefense defense;
        private int sequence;
        private int guardBreak;
        private bool playerDefense;
        private readonly VfxSequence received = new();
        private void Awake() { receiver = GetComponent<DamageReceiver>(); defense = GetComponent<CombatDefense>(); playerDefense = GetComponent<NetworkPlayerCombat>() != null; }
        public override void Spawned()
        {
            received.Reset();
            if (!Runner.IsServer || !HasStateAuthority) return;
            receiver.ImpactResolved += Impact;
            // Players already replicate guard-break counters; NPCs do not.
            guardBreak = defense != null ? defense.GuardBreakSequence : 0;
        }
        private void Impact(DamageImpactCue cue, GameObject source)
        {
            if (Object == null || !Object.IsValid || !Runner.IsServer || !HasStateAuthority || !Runner.IsForward) return;
            var kind = cue == DamageImpactCue.Parried ? VfxKind.Parry : cue == DamageImpactCue.Blocked ? VfxKind.BlockedHit : VfxKind.MeleeHit;
            Send(kind, VfxAudience.Team(source), TeamIdentity.Find(source)?.AllowFriendlyFire == true);
        }
        public void AfterTick()
        {
            if (playerDefense || defense == null || Object == null || !Object.IsValid || !Runner.IsServer || !HasStateAuthority || !Runner.IsForward) return;
            if (defense.GuardBreakSequence > guardBreak) Send(VfxKind.GuardBreak, VfxAudience.Team(gameObject), false);
            guardBreak = defense.GuardBreakSequence;
        }
        private void Send(VfxKind kind, TeamId sourceTeam, bool friendlyFire)
        {
            var visual = GetComponentInChildren<CharacterVisual>();
            Vector3 point = visual != null && visual.GetSocket(CharacterSocket.Chest) != null
                ? visual.GetSocket(CharacterSocket.Chest).position : transform.position + Vector3.up * .8f;
            RPC_Impact(++sequence, kind, point, sourceTeam, friendlyFire);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Impact(int number, VfxKind kind, Vector3 point, TeamId sourceTeam, bool friendlyFire)
        {
            // RPCs are transient and ordered: seed at zero so the first live cue plays.
            if (number <= 0) return;
            if (!started) { received.Observe(0); started = true; }
            if (!received.Observe(number)) return;
            if (Presented != null) foreach (System.Action<VfxKind, Vector3> handler in Presented.GetInvocationList())
            {
                try { handler(kind, point); } catch (System.Exception exception) { Debug.LogException(exception, this); }
            }
            VfxAudience.Pool(Runner)?.Play(kind, point, VfxAudience.Relation(Runner, sourceTeam, true, friendlyFire));
        }
        private bool started;
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (receiver != null) receiver.ImpactResolved -= Impact;
            started = false;
        }
    }
}
