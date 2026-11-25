#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Multiplayer
{
    [DefaultExecutionOrder(400)]
    internal sealed class NetworkLatencyProbe : MonoBehaviour
    {
        private MultiplayerBootstrap bootstrap;
        private int stage = -1;
        private float baseline;
        private float walkDistance;
        private bool dodged, hit, complete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrLatencyCheck")) return;
            var owner = FindAnyObjectByType<MultiplayerBootstrap>();
            if (owner != null) owner.gameObject.AddComponent<NetworkLatencyProbe>().bootstrap = owner;
        }

        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (complete || runner == null || !runner.IsRunning || match == null || !match.CanPlay) return;
            var humans = FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid
                && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 2) return;
            var defender = humans.First(p => p.Team == TeamId.TeamA);
            var striker = humans.First(p => p.Team == TeamId.TeamB);
            var health = defender.GetComponent<NetworkCharacterStats>();
            if (health.MaxHealth <= 0) return;
            float age = match.PlayingSeconds;
            int phase = age < 6 ? 0 : 1;
            if (stage != phase)
            {
                if (stage < 0) baseline = health.MaxHealth;
                stage = phase;
                if (runner.IsServer) Prepare(humans);
            }

            bool ownsStriker = striker.HasInputAuthority;
            bootstrap.TestFacing = ownsStriker ? Vector2.left : Vector2.right;
            bootstrap.TestInput = ownsStriker && age is > 1 and < 2 ? Vector2.right : Vector2.zero;
            bootstrap.TestDodge = ownsStriker && (age is > 2.4f and < 2.7f || age is > 3.6f and < 3.9f);
            bootstrap.TestAttack = ownsStriker && (age is > 7 and < 7.25f || age is > 8.4f and < 8.65f || age is > 9.8f and < 10.05f);
            bootstrap.TestBlock = bootstrap.TestParry = bootstrap.TestHeavyAttack = false;
            if (age is > 1.4f and < 2.35f) walkDistance = Mathf.Max(walkDistance, striker.transform.position.x - .7f);
            dodged |= striker.GetComponent<NetworkPlayerDodge>().DodgeSequence > 0;
            if (age > 6.5f) hit |= health.Health < baseline;

            if (age < 12) return;
            complete = true;
            Check(walkDistance > .35f && dodged && hit, $"movement={walkDistance:F2}m dodge={dodged} melee={hit}");
            if (!runner.IsServer)
            {
                var processStats = defender.GetComponent<CharacterStats>();
                float before = processStats.CurrentHealth;
                Check(processStats.ApplyDamage(50, striker.gameObject, "latency verification") == 0 && processStats.CurrentHealth == before,
                    "client cannot supply damage");
            }
            var remote = runner.ActivePlayers.First(p => p != runner.LocalPlayer);
            var rtt = runner.GetPlayerRtt(runner.IsServer ? remote : PlayerRef.None) * 1000;
            Debug.Log($"[LatencyNetwork] PASS movement={walkDistance:F2}m, dodge, authoritative melee and client mutation check, measured RTT={rtt:F0}ms server={runner.IsServer}");
        }

        private static void Prepare(NetworkTestPlayer[] humans)
        {
            foreach (var enemy in FindObjectsByType<NetworkEnemy>()) enemy.GetComponent<EnemyMeleeAttack>().enabled = false;
            Check(NavMesh.SamplePosition(new Vector3(0, 0, -18), out var floor, 3, NavMesh.AllAreas), "open test floor");
            foreach (var player in humans)
            {
                bool striker = player.Team == TeamId.TeamB;
                var combat = player.GetComponent<NetworkPlayerCombat>();
                combat.CancelForRespawn();
                player.GetComponent<CharacterStats>().ResetToFull();
                float x = striker ? .7f : -.7f;
                player.GetComponent<NetworkTransform>().Teleport(new Vector3(x, floor.position.y + 1.2f, floor.position.z),
                    Quaternion.LookRotation(striker ? Vector3.left : Vector3.right));
                var gear = player.GetComponent<Equipment>();
                if (gear.EquippedWeapon != null) continue;
                var inventory = player.GetComponent<Inventory>();
                var sword = inventory.Catalog.Items.First(i => i.name == "IronSword");
                if (inventory.GetAmount(sword.Id) == 0) Check(player.GetComponent<NetworkInventory>().TryAddItem(sword.Id), "sword inventory");
                int slot = Enumerable.Range(0, inventory.SlotCount).First(i => inventory.GetSlot(i).ItemId == sword.Id);
                Check(gear.TryEquip(slot, EquipmentSlot.MainHand, out _), "sword equip");
            }
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("[LatencyNetwork] FAIL " + message);
        }
    }
}
#endif
