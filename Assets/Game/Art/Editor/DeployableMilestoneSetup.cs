using System;
using Fusion;
using Fusion.Editor;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.Art.Editor
{
    public static class DeployableMilestoneSetup
    {
        private const string Folder = "Assets/Game/Data/Deployables";

        [MenuItem("Heroes of Dragonland/Install M72 Deployables")]
        public static void Install()
        {
            HumanoidAssetBuilder.Folder(Folder);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
            var loot = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/Game/Data/Battlefield/LowLoot.asset");
            InstallTrap("BearTrap", "Bear Trap", "Deals damage and briefly stops movement.", 30, .95f, 0, .85f, new Color(.64f,.43f,.24f), catalog, loot);
            InstallTrap("MagicMine", "Magic Mine", "Bursts for damage in an area.", 36, 0, 1, 2.7f, new Color(.66f,.24f,1), catalog, loot);
            InstallTrap("SlowTrap", "Slow Trap", "Reduces movement speed temporarily.", 0, 3.5f, .45f, 1.05f, new Color(.15f,.81f,.65f), catalog, loot);
            if (!catalog.TryValidate(out string error) || !loot.TryValidate(catalog, out error))
                throw new InvalidOperationException(error);
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkProjectConfigAsset"))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            foreach (string name in new[] { "BearTrap", "MagicMine", "SlowTrap" })
                if (!NetworkProjectConfigUtilities.TryGetPrefabId(Folder + "/" + name + ".prefab", out _))
                    throw new InvalidOperationException("Unregistered trap " + name);
            Debug.Log("[M72] Three deployables saved and registered with Fusion.");
        }

        private static void InstallTrap(string key, string label, string description, float damage, float duration,
            float multiplier, float effectRadius, Color color, ItemCatalog catalog, LootTable loot)
        {
            var definition = LoadOrCreate<DeployableDefinition>($"{Folder}/{key}.asset");
            var prefab = MakePrefab(key, definition, color);
            var settings = new SerializedObject(definition);
            settings.FindProperty("prefab").objectReferenceValue = prefab;
            settings.FindProperty("damage").floatValue = damage;
            settings.FindProperty("movementSeconds").floatValue = duration;
            settings.FindProperty("movementMultiplier").floatValue = multiplier;
            settings.FindProperty("effectRadius").floatValue = effectRadius;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var item = LoadOrCreate<ItemDefinition>($"Assets/Game/Data/Items/{key}.asset");
            var data = new SerializedObject(item);
            var id = data.FindProperty("id");
            if (string.IsNullOrEmpty(id.stringValue)) id.stringValue = Guid.NewGuid().ToString("N");
            data.FindProperty("displayName").stringValue = label;
            data.FindProperty("description").stringValue = description;
            data.FindProperty("category").intValue = (int)ItemCategory.Deployable;
            data.FindProperty("maximumStackSize").intValue = 5;
            data.FindProperty("value").intValue = 35;
            data.FindProperty("deployable").objectReferenceValue = definition;
            data.ApplyModifiedPropertiesWithoutUndo();

            var items = new SerializedObject(catalog);
            var array = items.FindProperty("items");
            bool found = false;
            for (int i = 0; i < array.arraySize; i++)
                found |= array.GetArrayElementAtIndex(i).objectReferenceValue == item;
            if (!found)
            {
                array.InsertArrayElementAtIndex(array.arraySize);
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = item;
                items.ApplyModifiedPropertiesWithoutUndo();
            }

            var drops = new SerializedObject(loot);
            var entries = drops.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) return;
            entries.InsertArrayElementAtIndex(entries.arraySize);
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("weight").floatValue = 5;
            entry.FindPropertyRelative("minimumQuantity").intValue = 1;
            entry.FindPropertyRelative("maximumQuantity").intValue = 1;
            drops.ApplyModifiedPropertiesWithoutUndo();
        }

        private static NetworkDeployable MakePrefab(string name, DeployableDefinition definition, Color color)
        {
            string path = $"{Folder}/{name}.prefab";
            var saved = AssetDatabase.LoadAssetAtPath<NetworkDeployable>(path);
            if (saved != null) return saved;
            var root = new GameObject(name);
            try
            {
                var trap = root.AddComponent<NetworkDeployable>();
                Material casing = FindMaterial(name, color);
                var marker = Part(root.transform, "Team marker", PrimitiveType.Cylinder, Vector3.up * .089f,
                    new Vector3(.27f, .012f, .27f), casing);
                if (name == "BearTrap")
                {
                    Part(root.transform, "Base", PrimitiveType.Cube, Vector3.up * .056f, new Vector3(.74f,.11f,.50f), casing);
                    for (int side = -1; side <= 1; side += 2)
                        for (int i = -1; i <= 1; i++)
                        {
                            var tooth = Part(root.transform, "Jaw tooth", PrimitiveType.Cube, new Vector3(side*.28f,.17f,i*.15f),
                                new Vector3(.095f,.18f,.08f), casing);
                            tooth.transform.localRotation = Quaternion.Euler(0, 0, side * 23);
                        }
                    marker.transform.localPosition = Vector3.up * .12f;
                }
                else
                {
                    Part(root.transform, "Casing", PrimitiveType.Cylinder, Vector3.up * .04f, new Vector3(.69f,.035f,.69f), casing);
                    if (name == "MagicMine")
                    {
                        var core = Part(root.transform, "Core", PrimitiveType.Cube, Vector3.up * .21f, Vector3.one * .19f, casing);
                        core.transform.rotation = Quaternion.Euler(35,45,12);
                    }
                    else
                        for (int i = -1; i <= 1; i += 2)
                            Part(root.transform, "Frost plate", PrimitiveType.Cube, new Vector3(i*.19f,.093f,0), new Vector3(.12f,.065f,.42f), casing);
                }
                var fields = new SerializedObject(trap);
                fields.FindProperty("definition").objectReferenceValue = definition;
                fields.FindProperty("teamMarker").objectReferenceValue = marker;
                fields.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NetworkDeployable>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Material FindMaterial(string name, Color color)
        {
            string path = $"{Folder}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Renderer Part(Transform root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var current = AssetDatabase.LoadAssetAtPath<T>(path);
            if (current != null) return current;
            current = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(current, path);
            return current;
        }
    }
}
