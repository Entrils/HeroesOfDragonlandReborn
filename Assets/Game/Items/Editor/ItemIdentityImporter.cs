using UnityEditor;

namespace HeroesOfDragonland.Items.Editor
{
    // Runs only in the editor. Builds carry the baked string ID, not AssetDatabase.
    // Duplicating an asset gets a new GUID; renaming/moving preserves identity.
    public sealed class ItemIdentityImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (string path in imported) AssignIdentity(path);
            foreach (string path in moved) AssignIdentity(path);
        }

        private static void AssignIdentity(string path)
        {
            if (!path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) return;
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null) return;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (item.Id == guid) return;
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = guid;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(item);
        }
    }
}
