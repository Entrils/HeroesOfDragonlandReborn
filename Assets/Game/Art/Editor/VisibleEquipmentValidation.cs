using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    // Real Unity objects and real equip/snapshot adapters; not a substitute for a Fusion session.
    public static class VisibleEquipmentValidation
    {
        public static string Status { get; private set; } = "Not run";
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("M58: " + message);
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly Inventory Inventory;
            internal readonly Equipment Equipment;
            internal readonly CharacterVisual Visual;
            internal readonly CharacterEquipmentVisual View;
            internal Fixture(ItemCatalog catalog, bool authority)
            {
                Root = new GameObject("M58 temporary avatar");
                Root.SetActive(false); // Configure Inventory before Awake constructs its state.
                Root.transform.position = new Vector3(0, 0, 100);
                Inventory = Root.AddComponent<Inventory>();
                Set(Inventory, "catalog", catalog);
                Inventory.SetAuthority(authority);
                Equipment = Root.AddComponent<Equipment>();
                Root.AddComponent<EquipmentVisualHooks>();
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath), Root.transform);
                Visual = model.GetComponent<CharacterVisual>();
                View = Root.AddComponent<CharacterEquipmentVisual>();
                Set(View, "visual", Visual);
                Set(View, "visualCatalog", AssetDatabase.LoadAssetAtPath<EquipmentVisualCatalog>(VisibleEquipmentSetup.CatalogPath));
                Root.SetActive(true);
            }
            public void Dispose() { Root.SetActive(false); UnityEngine.Object.Destroy(Root); }
        }
        private static ItemDefinition Clone(string path, EquipmentSlot slot, List<UnityEngine.Object> owned)
        {
            var source = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Check(source != null, "Missing test source: " + path);
            var item = UnityEngine.Object.Instantiate(source);
            owned.Add(item);
            var data = new SerializedObject(item);
            data.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
            data.FindProperty("equipmentSlot").intValue = (int)slot;
            data.FindProperty("equipmentRequirements").arraySize = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (item.Weapon != null)
            {
                var weapon = UnityEngine.Object.Instantiate(item.Weapon);
                owned.Add(weapon);
                var weaponData = new SerializedObject(weapon);
                weaponData.FindProperty("statRequirements").arraySize = 0;
                weaponData.ApplyModifiedPropertiesWithoutUndo();
                Set(item, "weapon", weapon);
            }
            Check(item.TryValidate(out _), "Valid temporary test definition");
            return item;
        }
        private static bool Snapshot(Equipment equipment, long revision, string[] ids)
        {
            // Exercise the same trusted adapter NetworkEquipment.Render calls, without RPC/authority bypass.
            var method = typeof(Equipment).GetMethod("ApplyNetworkSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(method != null, "Replication adapter exists");
            return (bool)method.Invoke(equipment, new object[] { revision, ids });
        }
        private static int Find(Inventory inventory, string id)
        {
            for (int i = 0; i < inventory.SlotCount; i++) if (inventory.GetSlot(i).ItemId == id) return i;
            return -1;
        }
        private static void CheckParts(Fixture fixture, EquipmentSlot slot, int expected)
        {
            Check(fixture.View.VisualCount(slot) == expected, slot + " part count");
            var item = fixture.Equipment.GetItem(slot);
            var definition = fixture.View.Catalog.Resolve(item);
            for (int i = 0; i < expected; i++)
            {
                var part = fixture.View.GetVisual(slot, i);
                Check(part != null && part.activeInHierarchy, slot + " active visual");
                var parent = slot == EquipmentSlot.MainHand ? fixture.Visual.GetSocket(CharacterSocket.RightHand)
                    : slot == EquipmentSlot.OffHand ? fixture.Visual.GetSocket(CharacterSocket.LeftHand) : definition.Parts[i].Resolve(fixture.Visual);
                Check(part.transform.parent == parent, slot + " mount follows configured bone/socket");
                Check(part.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), slot + " no visual collision");
                Check(part.GetComponentsInChildren<Rigidbody>(true).All(b => b.isKinematic && !b.detectCollisions), slot + " no visual physics authority");
            }
        }

        [MenuItem("Heroes of Dragonland/Characters/Run Visible Equipment Play Validation (M58)")]
        public static void Start()
        {
            Check(Application.isPlaying, "Use Play Mode through Unity MCP");
            Check(Status != "RUNNING", "Validation is already running");
            VisibleEquipmentSetup.Validate();
            Status = "RUNNING";
            var host = new GameObject("Temporary M58 validation").AddComponent<ArtPipelineValidationHost>();
            host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            var owned = new List<UnityEngine.Object>();
            Fixture local = null, replica = null;
            try
            {
                var items = new List<ItemDefinition>();
                foreach (string name in new[] { "IronSword", "Greatsword", "Spear", "Staff" })
                    items.Add(Clone("Assets/Game/Data/Items/" + name + ".asset", EquipmentSlot.MainHand, owned));
                foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Legs })
                    items.Add(Clone("Assets/Game/Data/Items/ScoutArmor.asset", slot, owned));
                var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                owned.Add(catalog);
                var catalogData = new SerializedObject(catalog);
                var entries = catalogData.FindProperty("items"); entries.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                catalogData.ApplyModifiedPropertiesWithoutUndo();
                Check(catalog.TryValidate(out _), "Temporary catalog is valid");
                local = new Fixture(catalog, true);
                foreach (var item in items) Check(local.Inventory.TryAdd(item), "Add local test gear");
                foreach (var item in items.Take(4))
                {
                    Check(local.Equipment.TryEquip(Find(local.Inventory, item.Id), EquipmentSlot.MainHand, out _), "Equip " + item.Weapon.Archetype);
                    CheckParts(local, EquipmentSlot.MainHand, 1);
                    var instance = local.View.GetVisual(EquipmentSlot.MainHand);
                    var visualDefinition = local.View.Catalog.Resolve(item);
                    var expectedPrefab = visualDefinition?.Parts[0].Prefab ?? item.Weapon.WeaponPrefab;
                    Check(instance.name.StartsWith(expectedPrefab.name, StringComparison.Ordinal), "Use the resolved equipment visual");
                    local.View.SetWeaponStowed(true);
                    Check(local.View.GetVisual(EquipmentSlot.MainHand) == instance && instance.transform.parent == local.Visual.GetSocket(CharacterSocket.Back), "Move same weapon to back");
                    local.View.SetWeaponStowed(false);
                    CheckParts(local, EquipmentSlot.MainHand, 1);
                    Check(local.Equipment.TryUnequip(EquipmentSlot.MainHand, out _), "Unequip weapon");
                    Check(local.View.VisualCount(EquipmentSlot.MainHand) == 0 && !instance.activeSelf, "Unequip hides immediately");
                    yield return null;
                }
                foreach (var item in items.Skip(4))
                    Check(local.Equipment.TryEquip(Find(local.Inventory, item.Id), item.EquipmentSlot, out _), "Equip armor " + item.EquipmentSlot);
                var counts = new[] { (EquipmentSlot.Head, 1), (EquipmentSlot.Chest, 1), (EquipmentSlot.Hands, 2), (EquipmentSlot.Legs, 4) };
                foreach (var pair in counts) CheckParts(local, pair.Item1, pair.Item2);
                var head = local.View.GetVisual(EquipmentSlot.Head);
                long revision = local.Equipment.Revision;
                local.View.Refresh(); local.View.Refresh(); local.Root.GetComponent<EquipmentVisualHooks>().Refresh();
                Check(local.View.GetVisual(EquipmentSlot.Head) == head && local.Equipment.Revision == revision, "Idempotent refresh and unchanged equipment state");
                local.Visual.Apply(new CharacterVisualState { Dead = true, ActionSpeed = 1 }, .1f);
                yield return new WaitForSeconds(.6f);
                foreach (var pair in counts) CheckParts(local, pair.Item1, pair.Item2);
                Check(local.View.GetVisual(EquipmentSlot.Head) == head && local.Equipment.Revision == revision, "Death pose retains mounted gear without authority mutation");
                local.Visual.Apply(new CharacterVisualState { ActionSpeed = 1 }, .1f);
                local.View.enabled = false;
                Check(!head.activeSelf && local.View.VisualCount(EquipmentSlot.Head) == 0, "Disable clears owned parts");
                local.View.enabled = true;
                foreach (var pair in counts) CheckParts(local, pair.Item1, pair.Item2);
                yield return null;

                replica = new Fixture(catalog, false);
                var ids = new string[6];
                ids[0] = items[0].Id;
                foreach (var item in items.Skip(4)) ids[(int)item.EquipmentSlot - 1] = item.Id;
                replica.View.enabled = false;
                Check(Snapshot(replica.Equipment, 1, ids), "Apply public equipment snapshot on non-authority replica");
                replica.View.enabled = true; // Late subscriber/recreated presentation queries the complete state.
                foreach (var pair in counts) CheckParts(replica, pair.Item1, pair.Item2);
                CheckParts(replica, EquipmentSlot.MainHand, 1);
                var remoteHead = replica.View.GetVisual(EquipmentSlot.Head);
                Check(!Snapshot(replica.Equipment, 1, ids), "Repeated snapshot revision rejected");
                Check(replica.View.GetVisual(EquipmentSlot.Head) == remoteHead, "Repeated replica snapshot keeps same instance");
                Check(!replica.Equipment.TryUnequip(EquipmentSlot.Head, out _), "Replica cannot mutate authoritative equipment");
                long remoteRevision = 1;
                foreach (var item in items.Take(4))
                {
                    ids[0] = item.Id;
                    Check(Snapshot(replica.Equipment, ++remoteRevision, ids), "Replicated weapon change");
                    CheckParts(replica, EquipmentSlot.MainHand, 1);
                    Check(replica.View.GetVisual(EquipmentSlot.Head) == remoteHead, "Weapon change does not respawn armor");
                }
                replica.Root.SetActive(false);
                replica.Root.SetActive(true); // Lifecycle reconstruction, not a claimed real network reconnect.
                foreach (var pair in counts) CheckParts(replica, pair.Item1, pair.Item2);
                Check(Snapshot(replica.Equipment, ++remoteRevision, new string[6]), "Replicated unequip snapshot");
                for (int i = 1; i <= 6; i++) Check(replica.View.VisualCount((EquipmentSlot)i) == 0, "Replica clears all removed slots");
                foreach (var item in items.Skip(4)) Check(local.Equipment.TryUnequip(item.EquipmentSlot, out _), "Unequip each armor slot");
                foreach (var pair in counts) Check(local.View.VisualCount(pair.Item1) == 0, "Armor visual removed on unequip");
                var unsafePrefab = new GameObject("Unsafe equipment test");
                owned.Add(unsafePrefab);
                unsafePrefab.AddComponent<MeshRenderer>();
                unsafePrefab.AddComponent<Camera>().enabled = false;
                Check(!EquipmentVisualSafety.CanSpawn(unsafePrefab, out _), "Reject non-presentation prefab before instantiation");
                Status = "PASS local four weapons/four armor slots, mounts, stow, idempotence, unequip, death pose, enable/recreation and trusted snapshot replica. Actual Fusion respawn/reconnect/remote sessions still pending.";
                Debug.Log("[M58 Validation] " + Status);
            }
            finally
            {
                local?.Dispose(); replica?.Dispose();
                foreach (var value in owned) if (value != null) UnityEngine.Object.Destroy(value);
                if (Status == "RUNNING") Status = "FAILED; see Console";
                UnityEngine.Object.Destroy(host);
            }
        }
    }
}
