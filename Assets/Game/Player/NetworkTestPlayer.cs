using Fusion;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Planar server arena motor; action components own costs and action state.
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public sealed class NetworkTestPlayer : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float movementSpeed = 5f;
        [SerializeField, Min(1f)] private float arenaHalfExtent = 18f;
        [Networked] public TeamId Team { get; private set; }
        [Networked] public int ParticipantNumber { get; private set; }
        [Networked] public int SpawnLane { get; private set; }
        private NetworkCharacterStats networkStats;
        private NetworkPlayerCombat combat;
        private NetworkPlayerDodge dodge;
        private void Awake()
        {
            networkStats = GetComponent<NetworkCharacterStats>();
            combat = GetComponent<NetworkPlayerCombat>();
            dodge = GetComponent<NetworkPlayerDodge>();
        }

        internal void Initialize(TeamId team, int number, int lane)
        {
            if (!Runner.IsServer) return;
            Team = team;
            ParticipantNumber = number;
            SpawnLane = lane;
        }

        public override void Spawned()
        {
            Object.ForceRemoteRenderTimeframe = !HasStateAuthority;
            name = "Player " + ParticipantNumber + " (" + Team + ")";
            Debug.Log("[Multiplayer] spawned participant=" + ParticipantNumber
                + " object=" + Object.Id + " inputOwner=" + Object.InputAuthority
                + " team=" + Team + " local=" + HasInputAuthority, this);
        }

        public override void FixedUpdateNetwork()
        {
            // Foundation only: server motion, interpolated clients. Prediction is a later milestone.
            if (!Runner.IsServer || !HasStateAuthority || combat == null || !combat.IsAdmitted()) return;
            // Missing input stops locomotion/new requests, but existing actions keep ticking.
            GetInput(out NetworkPlayerInput input);
            Vector2 move = Sanitize(input.Move);
            input.Facing = Sanitize(input.Facing);
            Vector3 direction = new Vector3(move.x, 0, move.y);
            Vector3 facing = input.Facing.sqrMagnitude > 0.0001f
                ? new Vector3(input.Facing.x, 0, input.Facing.y).normalized
                : direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            // Dodge wins simultaneous idle requests; neither action interrupts the other.
            if (dodge != null) dodge.TickIntent(input, direction, facing);
            if (!NetworkMatch.AllowsGameplay(Runner)) return;
            if (networkStats != null && networkStats.Dead) return;
            combat.TickIntent(input, facing, dodge != null && dodge.Busy);
            Vector3 offset = dodge != null && dodge.Busy ? dodge.StepDisplacement()
                : direction * (movementSpeed * combat.MovementMultiplier * Runner.DeltaTime);
            Vector3 position = transform.position + Sweep(offset);
            position.x = Mathf.Clamp(position.x, -arenaHalfExtent, arenaHalfExtent);
            position.z = Mathf.Clamp(position.z, -arenaHalfExtent, arenaHalfExtent);
            var spawnService = Runner.GetComponentInParent<TeamSpawnService>();
            if (spawnService != null) position = spawnService.ConstrainPlayerMovement(Team, transform.position, position);
            transform.position = position;
            facing = dodge != null && dodge.Busy ? dodge.Facing : combat.IsBusy ? combat.AttackFacing : facing;
            transform.rotation = Quaternion.LookRotation(facing);
        }

        private static Vector2 Sanitize(Vector2 value)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)) return Vector2.zero;
            return Vector2.ClampMagnitude(value, 1f);
        }

        // Matches the test capsule. Sweep prevents a roll tunnelling through a wall
        // or another avatar; no client transform or collision report is accepted.
        private Vector3 Sweep(Vector3 offset)
        {
            float distance = offset.magnitude;
            if (distance <= 0f) return Vector3.zero;
            Physics.SyncTransforms();
            float allowed = distance;
            foreach (var hit in Physics.CapsuleCastAll(transform.position + Vector3.up * 0.5f,
                transform.position - Vector3.up * 0.5f, 0.48f, offset / distance,
                distance + 0.02f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - 0.02f));
            }
            return offset / distance * allowed;
        }
    }
}
