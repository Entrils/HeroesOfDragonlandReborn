using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HeroesOfDragonland.Items.Editor
{
    public sealed class ItemDefinitionValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => ValidateAll();

        [MenuItem("Tools/Heroes of Dragonland/Validate Item Definitions")]
        public static void ValidateAll()
        {
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (!item.TryValidate(out string error)) throw new BuildFailedException(path + ": " + error);
                if (item.Id != guid || !ids.Add(item.Id))
                    throw new BuildFailedException(path + ": Duplicate or mismatched item identity; reimport the asset.");
                count++;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path).TryValidate(out string error))
                    throw new BuildFailedException(path + ": " + error);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponAttackAnimationSet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.LoadAssetAtPath<WeaponAttackAnimationSet>(path).TryValidate(out string error))
                    throw new BuildFailedException(path + ": " + error);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:ItemCatalog"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.LoadAssetAtPath<ItemCatalog>(path).TryValidate(out string error))
                    throw new BuildFailedException(path + ": " + error);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:LootTable"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.LoadAssetAtPath<LootTable>(path).TryValidate(null, out string error))
                    throw new BuildFailedException(path + ": " + error);
            }
            Debug.Log("Item validation passed: " + count + " definitions, all catalogs and loot tables.");
        }
    }
}
