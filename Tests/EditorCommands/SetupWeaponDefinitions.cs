using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Items.Editor;
using HeroesOfDragonland.Combat;

internal class CommandScript : IRunCommand
{
    private const string Data = "Assets/Game/Data/Weapons/";
    private const string Art = "Assets/Art/Models/Weapons/";
    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static void SetRequirement(SerializedObject so, string field, ItemStat stat, float value)
    {
        var requirements = so.FindProperty(field); requirements.arraySize = 1;
        requirements.GetArrayElementAtIndex(0).FindPropertyRelative("stat").intValue = (int)stat;
        requirements.GetArrayElementAtIndex(0).FindPropertyRelative("minimumValue").floatValue = value;
    }
    private static void Part(GameObject root, string name, PrimitiveType type, Vector3 position, Vector3 scale, bool shaft = false)
    {
        var part = GameObject.CreatePrimitive(type); part.name = name;
        part.transform.SetParent(root.transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
        if (shaft) part.transform.localRotation = Quaternion.Euler(90, 0, 0);
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/MeleePlaceholder.mat");
        if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
    }
    public void Execute(ExecutionResult result)
    {
        if (Application.isPlaying) throw new Exception("Use Edit Mode");
        System.IO.Directory.CreateDirectory(Data); System.IO.Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var archetypes = new[] { "Sword", "Greatsword", "Spear", "Staff" };
        float[] damage = { 25, 40, 24, 18 }, cost = { 25, 35, 20, 18 }, speed = { 1, .7f, 1.1f, .9f }, reach = { 2.1f, 2.8f, 3.2f, 2.5f }, requirements = { 8, 16, 12, 12 }, stagger = { 20, 40, 18, 15 };
        ItemStat[] stats = { ItemStat.Strength, ItemStat.Strength, ItemStat.Dexterity, ItemStat.Intelligence };
        var weapons = new WeaponDefinition[4];
        var items = new List<ItemDefinition>();
        for (int i = 0; i < 4; i++)
        {
            string name = archetypes[i], prefabPath = Art + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var root = new GameObject(name + " placeholder");
                try
                {
                    float length = i == 1 ? 1.7f : i >= 2 ? 2.3f : 1.1f;
                    if (i < 2)
                    {
                        Part(root, "Blade", PrimitiveType.Cube, new Vector3(0, 0, length * .5f + .2f), new Vector3(i == 1 ? .2f : .12f, .045f, length));
                        Part(root, "Crossguard", PrimitiveType.Cube, new Vector3(0, 0, .18f), new Vector3(.45f, .08f, .08f));
                        Part(root, "Grip", PrimitiveType.Cylinder, new Vector3(0, 0, -.05f), new Vector3(.08f, .15f, .08f), true);
                    }
                    else
                    {
                        Part(root, "Shaft", PrimitiveType.Cylinder, new Vector3(0, 0, length * .4f), new Vector3(.065f, length * .5f, .065f), true);
                        Part(root, i == 2 ? "Spearhead" : "Staff focus", i == 2 ? PrimitiveType.Cube : PrimitiveType.Sphere,
                            new Vector3(0, 0, length * .9f), i == 2 ? new Vector3(.12f, .04f, .45f) : Vector3.one * .24f);
                    }
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var profile = Asset<MeleeAttackDefinition>(Data + name + "Attack.asset");
            var serialized = new SerializedObject(profile); serialized.FindProperty("staggerPower").floatValue = stagger[i];
            serialized.FindProperty("hitHalfExtents").vector3Value = new Vector3(i == 2 ? .35f : .8f, .8f, .9f); serialized.ApplyModifiedPropertiesWithoutUndo();
            var animations = Asset<WeaponAttackAnimationSet>(Data + name + "Animations.asset");
            serialized = new SerializedObject(animations); serialized.FindProperty("combatProfile").objectReferenceValue = profile; serialized.ApplyModifiedPropertiesWithoutUndo();
            var weapon = Asset<WeaponDefinition>(Data + name + ".asset"); weapons[i] = weapon;
            serialized = new SerializedObject(weapon);
            serialized.FindProperty("archetype").intValue = i; serialized.FindProperty("baseDamage").floatValue = damage[i];
            serialized.FindProperty("staminaCost").floatValue = cost[i]; serialized.FindProperty("attackSpeed").floatValue = speed[i]; serialized.FindProperty("range").floatValue = reach[i];
            serialized.FindProperty("attackAnimationSet").objectReferenceValue = animations; serialized.FindProperty("weaponPrefab").objectReferenceValue = prefab;
            SetRequirement(serialized, "statRequirements", stats[i], requirements[i]);
            var scaling = serialized.FindProperty("scalingPlaceholders"); scaling.arraySize = 1; scaling.GetArrayElementAtIndex(0).FindPropertyRelative("stat").intValue = (int)stats[i]; scaling.GetArrayElementAtIndex(0).FindPropertyRelative("coefficient").floatValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (i == 0) continue;
            var item = Asset<ItemDefinition>("Assets/Game/Data/Items/" + name + ".asset"); items.Add(item);
            serialized = new SerializedObject(item); serialized.FindProperty("id").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
            serialized.FindProperty("displayName").stringValue = name; serialized.FindProperty("description").stringValue = "Prototype " + name.ToLowerInvariant() + ".";
            serialized.FindProperty("category").intValue = (int)ItemCategory.Weapon; serialized.FindProperty("equipmentSlot").intValue = (int)EquipmentSlot.MainHand;
            serialized.FindProperty("weapon").objectReferenceValue = weapon; serialized.FindProperty("value").intValue = 40; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Game" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.Category != ItemCategory.Weapon || item.Weapon != null) continue;
            var serialized = new SerializedObject(item); serialized.FindProperty("weapon").objectReferenceValue = weapons[0]; serialized.ApplyModifiedPropertiesWithoutUndo();
            result.Log("Linked legacy sword: " + item.name + "; retained existing requirements and gear bonuses");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ItemCatalog", new[] { "Assets/Game" }))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            var serialized = new SerializedObject(catalog); var entries = serialized.FindProperty("items");
            foreach (var item in items)
            {
                bool found = false; for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).objectReferenceValue == item) found = true;
                if (!found) { int index = entries.arraySize; entries.arraySize++; entries.GetArrayElementAtIndex(index).objectReferenceValue = item; }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        // Save newly woven timeline fields without changing the network prefab composition.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponent<MeleeAttack>() == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try { PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); ItemDefinitionValidation.ValidateAll();
        result.Log("Saved four weapon archetypes, attack sets, collider-free art prefabs, three new item entries, and migrated existing swords. Active scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
    }
}
