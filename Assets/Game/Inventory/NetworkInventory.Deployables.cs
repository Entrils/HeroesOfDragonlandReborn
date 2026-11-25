using Fusion;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public sealed partial class NetworkInventory
    {
        public event System.Action<bool, string> DeployableResolved;
        private bool placingDeployable;

        private bool RequestPlaceSelectedDeployable(int index, ItemDefinition item)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            float reach = item.Deployable.PlacementRange + Vector3.Distance(ray.origin, transform.position);
            RaycastHit nearest = default;
            foreach (var hit in Physics.RaycastAll(ray, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && (nearest.collider == null || hit.distance < nearest.distance))
                    nearest = hit;
            if (nearest.collider != null) return RequestPlaceDeployable(index, nearest.point);
            DeployableResolved?.Invoke(false, "Aim at nearby ground to place the trap.");
            return false;
        }

        public bool RequestPlaceDeployable(int index, Vector3 point)
        {
            if (!CanRequest || index < 0 || index >= inventory.SlotCount
                || !inventory.TryResolveItem(inventory.GetSlot(index).ItemId, out var item)
                || item.Category != ItemCategory.Deployable) return false;
            RPC_PlaceDeployable(index, HasStateAuthority ? SnapshotRevision : inventory.Revision, point);
            return true;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_PlaceDeployable(int index, long revision, Vector3 point, RpcInfo info = default)
        {
            if (!AdmitRequest(info.Source)) return;
            if (!ValidInventoryIndex(index))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.InventorySlot);
                RPC_DeployableResult(info.Source, false, "Invalid trap inventory slot.");
                return;
            }
            if (!float.IsFinite(point.sqrMagnitude))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.TrapPoint);
                RPC_DeployableResult(info.Source, false, "Invalid trap placement point.");
                return;
            }
            bool placed = TryPlaceDeployable(index, revision, point, out var reason);
            RPC_DeployableResult(info.Source, placed, placed ? "Trap placed." : reason);
        }

        internal bool TryPlaceDeployable(int index, long revision, Vector3 point, out string reason)
        {
            reason = "Trap placement is unavailable.";
            if (!CanMutateInventory || !stats.CanMutate || placingDeployable || revision != SnapshotRevision
                || index < 0 || index >= inventory.SlotCount
                || !inventory.TryResolveItem(inventory.GetSlot(index).ItemId, out var item)
                || item.Category != ItemCategory.Deployable || item.Deployable is not { IsValid: true } definition
                || GetComponent<NetworkTestPlayer>() is not { } player || player.Team == TeamId.Unassigned) return false;

            Physics.SyncTransforms();
            if (!DeployablePlacement.TryFind(Runner, Object, definition, point, out var location, out reason)) return false;
            placingDeployable = true;
            NetworkDeployable trap = null;
            bool committed = false;
            try
            {
                trap = Runner.Spawn(definition.Prefab, location, Quaternion.identity, PlayerRef.None,
                    (_, instance) => instance.GetComponent<NetworkDeployable>().Initialize(Object.Id, player.Team, location));
                if (trap != null) trap.Arm();
                if (trap == null || !inventory.TryConsumeAt(index, () => committed = true))
                { reason = "Trap could not be placed."; return false; }
                return true;
            }
            catch (System.Exception error)
            {
                Debug.LogException(error, this);
                if (committed) return true; // A post-commit inventory observer failed.
                reason = "Trap could not be placed.";
                return false;
            }
            finally
            {
                if (!committed && trap != null && trap.Object != null && trap.Object.IsValid) Runner.Despawn(trap.Object);
                placingDeployable = false;
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_DeployableResult([RpcTarget] PlayerRef recipient, bool placed, string reason)
            => DeployableResolved?.Invoke(placed, reason);
    }
}
