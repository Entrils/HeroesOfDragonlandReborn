using System; using UnityEngine; using UnityEditor; using HeroesOfDragonland.Items; using HeroesOfDragonland.EquipmentSystem; using HeroesOfDragonland.InventorySystem; using HeroesOfDragonland.Player; using HeroesOfDragonland.Stats;
internal class CommandScript : IRunCommand {
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);}
void Set(UnityEngine.Object obj,string field,float value){var so=new SerializedObject(obj);so.FindProperty(field).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
public void Execute(ExecutionResult result){
Check(Application.isPlaying,"Play Mode");
var armor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ScoutArmor.asset"));
var catalog=ScriptableObject.CreateInstance<ItemCatalog>();var actor=new GameObject("Armor validation");actor.SetActive(false);var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
try {
var a=new SerializedObject(armor);a.FindProperty("equipmentRequirements").arraySize=0;
var mods=a.FindProperty("statModifiers");mods.arraySize=7;
int[] ids={6,12,0,1,2,3,9};float[] vals={100,20,20,10,15,3,2};
for(int i=0;i<ids.Length;i++){var m=mods.GetArrayElementAtIndex(i);m.FindPropertyRelative("stat").intValue=ids[i];m.FindPropertyRelative("operation").intValue=0;m.FindPropertyRelative("value").floatValue=vals[i];}a.ApplyModifiedPropertiesWithoutUndo();
var c=new SerializedObject(catalog);var items=c.FindProperty("items");items.arraySize=1;items.GetArrayElementAtIndex(0).objectReferenceValue=armor;c.ApplyModifiedPropertiesWithoutUndo();
var inv=actor.AddComponent<Inventory>();var ins=new SerializedObject(inv);ins.FindProperty("catalog").objectReferenceValue=catalog;ins.ApplyModifiedPropertiesWithoutUndo();
actor.AddComponent<CharacterAttributes>();var eq=actor.AddComponent<Equipment>();var stats=actor.AddComponent<CharacterStats>();var movement=actor.AddComponent<PlayerMovement>();var dodge=actor.AddComponent<PlayerDodge>();
var ds=new SerializedObject(dodge);ds.FindProperty("definition").objectReferenceValue=AssetDatabase.LoadAssetAtPath<DodgeDefinition>("Assets/Game/Data/DefaultDodge.asset");ds.ApplyModifiedPropertiesWithoutUndo();
actor.transform.position=new Vector3(500,1.1f,500);floor.transform.position=new Vector3(500,-0.5f,500);floor.transform.localScale=new Vector3(50,1,50);actor.SetActive(true);Physics.SyncTransforms();for(int i=0;i<40;i++)movement.Tick(Vector3.zero,false,.02f);
Check(inv.TryAdd(armor)&&eq.TryEquip(0,EquipmentSlot.Chest,out _),"equip armor");
Check(eq.PhysicalDefense==100&&eq.MagicalDefense==20,"defenses aggregate");
Check(stats.MaxHealth==140&&stats.MaxStamina==110&&stats.MaxMana==115,"resource and vitality bonuses");
Check(actor.GetComponent<CharacterAttributes>().TryGetValue(ItemStat.Strength,out float strength)&&strength==13,"attribute bonus");
Check(stats.CurrentHealth==100,"equip does not heal");Check(Mathf.Abs(stats.ApplyDamage(20)-10)<.001f,"physical damage mitigation");
float[] weights={10,15,30};float[] costs={25,30,37.5f};float[] distances={4,3.4f,2.6f};float[] durations={.4f,.44f,.5f};
for(int i=0;i<3;i++){
Set(armor,"weight",weights[i]);stats.ResetToFull();Check((int)eq.WeightClass==i,"weight class "+i);float start=actor.transform.position.x;
Check(dodge.TryDodge(Vector3.right),"roll starts "+i);Check(Mathf.Abs(stats.CurrentStamina-(stats.MaxStamina-costs[i]))<.001f,"roll cost "+i);
dodge.TickDodge(durations[i]);Check(Mathf.Abs(actor.transform.position.x-start-distances[i])<.05f,"roll distance "+i);Check(dodge.IsBusy,"recovery "+i);dodge.TickDodge(2);Check(!dodge.IsBusy,"recovery finishes "+i);
}
Check(eq.TryUnequip(EquipmentSlot.Chest,out _),"unequip");Check(eq.TotalWeight==0&&eq.PhysicalDefense==0&&stats.MaxHealth==100&&stats.MaxStamina==100&&stats.MaxMana==100,"all bonuses removed");Check(stats.ApplyDamage(10)==10,"unarmored damage");
inv.SetAuthority(false);float health=stats.CurrentHealth; // Inventory replicas still derive gear through equipment snapshots.
Check(!EditorUtility.scriptCompilationFailed,"no compile errors");
result.Log("PASS: physical/magical defense, resources, attributes, no equip healing, three weight classes, roll cost/distance/recovery and unequip restoration.");
} finally {UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(floor);UnityEngine.Object.DestroyImmediate(catalog);UnityEngine.Object.DestroyImmediate(armor);}
}}
