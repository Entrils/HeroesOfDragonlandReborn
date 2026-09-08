using System; using UnityEngine; using UnityEditor; using HeroesOfDragonland.Items; using HeroesOfDragonland.Items.Editor;
internal class CommandScript : IRunCommand {
 [Serializable] internal class Payload { public string itemId; }
 private void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
 public void Execute(ExecutionResult result) {
 ItemDefinitionValidation.ValidateAll();
 var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
 Check(catalog.Items.Count==5,"Sample count");
 var categories=new System.Collections.Generic.HashSet<ItemCategory>(); var rarities=new System.Collections.Generic.HashSet<ItemRarity>();
 foreach(var item in catalog.Items) {
 categories.Add(item.Category); rarities.Add(item.Rarity);
 string json=JsonUtility.ToJson(new Payload {itemId=item.Id});
 var payload=JsonUtility.FromJson<Payload>(json);
 Check(catalog.TryGet(payload.itemId,out var resolved)&&resolved==item,"ID roundtrip");
 string id=item.Id; AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(item),ImportAssetOptions.ForceUpdate); Check(item.Id==id,"ID stable after reimport");
 }
 Check(categories.Count==4&&rarities.Count==5,"Category and rarity coverage");
 Check(!catalog.TryGet("unknown",out _)&&!catalog.TryGet(null,out _),"Unknown IDs rejected");
 var copy=UnityEngine.Object.Instantiate(catalog.Items[0]); var scratch=ScriptableObject.CreateInstance<ItemCatalog>();
 try {
 var cs=new SerializedObject(scratch); var list=cs.FindProperty("items"); list.arraySize=2; list.GetArrayElementAtIndex(0).objectReferenceValue=catalog.Items[0]; list.GetArrayElementAtIndex(1).objectReferenceValue=copy; cs.ApplyModifiedPropertiesWithoutUndo();
 Check(!scratch.TryValidate(out _)&&!scratch.TryGet(copy.Id,out _),"Duplicate IDs rejected");
 var so=new SerializedObject(copy); so.FindProperty("maximumStackSize").intValue=0; so.ApplyModifiedPropertiesWithoutUndo(); Check(!copy.TryValidate(out _),"Zero stack rejected");
 so.FindProperty("maximumStackSize").intValue=1; so.FindProperty("value").intValue=-1; so.ApplyModifiedPropertiesWithoutUndo(); Check(!copy.TryValidate(out _),"Negative value rejected");
 so.FindProperty("value").intValue=1; so.FindProperty("rarity").intValue=99; so.ApplyModifiedPropertiesWithoutUndo(); Check(!copy.TryValidate(out _),"Unknown enum rejected");
 so.FindProperty("rarity").intValue=0; var mods=so.FindProperty("statModifiers"); mods.arraySize=1; mods.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue=float.NaN; so.ApplyModifiedPropertiesWithoutUndo(); Check(!copy.TryValidate(out _),"Nonfinite modifier rejected");
 } finally { UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(scratch); }
 Check(!EditorUtility.scriptCompilationFailed,"Project compilation");
 result.Log("PASS: five items; all categories/rarities; JSON ID roundtrip; reimport identity; unknown/duplicate IDs; invalid stacks, value, enum, and modifiers. Compilation clean.");
 } }
