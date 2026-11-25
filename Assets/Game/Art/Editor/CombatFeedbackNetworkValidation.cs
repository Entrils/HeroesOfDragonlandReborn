using System;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Art.Editor
{
    public static class CombatFeedbackNetworkValidation
    {
        public static string Status { get; private set; } = "Not run";

        [MenuItem("Heroes of Dragonland/Combat/Validate M84 Local Network Impact")]
        public static async void Run()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
            if (!Application.isPlaying || bootstrap == null || bootstrap.Running || bootstrap.Busy)
            {
                Debug.LogError("[M84] Start Battlefield in Play Mode without an active session.");
                return;
            }

            Status = "Running";
            var bots = bootstrap.GetComponent<DeveloperBotService>();
            int previousBotCount = bots.RequestedBots;
            try
            {
                bots.SetBotCount(5);
                await bootstrap.StartSession(GameMode.Single, "m84-impact-validation");
                var spawner = bootstrap.GetComponent<NetworkFortressSpawner>();
                await Wait(() => spawner.Match != null && spawner.Match.State == MatchPhase.Lobby
                    && spawner.Match.TeamACount + spawner.Match.TeamBCount == 6, "complete lobby roster");
                var match = spawner.Match;
                match.RPC_SetReady(true);
                await Wait(() => match.LocalReady, "local ready acknowledgement");
                await Wait(() => match.CanStartLobby, "host readiness");
                Check(match.TryStartLobby(), "normal lobby start");
                await Wait(() => match.CanPlay, "match countdown");
                var runner = bootstrap.Runner;
                Check(runner.TryGetPlayerObject(runner.LocalPlayer, out var source), "local avatar");
                var player = source.GetComponent<NetworkTestPlayer>();
                var inventory = source.GetComponent<Inventory>();
                var sword = inventory.Catalog.Items.First(item => item.name == "IronSword");
                if (inventory.GetAmount(sword.Id) == 0)
                    Check(source.GetComponent<NetworkInventory>().TryAddItem(sword.Id), "add sword to test inventory");
                int swordSlot = Enumerable.Range(0, inventory.SlotCount).First(i => inventory.GetSlot(i).ItemId == sword.Id);
                Check(source.GetComponent<Equipment>().TryEquip(swordSlot, EquipmentSlot.MainHand, out var reason),
                    "equip test sword " + reason);
                var target = bots.Avatars.First(actor => actor.GetComponent<NetworkTestPlayer>().Team != player.Team);
                foreach (var bot in bots.Avatars) bot.GetComponent<NetworkTestPlayer>().enabled = false;

                Check(NavMesh.SamplePosition(new Vector3(0, 0, -18), out var floor, 3, NavMesh.AllAreas), "open test floor");
                Vector3 position = floor.position + Vector3.up;
                source.GetComponent<NetworkTransform>().Teleport(position, Quaternion.identity);
                target.GetComponent<NetworkTransform>().Teleport(position + Vector3.forward, Quaternion.LookRotation(Vector3.back));
                target.GetComponent<CombatDefense>().SetBlockIntent(false, false);
                ServerValidationChecks.RunLive(source, target);
                Physics.SyncTransforms();

                Vector3 contactPoint = default;
                int contacts = 0;
                target.GetComponent<DamageReceiver>().ContactResolved += (cue, attacker, impact) =>
                {
                    if (cue != DamageImpactCue.MeleeHit || attacker != source.gameObject || impact.Weapon != ImpactWeapon.Sword) return;
                    contacts++;
                    contactPoint = impact.Point;
                };
                var melee = source.GetComponent<MeleeAttack>();
                var pool = bootstrap.GetComponent<VfxPool>();
                await Wait(() => melee.RestartRemaining <= 0 && melee.TryAttack(Vector3.forward), "accepted melee swing");
                await Wait(() => contacts > 0, "authoritative melee contact");
                await Wait(() => pool.GetComponentsInChildren<PooledVfx>()
                    .Any(effect => effect.name.StartsWith("VFX_MeleeHit", StringComparison.Ordinal)
                        && (effect.transform.position - contactPoint).sqrMagnitude < .01f), "RPC contact rendered in VFX pool");
                Check(contacts == 1, "one outcome for this target");
                Debug.Log("[M84] PASS Fusion single-player session delivered one authoritative melee impact to pooled VFX.");
                Status = "PASS";
            }
            catch (Exception error)
            {
                Status = "Failed";
                Debug.LogException(error);
            }
            finally
            {
                if (bootstrap != null) await bootstrap.Disconnect();
                if (bots != null) bots.SetBotCount(previousBotCount);
            }
        }

        private static async Task Wait(Func<bool> ready, string step)
        {
            double timeout = Time.realtimeSinceStartupAsDouble + 15;
            while (Application.isPlaying && Time.realtimeSinceStartupAsDouble < timeout)
            {
                if (ready()) return;
                await Task.Delay(20);
            }
            Check(false, step);
        }

        private static void Check(bool value, string step)
        {
            if (!value) throw new InvalidOperationException("[M84] " + step);
        }
    }
}
