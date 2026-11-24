#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    [DefaultExecutionOrder(410)]
    public sealed class DeployableNetworkProbe : MonoBehaviour
    {
        private static readonly string[] Names = { "BearTrap", "MagicMine", "SlowTrap" };
        private MultiplayerBootstrap bootstrap;
        private int sent;
        private bool initialized, passed, rejected, alliedSafe;
        private readonly bool[] visible = new bool[3];
        private NetworkObject victim, bystander, ally;
        private float startHealth, extraHealth, allyHealth;
        private bool rootSeen, slowSeen, areaSeen;
        private bool spacingRejected;
        private NetworkId watchedOwner;
        private Vector3 position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrTrapsCheck")) return;
            var manager = FindAnyObjectByType<MultiplayerBootstrap>();
            if (manager != null) manager.gameObject.AddComponent<DeployableNetworkProbe>().bootstrap = manager;
        }

        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (passed || runner == null || !runner.IsRunning || match == null || !match.CanPlay) return;
            var humans = FindObjectsByType<NetworkTestPlayer>()
                .Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 4) return;
            var owner = humans.Where(p => p.Team == TeamId.TeamB).OrderBy(p => p.ParticipantNumber).First();
            victim = humans.Where(p => p.Team == TeamId.TeamA).OrderBy(p => p.ParticipantNumber).First().Object;
            var health = victim.GetComponent<NetworkCharacterStats>();
            var inventory = owner.GetComponent<Inventory>();
            var network = owner.GetComponent<NetworkInventory>();
            float age = match.PlayingSeconds;
            if (!initialized)
            {
                startHealth = health.Health;
                if (runner.IsServer) Arrange(owner, network, inventory, humans);
                watchedOwner = owner.Object.Id;
                if (owner.HasInputAuthority) network.DeployableResolved += OnResult;
                initialized = true;
            }
            if (owner.HasInputAuthority)
            {
                // The owner uses real inventory RPCs, including the rejected-location case.
                var location = owner.transform.position - Vector3.up + owner.transform.forward * 2.8f;
                if (sent == 0 && age > .9f) Send(network, inventory, 0, location + Vector3.up * 30);
                if (sent == 1 && age > 1.6f) Send(network, inventory, 0, location);
                if (sent == 2 && age > 4.4f) Send(network, inventory, 1, location);
                if (sent == 3 && age > 7.2f) Send(network, inventory, 2, location);
                if (sent == 4 && age > 12f) Send(network, inventory, 0, location);
            }

            foreach (var trap in FindObjectsByType<NetworkDeployable>())
            {
                if (trap.Object == null || !trap.Object.IsValid || trap.OwnerId != watchedOwner) continue;
                int type = Array.IndexOf(Names, trap.Definition.name);
                Check(type >= 0 && trap.Team == TeamId.TeamB, "replicated owner and team");
                if (!visible[type]) Debug.Log("[TrapsNetwork] Saw " + trap.Definition.name + " at " + trap.WorldPosition);
                visible[type] = true;
                if (!runner.IsServer) continue;
                if (!spacingRejected && type == 0 && age < 2.5f)
                {
                    Check(!DeployablePlacement.TryFind(runner, owner.Object, trap.Definition, trap.WorldPosition,
                        out _, out var reason) && reason.Contains("farther"), "duplicate placement rejected by spacing");
                    spacingRejected = true;
                }
                if (age > 2.5f && age < 2.9f && type == 0)
                {
                    // An ally can stand on the trap after arming without receiving anything.
                    if (ally != null) Teleport(ally, trap.WorldPosition);
                }
                if (age > 2.9f && age < 3.3f && type == 0)
                {
                    alliedSafe = Mathf.Approximately(ally.GetComponent<NetworkCharacterStats>().Health, allyHealth);
                    Teleport(ally, position + Vector3.right * 8);
                }
                bool trigger = (type == 0 && age >= 3.3f && age < 4.4f) ||
                    (type == 1 && age >= 6f && age < 7.2f) || (type == 2 && age >= 8.8f && age < 10.1f);
                if (trigger)
                {
                    Teleport(victim, trap.WorldPosition);
                    if (type == 1)
                    {
                        Vector3 towardOwner = Vector3.ProjectOnPlane(owner.transform.position - trap.WorldPosition, Vector3.up).normalized;
                        Teleport(bystander, trap.WorldPosition + towardOwner * 1.7f);
                    }
                }
            }

            var combat = victim.GetComponent<NetworkPlayerCombat>();
            float movement = combat.DeployableMovementMultiplier;
            rootSeen |= movement < .01f && age < 4.5f;
            slowSeen |= Mathf.Abs(movement - .45f) < .02f && age > 8;
            if (runner.IsServer)
            {
                areaSeen |= Mathf.Abs(extraHealth - bystander.GetComponent<NetworkCharacterStats>().Health - 36) < .1f;
                if (age is > 4.1f and < 4.4f or > 6.9f and < 7.2f or > 10.1f and < 11.5f)
                    Teleport(victim, position + Vector3.forward * 9);
                if (age is > 6.9f and < 7.2f) Teleport(bystander, position - Vector3.forward * 9);
            }
            if (age < 14) return;
            if (runner.IsServer)
            {
                Check(FindObjectsByType<NetworkDeployable>().Any(t => t.OwnerId == watchedOwner), "spare trap before cleanup");
                bootstrap.GetComponent<NetworkRoundReset>().ClearWorld(runner);
            }
            else if (age < 15 && FindObjectsByType<NetworkDeployable>().Any(t => t.OwnerId == watchedOwner))
                return;
            passed = true;
            Check(visible.All(value => value), "observed bear/mine/slow=" + string.Join(",", visible) + " remaining health=" + health.Health);
            Check(rootSeen && slowSeen && Mathf.Approximately(movement, 1), "replicated root, slow and recovery");
            Check(Mathf.Abs(startHealth - health.Health - 66) < .1f, "bear and mine applied exactly once; slow applied no damage");
            Check(!FindObjectsByType<NetworkDeployable>().Any(t => t.Object != null && t.Object.IsValid && t.OwnerId == watchedOwner),
                "round cleanup removed the spare trap");
            if (runner.IsServer) Check(areaSeen && alliedSafe && spacingRejected, "mine burst, friendly-fire protection and spacing");
            if (owner.HasInputAuthority)
                Check(rejected && Names.All(name => inventory.GetAmount(Find(inventory, name).Id) == 0), "rejected request kept its item; successful placements consumed theirs");
            Debug.Log("[TrapsNetwork] PASS placement, team rules, bear root, mine burst, slow and cleanup; server=" + runner.IsServer);
        }

        private void Arrange(NetworkTestPlayer owner, NetworkInventory network, Inventory inventory, NetworkTestPlayer[] humans)
        {
            foreach (var bot in bootstrap.Avatars.Select(o => o.GetComponent<NetworkTestPlayer>()).Where(p => p != null && p.IsBot))
                bot.enabled = false;
            foreach (var enemy in FindObjectsByType<NetworkEnemy>())
                enemy.GetComponent<HeroesOfDragonland.AI.EnemyBrain>().enabled = false;
            bystander = humans.Single(p => p.Object != victim && p.Team == TeamId.TeamA).Object;
            extraHealth = bystander.GetComponent<NetworkCharacterStats>().Health;
            ally = humans.Single(p => p != owner && p.Team == TeamId.TeamB).Object;
            allyHealth = ally.GetComponent<NetworkCharacterStats>().Health;
            foreach (string name in Names) Check(network.TryAddItem(Find(inventory, name).Id, name == Names[0] ? 2 : 1), "grant fixture item");

            foreach (var anchor in new[] { new Vector3(0,0,-18), new Vector3(0,0,18), new Vector3(-8,0,-18), new Vector3(8,0,18) })
            foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                if (!NavMesh.SamplePosition(anchor, out var ground, .5f, NavMesh.AllAreas)) continue;
                position = ground.position;
                owner.GetComponent<NetworkTransform>().Teleport(position + Vector3.up, Quaternion.LookRotation(direction));
                Physics.SyncTransforms();
                if (DeployablePlacement.TryFind(owner.Runner, owner.Object, Find(inventory, Names[0]).Deployable,
                    position + direction * 2.8f, out _, out _)) return;
            }
            Check(false, "fixture found valid trap ground");
        }

        private void OnResult(bool placed, string reason)
        {
            Debug.Log($"[TrapsNetwork] request {sent}: {placed} {reason}");
            if (!placed && sent == 1) rejected = true;
        }
        private void Send(NetworkInventory network, Inventory inventory, int type, Vector3 point)
        {
            var item = Find(inventory, Names[type]);
            for (int i = 0; i < inventory.SlotCount; i++)
                if (inventory.GetSlot(i).ItemId == item.Id)
                {
                    sent++;
                    Debug.Log($"[TrapsNetwork] request {sent} {item.name} at {point} revision {inventory.Revision}");
                    Check(network.RequestPlaceDeployable(i, point), "placement request sent");
                    return;
                }
        }

        private static ItemDefinition Find(Inventory inventory, string name) => inventory.Catalog.Items.Single(i => i.name == name);
        private static void Teleport(NetworkObject actor, Vector3 ground)
            => actor.GetComponent<NetworkTransform>().Teleport(ground + Vector3.up, Quaternion.identity);
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("[TrapsNetwork] FAIL " + label);
        }
    }
}
#endif
