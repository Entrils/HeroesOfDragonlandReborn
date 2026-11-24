using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>M58 authoring only. Invoke through Unity MCP; no serialized YAML editing.</summary>
    public static class VisibleEquipmentSetup
    {
        public const string Folder = "Assets/Art/Equipment";
        public const string CatalogPath = Folder + "/EquipmentVisualCatalog.asset";
        private static readonly EquipmentSlot[] ArmorSlots = { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Legs };

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Box(Transform parent, Vector3 center, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "ArmorPlate";
            part.transform.SetParent(parent, false);
            part.transform.localPosition = center;
            part.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static GameObject BuildMesh(string name, Material material, params (Vector3 center, Vector3 size)[] boxes)
        {
            string path = Folder + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                foreach (var box in boxes) Box(root.transform, box.center, box.size, material);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static void Pose(SerializedProperty target, Vector3 position, Quaternion rotation)
        {
            target.FindPropertyRelative("position").vector3Value = position;
            target.FindPropertyRelative("eulerAngles").vector3Value = rotation.eulerAngles;
            target.FindPropertyRelative("scale").vector3Value = Vector3.one;
        }
        private static EquipmentVisualDefinition Recipe(string name, EquipmentSlot slot, CharacterVisual rig,
            params (GameObject prefab, HumanBodyBones bone, Vector3 center)[] parts)
        {
            string path = Folder + "/Definitions/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<EquipmentVisualDefinition>(path);
            if (existing != null) return existing; // Preserve reviewed offsets and meshes.
            var definition = ScriptableObject.CreateInstance<EquipmentVisualDefinition>();
            var data = new SerializedObject(definition);
            data.FindProperty("slot").intValue = (int)slot;
            var array = data.FindProperty("parts");
            array.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = array.GetArrayElementAtIndex(i);
                part.FindPropertyRelative("prefab").objectReferenceValue = parts[i].prefab;
                part.FindPropertyRelative("bone").intValue = (int)parts[i].bone;
                part.FindPropertyRelative("socket").intValue = (int)CharacterSocket.RightHand;
                if (parts[i].bone == HumanBodyBones.LastBone)
                    Pose(part.FindPropertyRelative("pose"), Vector3.zero, Quaternion.identity);
                else
                {
                    var bone = rig.Animator.GetBoneTransform(parts[i].bone);
                    if (bone == null) throw new InvalidOperationException("Missing armor bone: " + parts[i].bone);
                    Pose(part.FindPropertyRelative("pose"), bone.InverseTransformPoint(rig.transform.TransformPoint(parts[i].center)),
                        Quaternion.Inverse(bone.rotation) * rig.transform.rotation);
                }
            }
            // The existing Back socket already owns the approved rest orientation.
            Pose(data.FindProperty("stowedPose"), Vector3.zero, Quaternion.identity);
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!definition.TryValidate(out var reason)) throw new InvalidOperationException(name + ": " + reason);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        [MenuItem("Heroes of Dragonland/Characters/Build Visible Equipment (M58)")]
        public static void Build()
        {
            if (Application.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Use compiled Edit Mode through Unity MCP.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAssetBuilder.VisualPath);
            if (source == null) throw new InvalidOperationException("Build the existing Humanoid visual pipeline first.");
            foreach (string directory in new[] { Folder, Folder + "/Prefabs", Folder + "/Definitions", Folder + "/Materials" }) HumanoidAssetBuilder.Folder(directory);
            string materialPath = Folder + "/Materials/M_EquipmentIron.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit unavailable.");
                material = new Material(shader) { name = "M_EquipmentIron", color = new Color(.43f, .47f, .5f) };
                material.SetFloat("_Metallic", .65f); material.SetFloat("_Smoothness", .3f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var helmet = BuildMesh("PV_ArmorHead", material,
                (new Vector3(0,.15f,0),new Vector3(.28f,.05f,.26f)),
                (new Vector3(-.13f,.04f,0),new Vector3(.035f,.2f,.25f)),
                (new Vector3(.13f,.04f,0),new Vector3(.035f,.2f,.25f)),
                (new Vector3(0,.04f,-.115f),new Vector3(.24f,.2f,.035f)));
            // Open breastplate frame keeps the existing center team tint visible.
            var chest = BuildMesh("PV_ArmorChest", material,
                (new Vector3(-.14f,0,.15f),new Vector3(.08f,.34f,.06f)),
                (new Vector3(.14f,0,.15f),new Vector3(.08f,.34f,.06f)),
                (new Vector3(0,.13f,.15f),new Vector3(.28f,.06f,.06f)),
                (new Vector3(0,0,-.14f),new Vector3(.08f,.3f,.04f)));
            var glove = BuildMesh("PV_ArmorHand", material,(Vector3.zero,new Vector3(.14f,.13f,.13f)));
            var thigh = BuildMesh("PV_ArmorThigh", material,(Vector3.zero,new Vector3(.19f,.32f,.19f)));
            var shin = BuildMesh("PV_ArmorShin", material,(Vector3.zero,new Vector3(.15f,.3f,.15f)));
            var rigRoot = PrefabUtility.LoadPrefabContents(HumanoidAssetBuilder.VisualPath);
            try
            {
                var rig = rigRoot.GetComponent<CharacterVisual>();
                var defaults = new[] {
                    Recipe("EV_Head",EquipmentSlot.Head,rig,(helmet,HumanBodyBones.Head,new Vector3(0,1.685f,0))),
                    Recipe("EV_Chest",EquipmentSlot.Chest,rig,(chest,HumanBodyBones.Chest,new Vector3(0,1.28f,0))),
                    Recipe("EV_Hands",EquipmentSlot.Hands,rig,
                        (glove,HumanBodyBones.LeftHand,new Vector3(-.84f,1.42f,0)),(glove,HumanBodyBones.RightHand,new Vector3(.84f,1.42f,0))),
                    Recipe("EV_Legs",EquipmentSlot.Legs,rig,
                        (thigh,HumanBodyBones.LeftUpperLeg,new Vector3(-.11f,.73f,0)),(thigh,HumanBodyBones.RightUpperLeg,new Vector3(.11f,.73f,0)),
                        (shin,HumanBodyBones.LeftLowerLeg,new Vector3(-.11f,.325f,0)),(shin,HumanBodyBones.RightLowerLeg,new Vector3(.11f,.325f,0))) };
                var weapons = new List<EquipmentVisualDefinition>();
                foreach (WeaponArchetype archetype in new[] { WeaponArchetype.Sword, WeaponArchetype.Greatsword, WeaponArchetype.Spear, WeaponArchetype.Staff })
                    weapons.Add(Recipe("EV_" + archetype,EquipmentSlot.MainHand,rig,(null,HumanBodyBones.LastBone,Vector3.zero)));
                var catalog = AssetDatabase.LoadAssetAtPath<EquipmentVisualCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<EquipmentVisualCatalog>();
                    var data = new SerializedObject(catalog);
                    var armor = data.FindProperty("armorDefaults"); armor.arraySize = defaults.Length;
                    for (int i = 0; i < defaults.Length; i++) armor.GetArrayElementAtIndex(i).objectReferenceValue = defaults[i];
                    var bindings = data.FindProperty("weapons"); bindings.arraySize = weapons.Count;
                    for (int i = 0; i < weapons.Count; i++)
                    {
                        bindings.GetArrayElementAtIndex(i).FindPropertyRelative("archetype").intValue = i;
                        bindings.GetArrayElementAtIndex(i).FindPropertyRelative("visual").objectReferenceValue = weapons[i];
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                if (!catalog.TryValidate(out var error)) throw new InvalidOperationException(error);
                int installed = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab.GetComponent<NetworkTestPlayer>() == null || prefab.GetComponentInChildren<CharacterVisual>(true) == null) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var view = root.GetComponent<CharacterEquipmentVisual>();
                        if (view == null) view = root.AddComponent<CharacterEquipmentVisual>();
                        Set(view, "visual", root.GetComponentInChildren<CharacterVisual>(true));
                        Set(view, "visualCatalog", catalog);
                        if (root.GetComponent<VisibleEquipmentNetworkProbe>() == null) root.AddComponent<VisibleEquipmentNetworkProbe>();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        installed++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                if (installed == 0) throw new InvalidOperationException("No humanoid player prefabs found for equipment setup.");
                AssetDatabase.SaveAssets();
                Debug.Log($"[M58] Installed shared equipment visual catalog on {installed} player prefabs. No item, stat, inventory or network definition changed. Run Play validation and real remote/reconnect checks.");
                Validate();
            }
            finally { PrefabUtility.UnloadPrefabContents(rigRoot); }
        }

        [MenuItem("Heroes of Dragonland/Characters/Validate Visible Equipment Assets (M58)")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EquipmentVisualCatalog>(CatalogPath);
            if (catalog == null || !catalog.TryValidate(out _)) throw new InvalidOperationException("Build valid M58 visual assets first.");
            var data = new SerializedObject(catalog);
            if (data.FindProperty("weapons").arraySize != 4 || data.FindProperty("armorDefaults").arraySize != 4)
                throw new InvalidOperationException("M58 needs four weapon families and all four armor slot defaults.");
            foreach (var slot in ArmorSlots)
                if (AssetDatabase.LoadAssetAtPath<EquipmentVisualDefinition>(Folder + "/Definitions/EV_" + slot + ".asset") == null)
                    throw new InvalidOperationException("Missing armor recipe: " + slot);
            foreach (WeaponArchetype archetype in new[] { WeaponArchetype.Sword, WeaponArchetype.Greatsword, WeaponArchetype.Spear, WeaponArchetype.Staff })
                if (AssetDatabase.LoadAssetAtPath<EquipmentVisualDefinition>(Folder + "/Definitions/EV_" + archetype + ".asset") == null)
                    throw new InvalidOperationException("Missing weapon recipe: " + archetype);
            int players = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Data" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.GetComponent<NetworkTestPlayer>() == null || prefab.GetComponentInChildren<CharacterVisual>(true) == null) continue;
                var assigned = prefab.GetComponent<CharacterEquipmentVisual>()?.Catalog;
                if (assigned == null || !assigned.TryValidate(out _))
                    throw new InvalidOperationException("Player visual catalog is missing or invalid: " + prefab.name);
                players++;
            }
            if (players == 0) throw new InvalidOperationException("No humanoid player prefabs validated.");
            Debug.Log("[M58] PASS visual recipes/catalog references. Play, host/client and reconnect tests are separate.");
        }
    }
}
