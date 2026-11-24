using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject), typeof(TeamIdentity))]
    public sealed class NetworkDeployable : NetworkBehaviour
    {
        [SerializeField] private DeployableDefinition definition;
        [SerializeField] private Renderer teamMarker;
        [Networked] public NetworkId OwnerId { get; private set; }
        [Networked] public TeamId Team { get; private set; }
        [Networked] public Vector3 WorldPosition { get; private set; }
        [Networked] private TickTimer ArmTimer { get; set; }
        [Networked] private TickTimer Expiry { get; set; }
        private bool active;
        private bool resolving;
        public DeployableDefinition Definition => definition;

        internal void Initialize(NetworkId owner, TeamId team, Vector3 point)
        {
            OwnerId = owner;
            Team = team;
            WorldPosition = point;
        }

        internal void Arm()
        {
            active = true;
            ArmTimer = TickTimer.CreateFromSeconds(Runner, definition.ArmingSeconds);
            Expiry = TickTimer.CreateFromSeconds(Runner, definition.Lifetime);
        }

        public override void Spawned()
        {
            transform.position = WorldPosition;
            GetComponent<TeamIdentity>().TryConfigure(Team, CombatantKind.NPC, definition.FriendlyFire);
            if (teamMarker == null) return;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Team == TeamId.TeamA ? new Color(.12f,.48f,1) : new Color(.9f,.16f,.1f));
            teamMarker.SetPropertyBlock(properties);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !Runner.IsServer || !active || !NetworkMatch.AllowsGameplay(Runner)) return;
            if (Expiry.Expired(Runner)) { Runner.Despawn(Object); return; }
            if (!ArmTimer.ExpiredOrNotRunning(Runner)) return;
            var bootstrap = Runner.GetComponentInParent<MultiplayerBootstrap>();
            if (bootstrap == null) return;
            foreach (var candidate in bootstrap.Avatars)
            {
                if (!Eligible(candidate, definition.TriggerRadius)) continue;
                active = false;
                resolving = true;
                try
                {
                    foreach (var target in bootstrap.Avatars)
                        if (Eligible(target, definition.EffectRadius)) Apply(target);
                    RPC_Triggered(WorldPosition);
                }
                finally { resolving = false; Runner.Despawn(Object); }
                return;
            }
        }

        private bool Eligible(NetworkObject target, float radius)
        {
            if (!NetworkParticipant.IsAdmitted(target) || target.Runner != Runner
                || !target.TryGetComponent<NetworkCharacterStats>(out var stats) || stats.Dead
                || !TeamIdentity.CanDamage(gameObject, target.gameObject)) return false;
            var offset = target.transform.position - WorldPosition;
            if (Mathf.Abs(offset.y) > 2 || offset.x * offset.x + offset.z * offset.z > radius * radius) return false;
            var start = WorldPosition + Vector3.up * .12f;
            return !gameObject.scene.GetPhysicsScene().LinecastToActor(start, target.transform.position, target.transform, ignorePlayers: true);
        }

        private void Apply(NetworkObject target)
        {
            foreach (var immunity in target.GetComponents<IInvulnerabilityProvider>())
                if (immunity.IsInvulnerable) return;
            if (definition.Damage > 0) target.GetComponent<DamageReceiver>()?.TakeDamage(definition.Damage, gameObject, definition.name);
            if (definition.MovementSeconds > 0)
                target.GetComponent<NetworkPlayerCombat>()?.ApplyDeployableMovement(definition.MovementMultiplier, definition.MovementSeconds);
        }

        internal bool AllowsTrapDamage(NetworkRunner runner, GameObject victim) => resolving && Object != null && Object.IsValid
            && Runner == runner && Runner.IsServer && HasStateAuthority && NetworkMatch.AllowsGameplay(Runner)
            && TeamIdentity.CanDamage(gameObject, victim);

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Triggered(Vector3 point)
        {
            VfxAudience.Pool(Runner)?.Play(definition.Damage > 0 ? VfxKind.MeleeHit : VfxKind.MagicShield, point,
                VfxAudience.Relation(Runner, Team, true, definition.FriendlyFire));
        }
    }
}
