#if UNITY_EDITOR || DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.World;
using HeroesOfDragonland.World.Camps;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Presentation
{
    [DefaultExecutionOrder(500)]
    public sealed class ModelPackAcceptanceProbe : MonoBehaviour
    {
        private const string Flag = "-hodrModelPackCheck";
        private const string DragonbladeFlag = "-hodrModelPackDragonblade";
        private const string LateFlag = "-hodrModelPackLate";
        // Staff uses the legacy Intelligence gate. A normal match has no way to
        // allocate that stat, so it is covered by the separate visual fixture.
        private static readonly string[] StandardItems = { "IronSword", "Greatsword", "Spear" };
        private static readonly string[] SwordItems = { "IronSword", "Dragonblade" };
        private static readonly string[] Models = { "IronSword", "Dragonblade", "Greatsword", "Spear", "PlayerStaff" };
        private readonly HashSet<string> ownerModels = new();
        private readonly HashSet<string> replicaModels = new();
        private readonly HashSet<EnemyArchetype> released = new();
        private readonly HashSet<EnemyArchetype> stopped = new();
        private readonly Dictionary<NetworkId, Vector3> flight = new();
        private readonly HashSet<EnemyArchetype> travelled = new();
        private MultiplayerBootstrap bootstrap;
        private NetworkCamp camp;
        private float started = -1;
        private float repositionAt;
        private int equipped = -1;
        private bool sawDraw;
        private bool sawBlock;
        private bool passed;
        private bool lateObserver;
        private NetworkRunner originalRunner;
        private bool compareSwords;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            bool late = args.Contains(LateFlag);
            bool swords = args.Contains(DragonbladeFlag);
            if (!late && !swords && !args.Contains(Flag)) return;
            var host = FindAnyObjectByType<MultiplayerBootstrap>();
            if (host == null) return;
            var probe = host.gameObject.AddComponent<ModelPackAcceptanceProbe>();
            probe.lateObserver = late;
            probe.compareSwords = swords;
        }

        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();

        private void LateUpdate()
        {
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)
                || (passed && !runner.IsServer)) return;
            if (lateObserver)
            {
                CheckLateGateState(runner);
                return;
            }
            var humans = FindObjectsByType<NetworkTestPlayer>()
                .Where(p => !p.IsBot && p.Object != null && p.Object.IsValid && p.Runner == runner).OrderBy(p => p.Team).ToArray();
            if (humans.Length != 2) return;
            if (started < 0) started = Time.realtimeSinceStartup;
            float age = Time.realtimeSinceStartup - started;
            string[] items = compareSwords ? SwordItems : StandardItems;
            camp ??= FindObjectsByType<NetworkCamp>().FirstOrDefault(c =>
                c.Object != null && c.Object.IsValid && c.Runner == runner && c.Definition.Difficulty == CampDifficulty.Tier3
                && (runner.IsServer || c.State == NetworkCampState.Active));
            if (camp == null) return;
            if (runner.IsServer && camp.State == NetworkCampState.Pending)
                camp.TryStartDevelopmentEncounter(compareSwords ? DragonbladeFlag : Flag);
            if (camp.State != NetworkCampState.Active) return;

            var foes = FindObjectsByType<NetworkEnemy>().Where(enemy =>
                enemy.Object != null && enemy.Object.IsValid && enemy.Runner == runner &&
                Vector3.Distance(enemy.transform.position, camp.transform.position) < 20).ToArray();
            var knight = foes.FirstOrDefault(e => e.Definition.Archetype == EnemyArchetype.Knight);
            if (knight != null) sawBlock |= knight.Blocking;
            foreach (var enemy in foes.Where(e => e.Definition.IsRanged)) CheckProjectile(enemy);

            if (runner.IsServer)
            {
                int expected = Mathf.Min(items.Length - 1, (int)(age / 3));
                if (equipped != expected)
                {
                    if (equipped < 0)
                        foreach (var human in humans)
                            Require(human.GetComponent<NetworkSessionProgression>().TryGrantExperience(10000, "model-pack-equipment"),
                                "grant test progression through the server");
                    equipped = expected;
                    foreach (var human in humans) Equip(human, items[equipped]);
                }
                if (age >= repositionAt)
                {
                    repositionAt = age + 2.5f;
                    PositionForCombat(humans[0], knight, -Vector3.forward, 2.3f);
                    var ranged = foes.FirstOrDefault(e => e.Definition.Archetype == EnemyArchetype.Mage);
                    PositionForCombat(humans[1], ranged, Vector3.forward, 5f);
                }
                foreach (var human in humans) human.GetComponent<CharacterStats>().ResetToFull();
            }
            // Attack the neutral knight from each owner's normal network input.
            bool ownsKnightTarget = humans[0].HasInputAuthority;
            bootstrap.TestInput = Vector2.zero;
            bootstrap.TestFacing = ownsKnightTarget ? Vector2.up : Vector2.down;
            bootstrap.TestAttack = ownsKnightTarget && age % 3 < .65f;
            foreach (var human in humans) CheckWeapon(human);

            bool equipmentPassed = ownerModels.Count == items.Length && replicaModels.Count == items.Length;
            bool rangedPassed = released.Count == 2 && travelled.Count == 2 && stopped.Count == 2 && sawDraw && sawBlock;
            if (!passed && equipmentPassed && (compareSwords || rangedPassed))
            {
                passed = true;
                string scope = compareSwords ? "separate IronSword and Dragonblade models" :
                    "both ranged models wind up, fly and stop, knight blocks, three standard weapon models";
                Debug.Log("[ModelPack] PASS " + scope + "; owner and observer weapon mounts; server=" + runner.IsServer);
            }
            if (age > 45 && !passed)
            {
                passed = true;
                Debug.LogError($"[ModelPack] FAIL owner={ownerModels.Count} replica={replicaModels.Count} release={released.Count} travel={travelled.Count} stop={stopped.Count} bow={sawDraw} block={sawBlock}");
            }
        }

        private void CheckProjectile(NetworkEnemy enemy)
        {
            var kind = enemy.Definition.Archetype;
            var prefab = kind == EnemyArchetype.Archer ? "PV_NeutralArrow(Clone)" : "PV_AmethystBolt(Clone)";
            var projectile = enemy.transform.Find(prefab);
            Require(projectile != null, prefab + " presentation missing");
            if (kind == EnemyArchetype.Archer)
            {
                var bow = enemy.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.sharedMesh.GetBlendShapeIndex("BowDraw") >= 0);
                float draw = bow.GetBlendShapeWeight(bow.sharedMesh.GetBlendShapeIndex("BowDraw"));
                sawDraw |= !enemy.ProjectileInFlight && draw > 20;
                if (enemy.ProjectileInFlight) Require(draw < 1, "bow released with the projectile");
            }
            Require(projectile.gameObject.activeSelf == (bool)enemy.ProjectileInFlight, prefab + " visibility");
            if (enemy.ProjectileInFlight)
            {
                Require(Vector3.Distance(projectile.position, enemy.ProjectilePosition) < .02f, prefab + " follows the authoritative position");
                Require(Vector3.Dot(projectile.forward, enemy.AttackForward) > .995f, prefab + " follows the authoritative direction");
                released.Add(kind);
                if (flight.TryGetValue(enemy.Object.Id, out var origin))
                {
                    if (Vector3.Distance(origin, projectile.position) > .1f) travelled.Add(kind);
                }
                else flight.Add(enemy.Object.Id, projectile.position);
            }
            else if (flight.Remove(enemy.Object.Id)) stopped.Add(kind);
        }

        private void CheckLateGateState(NetworkRunner runner)
        {
            if (ReferenceEquals(originalRunner, null)) originalRunner = runner;
            if (ReferenceEquals(runner, originalRunner)) return;
            var gates = FindObjectsByType<NetworkFortressGate>()
                .Where(g => g.Object != null && g.Object.IsValid && g.Runner == runner).ToArray();
            if (gates.Length != 2 || gates.Any(g => !g.IsOpen)) return;
            foreach (var gate in gates)
            {
                Require(gate.GetComponent<FortressGate>().IsOpen, "late breach state");
                Require(gate.transform.Find("FortressGate_Breached").gameObject.activeSelf
                    && !gate.transform.Find("Gate body").gameObject.activeSelf, "late gate visual state");
                Require(!gate.GetComponent<BoxCollider>().enabled, "late gate collision state");
            }
            passed = true;
            Debug.Log("[ModelPackLate] PASS both breached models and their disabled blockers after reconnection");
        }

        private void CheckWeapon(NetworkTestPlayer player)
        {
            var view = player.GetComponent<CharacterEquipmentVisual>();
            var rig = player.GetComponent<NetworkCharacterVisual>().Visual;
            var model = view.GetVisual(EquipmentSlot.MainHand);
            if (model == null) return;
            var name = Models.FirstOrDefault(candidate => model.name == "PV_" + candidate + "(Clone)");
            if (name == null) return;
            var observed = player.HasInputAuthority ? ownerModels : replicaModels;
            if (!observed.Add(name)) return;
            Require(model.transform.parent == rig.GetSocket(CharacterSocket.RightHand), name + " main hand");
            view.SetWeaponStowed(true);
            Require(model.transform.parent == rig.GetSocket(CharacterSocket.Back), name + " back socket");
            view.SetWeaponStowed(false);
            Require(model.transform.parent == rig.GetSocket(CharacterSocket.RightHand), name + " drawn socket");
        }

        private static void Equip(NetworkTestPlayer player, string name)
        {
            var inventory = player.GetComponent<Inventory>();
            var item = inventory.Catalog.Items.Single(candidate => candidate.name == name);
            var equipment = player.GetComponent<Equipment>();
            var progression = player.GetComponent<SessionProgression>();
            foreach (var requirement in item.EquipmentRequirements)
            {
                Require(equipment.TryGetRequirementValue(requirement.Stat, out var value), "missing " + requirement.Stat);
                for (int point = 0; point < Mathf.CeilToInt(requirement.MinimumValue - value); point++)
                    Require(progression.TryIncreaseAttribute(requirement.Stat, progression.SessionId, out var reason), reason);
            }
            if (inventory.GetAmount(item.Id) == 0) player.GetComponent<NetworkInventory>().TryAddItem(item.Id);
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                if (inventory.GetSlot(slot).ItemId != item.Id) continue;
                Require(equipment.TryEquip(slot, EquipmentSlot.MainHand, out var reason), name + ": " + reason);
                return;
            }
            Require(false, "could not add " + name);
        }

        private static void PositionForCombat(NetworkTestPlayer player, NetworkEnemy foe, Vector3 offset, float distance)
        {
            if (foe == null || !NavMesh.SamplePosition(foe.transform.position + offset * distance, out var target, 2, NavMesh.AllAreas)) return;
            player.GetComponent<NetworkTransform>().Teleport(target.position + Vector3.up * .1f, Quaternion.LookRotation(-offset));
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("[ModelPack] FAIL " + label);
        }
    }
}
#endif
