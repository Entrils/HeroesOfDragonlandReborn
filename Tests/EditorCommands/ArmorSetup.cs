using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; using HeroesOfDragonland.EquipmentSystem; using HeroesOfDragonland.Items;
internal class CommandScript : IRunCommand {
public void Execute(ExecutionResult result) {
if(Application.isPlaying) throw new System.Exception("Edit Mode required");
AssetDatabase.Refresh();
const string path="Assets/Game/Data/DefaultArmorRules.asset";
var rules=AssetDatabase.LoadAssetAtPath<ArmorRules>(path);
if(rules==null){rules=ScriptableObject.CreateInstance<ArmorRules>(); AssetDatabase.CreateAsset(rules,path);}
int count=0;
foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game"})) {
var p=AssetDatabase.GUIDToAssetPath(guid); var root=PrefabUtility.LoadPrefabContents(p);
try {bool changed=false; foreach(var eq in root.GetComponentsInChildren<Equipment>(true)){var so=new SerializedObject(eq); so.FindProperty("armorRules").objectReferenceValue=rules; so.ApplyModifiedPropertiesWithoutUndo();changed=true; count++;} if(changed)PrefabUtility.SaveAsPrefabAsset(root,p);}
finally {PrefabUtility.UnloadPrefabContents(root);}}
foreach(var eq in Object.FindObjectsByType<Equipment>(FindObjectsInactive.Include,FindObjectsSortMode.None)){var so=new SerializedObject(eq);so.FindProperty("armorRules").objectReferenceValue=rules;so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(eq.gameObject.scene);}
var scout=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ScoutArmor.asset");var item=new SerializedObject(scout);item.FindProperty("weight").floatValue=10;item.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes();
if(rules.Classify(14.99f)!=EquipmentWeightClass.Light || rules.Classify(15)!=EquipmentWeightClass.Medium || rules.Classify(30)!=EquipmentWeightClass.Heavy)throw new System.Exception("Weight boundaries");
if(Mathf.Abs(rules.ReducePhysicalDamage(100,100)-50)>0.001f)throw new System.Exception("Defense formula");
result.Log("PASS: armor rules created, prefab bindings="+count+", active scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+", weight boundaries and physical defense formula verified. Compilation failed="+EditorUtility.scriptCompilationFailed);
}}
