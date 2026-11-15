using System;
using System.Collections.Generic;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI;
using HeroesOfDragonland.World.Camps;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Editor
{
    public static class NeutralBossValidation
    {
        private static int checks;
        private static void Check(bool value, string message) { if (!value) throw new Exception("[NeutralBossValidation] FAIL " + message); checks++; }
        private static void Near(float value, float expected, string message) => Check(Mathf.Abs(value - expected) < .01f, message + " actual=" + value + " expected=" + expected);
        [MenuItem("Heroes of Dragonland/Battlefield/Validate Boss In Play Mode")]
        public static void Validate()
        {
            Check(Application.isPlaying, "Play Mode required"); checks = 0;
            const string folder = BattlefieldSceneBuilder.Folder;
            var camp = AssetDatabase.LoadAssetAtPath<NetworkCampDefinition>(folder + "/BossCamp.asset");
            Check(camp.TryValidate(out var reason), reason);
            Check(camp.Enemies.Count == 1 && camp.ExperienceReward == 300, "single high value boss");
            var prefab = camp.Enemies[0].Prefab.gameObject; var definition = prefab.GetComponent<EnemyBrain>().Definition;
            Check(definition.Boss != null && definition.AttackSet.Count == 3 && definition.Boss.PhaseTwoAttacks.Count == 4, "two complete attack sets");
            Check(definition.Stats.MaxHealth == 1800 && definition.Stats.Poise == 250, "2-4 player HP and poise budget");
            Check(prefab.GetComponent<BossEncounter>() != null && prefab.GetComponent<BossTelegraphPresentation>() != null, "boss components");
            Check(prefab.GetComponent<NetworkFortressGuard>() == null && (TeamIdentity.Find(prefab) == null || !TeamIdentity.Find(prefab).HasTeam), "neutral identity");
            Check(AssetDatabase.GetAssetPath(camp.ChestPrefab).EndsWith("/WardenReliquary.prefab"), "exclusive chest prefab");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
            var loot = camp.ChestPrefab.GetComponent<LootChest>().Table;
            Check(loot.TryValidate(catalog, out reason), "boss rewards in player catalog " + reason);
            Check(loot.TryRoll(new System.Random(47), out var rewards, out reason) && rewards.Length >= 4 && rewards.Length <= 5, "high value roll");
            foreach (var reward in rewards) Check(catalog.TryGet(reward.ItemId, out var item) && item.Rarity >= ItemRarity.Epic, "epic or legendary rewards only");
            var objects = new List<GameObject>(); Vector3 origin = new Vector3(1100, 0, 1100);
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(60, 1, 60),
                transform = Matrix4x4.TRS(origin - Vector3.up * .5f, Quaternion.identity, Vector3.one), area = 0 } };
            var mesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, new Bounds(origin, new Vector3(64, 8, 64)), Vector3.zero, Quaternion.identity);
            var instance = NavMesh.AddNavMeshData(mesh);
            try
            {
                var a = Player("Team A", TeamId.TeamA, origin + Vector3.forward * 3, catalog, objects);
                var b = Player("Team B", TeamId.TeamB, origin + Vector3.back * 3, catalog, objects);
                var ta = a.GetComponent<EnemyThreatTarget>(); var tb = b.GetComponent<EnemyThreatTarget>();
                var sa = a.GetComponent<CharacterStats>(); var sb = b.GetComponent<CharacterStats>();
                var actor = new GameObject("M47 boss"); objects.Add(actor); actor.SetActive(false); actor.transform.position = origin;
                actor.AddComponent<CharacterStats>(); var agent = actor.AddComponent<NavMeshAgent>(); agent.enabled = false;
                var brain = actor.AddComponent<EnemyBrain>(); var boss = actor.AddComponent<BossEncounter>();
                Check(brain.TryConfigureSpawn(definition, true), "configure shared AI");
                actor.SetActive(true); agent.enabled = true; Physics.SyncTransforms();
                var attack = actor.GetComponent<EnemyMeleeAttack>(); var receiver = actor.GetComponent<DamageReceiver>(); var stats = actor.GetComponent<CharacterStats>();
                Check(receiver.TakeDamage(1, a) == 1, "Team A damages neutral boss"); brain.Tick(.5f);
                Check(boss.Phase == BossPhase.PhaseOne && attack.IsBusy, "phase one begins");
                Vector3 direction = attack.AttackForward;
                Check(receiver.TakeDamage(1, b) == 1 && brain.AggroTarget == ta && attack.AttackForward == direction, "Team B damage does not snap telegraph");
                attack.Tick(.5f); Near(sa.CurrentHealth, 100, "no early hit");
                attack.Tick(.7f); Near(sa.CurrentHealth, 72, "cleave lands once"); Near(sb.CurrentHealth, 100, "cleave excludes rear target");
                Check(!attack.TryStart(ta), "recovery blocks immediate repeat"); attack.Tick(5);
                brain.Tick(7.1f); Check(brain.AggroTarget == tb, "deliberate target switch to opposing team"); attack.Cancel(); attack.ResetPattern();
                stats.ApplyDamage(900); Check(attack.TryStart(ta), "committed phase one attack at threshold");
                float windup = attack.Windup; brain.Tick(.1f); Check(boss.Phase == BossPhase.PhaseOne && attack.Windup == windup, "threshold preserves committed windup");
                attack.Tick(5); sa.ResetToFull(); sb.ResetToFull(); brain.Tick(.1f);
                Check(boss.Phase == BossPhase.Transition && !attack.TryStart(ta), "enrage transition prevents attacks");
                brain.Tick(2.4f); Check(boss.Phase == BossPhase.Transition && !attack.IsBusy, "roar grants full reaction window");
                brain.Tick(.2f); Check(boss.Phase == BossPhase.PhaseTwo && boss.SpeedMultiplier > 1, "phase two enrage");
                attack.Cancel(); attack.ResetPattern();
                for (int i = 0; i < 4; i++)
                {
                    sa.ResetToFull(); sb.ResetToFull(); a.GetComponent<CombatDefense>().Tick(10); b.GetComponent<CombatDefense>().Tick(10);
                    Check(attack.TryStart(ta) && attack.AttackIndex == i, "phase two attack " + i);
                    var move = definition.Boss.PhaseTwoAttacks.Get(i);
                    attack.Tick(move.Windup - .01f); Near(sa.CurrentHealth, 100, "telegraph " + i);
                    attack.Tick(.02f); Near(sa.CurrentHealth, 100 - move.Damage, "phase two damage " + i);
                    Near(sb.CurrentHealth, move.Angle >= 359 ? 100 - move.Damage : 100, "sector or area rule " + i);
                    attack.Tick(5);
                }
                sa.ResetToFull(); sb.ResetToFull(); a.GetComponent<CombatDefense>().Tick(10); b.GetComponent<CombatDefense>().Tick(10);
                Check(attack.TryStart(ta), "evasion fixture"); a.transform.position += Vector3.forward * 9; Physics.SyncTransforms(); attack.Tick(5);
                Near(sa.CurrentHealth, 100, "moving out avoids damage");
                a.transform.position = origin + Vector3.forward * 3; Physics.SyncTransforms();
                var defense = actor.GetComponent<CombatDefense>(); defense.ApplyPoiseDamage(100);
                Check(!defense.IsStaggered, "resists ordinary stagger"); defense.ApplyPoiseDamage(160); Check(defense.IsStaggered, "coordinated poise break possible"); defense.Tick(3);
                b.GetComponent<DamageReceiver>().TakeDamage(1, a); a.GetComponent<DamageReceiver>().TakeDamage(1, b);
                Check(sa.CurrentHealth < 100 && sb.CurrentHealth < 100, "PvP damage available both ways during encounter");
                b.SetActive(false); brain.Tick(.1f);
                Check(brain.AggroTarget == ta && stats.CurrentHealth < stats.MaxHealth, "lost target replaced without resetting surviving challengers");
                a.SetActive(false); agent.Warp(origin + Vector3.right * 5); Physics.SyncTransforms(); brain.Tick(6);
                Check((brain.State == EnemyState.Return || brain.State == EnemyState.Idle) && boss.Phase == BossPhase.Dormant && !attack.IsBusy, "wipe resets encounter"); Near(stats.CurrentHealth, 1800, "evade restores health");
                Near(receiver.TakeDamage(10, a), 0, "returning boss cannot be chipped for free");
                agent.Warp(origin); brain.Tick(.1f); Check(brain.State == EnemyState.Idle, "returned home ready for solo retry");
                a.SetActive(true); a.transform.position = origin + Vector3.forward * 3; Physics.SyncTransforms(); receiver.TakeDamage(1, a); brain.Tick(.5f);
                Check(boss.Phase == BossPhase.PhaseOne && brain.AggroTarget == ta, "solo attempt can start without party gate");
                brain.SetAuthority(false); var phase = boss.Phase; brain.Tick(100);
                Check(boss.Phase == phase && !attack.TryStart(ta), "non-authority cannot advance boss or attack"); brain.SetAuthority(true);
                stats.ApplyDamage(10000); Check(boss.Phase == BossPhase.Defeated && brain.State == EnemyState.Dead && !attack.TryStart(ta), "death ends combat");
                // Exercise the real atomic chest transaction with a local authority fixture.
                a.transform.position = origin + Vector3.right * 2; b.SetActive(true); b.transform.position = origin + Vector3.left * 2;
                var chestObject = new GameObject("M47 reward transaction"); objects.Add(chestObject); chestObject.SetActive(false); chestObject.transform.position = origin;
                var chest = chestObject.AddComponent<LootChest>(); chestObject.AddComponent<BossChestValidationAuthority>();
                Check(chest.TryConfigureSpawn(loot, true), "configure exclusive chest"); chestObject.SetActive(true); Physics.SyncTransforms();
                var ia = a.GetComponent<Inventory>(); var ib = b.GetComponent<Inventory>();
                long revA = ia.Revision, revB = ib.Revision;
                bool reentryDenied = false; chest.Changed += () => reentryDenied = !chest.TryInteract(ib, out _);
                Check(chest.TryInteract(ia, out reason), "first team claims complete roll " + reason);
                Check(chest.IsOpen && reentryDenied && !chest.TryInteract(ia, out _) && !chest.TryInteract(ib, out _), "repeat and competing reward claims rejected");
                Check(ia.Revision == revA + 1 && ib.Revision == revB && chest.Revision == 1, "exactly one atomic reward transaction");
                var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>(); Check(view != null, "HUD exists");
                view.SetBoss(true, "Stone Warden", "PHASE 2", 900, 1800, "SHOCKWAVE"); Check(view.HasBoss && Mathf.Approximately(view.BossHealthValue, .5f), "boss health UI"); view.SetBoss(false); Check(!view.HasBoss, "boss UI hides on exit");
                Debug.Log("[NeutralBossValidation] PASS " + checks + " asset, combat, phase, PvPvE, reset, UI and atomic chest assertions.");
            }
            finally { foreach (var go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go); instance.Remove(); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private static GameObject Player(string name, TeamId team, Vector3 position, ItemCatalog catalog, List<GameObject> objects)
        {
            var go = new GameObject("M47 " + name); objects.Add(go); go.SetActive(false); go.transform.position = position;
            go.AddComponent<CharacterStats>(); go.AddComponent<EnemyThreatTarget>();
            var collider = go.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2;
            go.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.Player);
            var inventory = go.AddComponent<Inventory>(); var data = new SerializedObject(inventory); data.FindProperty("catalog").objectReferenceValue = catalog; data.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true); return go;
        }
    }
    // Editor-only authority to invoke the production transaction without a Fusion room.
    public sealed class BossChestValidationAuthority : MonoBehaviour, ILootChestAuthority
    {
        public bool CanMutateChest => true;
        public bool AllowsClaimant(Inventory inventory) => inventory != null && inventory.HasAuthority;
    }
}
