#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Teams;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Combat
{
    [DefaultExecutionOrder(400)]
    public sealed class BowNetworkProbe : MonoBehaviour
    {
        private MultiplayerBootstrap bootstrap;
        private float baseline;
        private bool installed;
        private bool drawing;
        private bool release;
        private bool projectile;
        private bool passed;
        private bool freeHit;
        private GameObject wall;
        private Vector3 launchLine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrBowCheck")) return;
            var scene = FindAnyObjectByType<MultiplayerBootstrap>();
            if (scene != null) scene.gameObject.AddComponent<BowNetworkProbe>().bootstrap = scene;
        }

        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (passed || runner == null || !runner.IsRunning || match == null || !match.CanPlay) return;

            var humans = FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid && !p.IsBot).ToArray();
            if (humans.Length != 2) return;
            var shooter = humans.Single(p => p.Team == TeamId.TeamB);
            var defender = humans.Single(p => p.Team == TeamId.TeamA);
            var combat = shooter.GetComponent<NetworkPlayerCombat>();
            var view = shooter.GetComponent<BowPresentation>();
            var health = defender.GetComponent<NetworkCharacterStats>();
            var age = match.PlayingSeconds;
            if (!installed)
            {
                baseline = health.Health;
                if (runner.IsServer) Prepare(shooter, defender);
                Check(VfxAudience.Pool(runner) != null, "battlefield impact VFX pool");
                installed = true;
            }

            if (runner.IsServer && wall == null && age > 9 && age < 10)
            {
                wall = new GameObject("M71 server-side obstruction");
                wall.transform.position = Vector3.Lerp(BowShot.Origin(shooter.transform), BowShot.Origin(defender.transform), .5f);
                wall.AddComponent<BoxCollider>().size = new Vector3(1.75f, 2.5f, 1.75f);
            }

            Vector3 direction = Vector3.ProjectOnPlane(defender.transform.position - shooter.transform.position, Vector3.up).normalized;
            bool assisted = age > 5 && age < 8;
            Vector3 aim = assisted ? Quaternion.AngleAxis(4.5f, Vector3.up) * direction : direction;
            bootstrap.TestInput = Vector2.zero;
            bootstrap.TestAttack = shooter.HasInputAuthority && ((age > 2 && age < 3) || (age > 6 && age < 7) || (age > 10 && age < 11));
            bootstrap.TestLockedOn = assisted;
            bootstrap.TestBowTarget = assisted && shooter.HasInputAuthority ? defender.Object.Id : default;
            bootstrap.TestFacing = new Vector2(aim.x, aim.z);

            drawing |= combat.BowDrawing && view.PoseWeight > .2f;
            release |= combat.BowReleaseProgress > 0 && view.PoseWeight > 0;
            projectile |= combat.ArrowInFlight && view.ProjectileVisible;
            if (age > 4 && age < 5) freeHit |= health.Health < baseline;
            if (runner.IsServer && assisted && combat.ArrowInFlight)
                Check(Vector3.Angle(Vector3.ProjectOnPlane(combat.ArrowDirection, Vector3.up), launchLine) < 2,
                    "bounded assistance corrected the launch without homing");
            if (age < 12 && !(!runner.IsServer && view.ImpactsPresented >= 3)) return;
            Check(drawing && release && projectile && freeHit && view.ImpactsPresented == 3,
                $"presentation draw={drawing}, release={release}, projectile={projectile}, freeHit={freeHit}, impacts={view.ImpactsPresented}");
            Check(Mathf.Abs(health.Health - (baseline - 52)) < .05f, "two accepted hits and the wall stopped the third");
            Check(!combat.ArrowInFlight && !view.ProjectileVisible, "flight stopped");
            passed = true;
            Debug.Log("[BowNetwork] PASS equipment requirements, free aim, assisted launch, wall hit, replicated draw/release/arrow and impact; server=" + runner.IsServer);
        }

        private void Prepare(NetworkTestPlayer shooter, NetworkTestPlayer defender)
        {
            foreach (var bot in FindObjectsByType<NetworkTestPlayer>().Where(p => p.IsBot))
                bot.enabled = false;
            var inventory = shooter.GetComponent<Inventory>();
            var item = inventory.Catalog.Items.Single(i => i.name == "HuntingBow");
            Check(shooter.GetComponent<NetworkInventory>().TryAddItem(item.Id), "add bow");
            int slot = Enumerable.Range(0, inventory.SlotCount).First(i => inventory.GetSlot(i).ItemId == item.Id);
            var gear = shooter.GetComponent<Equipment>();
            Check(!gear.TryEquip(slot, EquipmentSlot.MainHand, out _), "DEX gate");
            var progression = shooter.GetComponent<SessionProgression>();
            Check(shooter.GetComponent<NetworkSessionProgression>().TryGrantExperience(100, "m71-bow-check"), "grant first level");
            Check(progression.TryIncreaseAttribute(ItemStat.Dexterity, progression.SessionId, out _), "allocate Dexterity");
            Check(gear.TryEquip(slot, EquipmentSlot.MainHand, out var reason), "equip bow " + reason);

            foreach (var start in new[] { new Vector3(0,0,-18),new Vector3(0,0,18),new Vector3(0,0,-12),new Vector3(-8,0,-18) })
            foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                if (!NavMesh.SamplePosition(start, out var a, .5f, NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(start + direction * 10, out var b, .5f, NavMesh.AllAreas)
                    || Mathf.Abs(a.position.y - b.position.y) > .15f
                    || Physics.Linecast(a.position + Vector3.up * 1.36f, b.position + Vector3.up * 1.36f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                launchLine = Vector3.ProjectOnPlane(b.position - a.position, Vector3.up).normalized;
                shooter.GetComponent<NetworkTransform>().Teleport(a.position + Vector3.up, Quaternion.LookRotation(launchLine));
                defender.GetComponent<NetworkTransform>().Teleport(b.position + Vector3.up, Quaternion.LookRotation(-launchLine));
                return;
            }
            Check(false, "open shot corridor");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BowNetwork] FAIL " + message);
        }

        private void OnDisable()
        {
            if (wall != null) Destroy(wall);
            if (bootstrap == null) return;
            bootstrap.TestAttack = bootstrap.TestLockedOn = null;
            bootstrap.TestFacing = bootstrap.TestInput = null;
            bootstrap.TestBowTarget = default;
        }
    }
}
#endif
