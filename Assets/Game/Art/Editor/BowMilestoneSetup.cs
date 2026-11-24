using System;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfDragonland.Art.Editor
{
    public static class BowMilestoneSetup
    {
        private const string ItemsPath = "Assets/Game/Data/Items/";
        private const string CombatPath = "Assets/Game/Data/Weapons/";
        private const string VisualPath = "Assets/Art/Weapons/Prefabs/PV_HuntingBow.prefab";

        [MenuItem("Heroes of Dragonland/Characters/Install M71 Bow")]
        public static void Install()
        {
            HumanoidAssetBuilder.Folder(CombatPath.TrimEnd('/'));
            HumanoidAssetBuilder.Folder("Assets/Art/Weapons/Prefabs");
            var arrowVisual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vfx/Prefabs/ModelPack/PV_NeutralArrow.prefab");
            if (arrowVisual == null || !EquipmentVisualSafety.CanSpawn(arrowVisual, out _))
                throw new InvalidOperationException("M71 requires the inert NeutralArrow presentation prefab.");
            var basicArrow = Asset<ArrowDefinition>(CombatPath + "BasicArrow.asset");
            var profile = Asset<BowProfile>(CombatPath + "HuntingBowProfile.asset");
            Edit(profile, data =>
            {
                Set(data, "basicArrow", basicArrow);
                Set(data, "projectileVisual", arrowVisual);
            });
            var prefab = MakeVisual();
            var weapon = Asset<WeaponDefinition>(CombatPath + "HuntingBow.asset");
            Edit(weapon, data =>
            {
                data.FindProperty("archetype").intValue = (int)WeaponArchetype.Bow;
                data.FindProperty("baseDamage").floatValue = 26;
                data.FindProperty("staminaCost").floatValue = 16;
                data.FindProperty("attackSpeed").floatValue = 1;
                data.FindProperty("range").floatValue = 24;
                var requirements = data.FindProperty("statRequirements");
                requirements.arraySize = 1;
                var dexterity = requirements.GetArrayElementAtIndex(0);
                dexterity.FindPropertyRelative("stat").intValue = (int)ItemStat.Dexterity;
                dexterity.FindPropertyRelative("minimumValue").floatValue = 11;
                Set(data, "bow", profile);
                Set(data, "weaponPrefab", prefab);
            });
            var item = Asset<ItemDefinition>(ItemsPath + "HuntingBow.asset");
            Edit(item, data =>
            {
                var id = data.FindProperty("id");
                if (string.IsNullOrEmpty(id.stringValue)) id.stringValue = Guid.NewGuid().ToString("N");
                data.FindProperty("displayName").stringValue = "Hunting Bow";
                data.FindProperty("description").stringValue = "Hold the secondary button to aim. Press attack to draw and loose a basic arrow.";
                data.FindProperty("equipmentSlot").intValue = (int)EquipmentSlot.MainHand;
                data.FindProperty("maximumStackSize").intValue = 1;
                data.FindProperty("value").intValue = 45;
                Set(data, "weapon", weapon);
            });
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemsPath + "TestItemCatalog.asset");
            Edit(catalog, data => { Append(data.FindProperty("items"), item); });
            var loot = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/Game/Data/Battlefield/LowLoot.asset");
            Edit(loot, data =>
            {
                var entries = data.FindProperty("entries");
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) return;
                var index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                var entry = entries.GetArrayElementAtIndex(index);
                Set(entry, "item", item);
                entry.FindPropertyRelative("weight").floatValue = 10;
                entry.FindPropertyRelative("minimumQuantity").intValue = 1;
                entry.FindPropertyRelative("maximumQuantity").intValue = 1;
            });
            if (!catalog.TryValidate(out var error) || !loot.TryValidate(catalog, out error))
                throw new InvalidOperationException(error);
            AssetDatabase.SaveAssets();
            Debug.Log("[M71] Bow, arrow, item catalog and low camp loot validated and saved.");
        }

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Edit(Object asset, Action<SerializedObject> modify)
        {
            if (asset == null) throw new InvalidOperationException("Required M71 dependency is missing.");
            var data = new SerializedObject(asset);
            modify(data);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject data, string property, Object value) =>
            data.FindProperty(property).objectReferenceValue = value;
        private static void Set(SerializedProperty data, string property, Object value) =>
            data.FindPropertyRelative(property).objectReferenceValue = value;

        private static void Append(SerializedProperty array, Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            array.InsertArrayElementAtIndex(array.arraySize);
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
        }

        private static GameObject MakeVisual()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (existing != null) return existing;
            const string folder = "Assets/Art/Equipment/Materials/";
            var wood = MaterialAt(folder + "M_HuntingBow_Wood.mat", new Color(.28f,.12f,.055f));
            var stringMaterial = MaterialAt(folder + "M_HuntingBow_String.mat", new Color(.65f,.55f,.36f));
            var root = new GameObject("PV_HuntingBow");
            try
            {
                Segment(root.transform, "Wrapped grip", Vector3.down * .11f, Vector3.up * .11f, .036f, stringMaterial);
                foreach (int side in new[] { -1, 1 })
                {
                    var shoulder = new Vector3(0, side*.30f, .075f);
                    var curve = new Vector3(0, side*.56f, -.055f);
                    var tip = new Vector3(0, side*.72f, -.01f);
                    Segment(root.transform, "Inner limb", Vector3.up * (side * .085f), shoulder, .028f, wood);
                    Segment(root.transform, "Outer limb", shoulder, curve, .022f, wood);
                    Segment(root.transform, "Recurve tip", curve, tip, .015f, wood);
                    Segment(root.transform, side == 1 ? "StringUpper" : "StringLower", tip, new Vector3(0,0,-.01f), .005f, stringMaterial);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Material MaterialAt(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Segment(Transform parent, string name, Vector3 start, Vector3 end, float width, Material material)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(segment.GetComponent<Collider>());
            segment.name = name;
            segment.transform.SetParent(parent, false);
            segment.transform.SetLocalPositionAndRotation((start + end) * .5f, Quaternion.FromToRotation(Vector3.up, end - start));
            segment.transform.localScale = new Vector3(width, Vector3.Distance(start, end) * .5f, width);
            segment.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
