using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Skills;
using UnityEngine;

namespace HeroesOfDragonland.Abilities
{
    [DisallowMultipleComponent, RequireComponent(typeof(AbilityCaster), typeof(NetworkSessionSkills))]
    public sealed class NetworkAbilities : NetworkBehaviour
    {
        [Networked] public float CastRemaining { get; private set; }
        [Networked] public float ShieldRemaining { get; private set; }
        [Networked] public float ShieldCapacity { get; private set; }
        private AbilityCaster caster;
        private SessionSkills skills;
        private void Awake() { caster = GetComponent<AbilityCaster>(); skills = GetComponent<SessionSkills>(); }
        public override void Spawned() { if (Runner.IsServer && HasStateAuthority) caster.Presentation += Broadcast; }
        public override void FixedUpdateNetwork()
        {
            if (!Runner.IsServer || !HasStateAuthority) return;
            if (skills.HasAuthority) caster.Tick(Runner.DeltaTime); else caster.Clear();
            CastRemaining = caster.CastRemaining; ShieldRemaining = caster.ShieldRemaining; ShieldCapacity = caster.ShieldCapacity;
        }
        public bool AllowsProjectileDamage(NetworkRunner runner) => Runner == runner && Runner.IsServer && HasStateAuthority
            && NetworkParticipant.IsAdmitted(Object) && NetworkMatch.AllowsGameplay(Runner) && caster.HasAuthority && caster.IsResolvingProjectile;
        private void Broadcast(AbilityDefinition definition, AbilityCue cue, Vector3 position, Vector3 direction)
        {
            for (int i = 0; i < skills.NodeCount; i++)
                if (skills.GetDefinition(i)?.Ability == definition) { RPC_Cue(i, cue, position, direction); return; }
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Cue(int index, AbilityCue cue, Vector3 position, Vector3 direction)
        {
            // Server already delivered its local event. Replicas receive presentation only.
            if (!HasStateAuthority && skills.GetDefinition(index)?.Ability is { } definition) caster.Cue(definition, cue, position, direction);
        }
        public override void Render() { if (!HasStateAuthority) caster.ApplySnapshot(CastRemaining, ShieldRemaining, ShieldCapacity); }
        public override void Despawned(NetworkRunner runner, bool hasState) { caster.Presentation -= Broadcast; caster.Clear(); }
    }
}
