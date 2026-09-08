using System; using UnityEngine; using UnityEditor; using HeroesOfDragonland.Items; using HeroesOfDragonland.InventorySystem; using HeroesOfDragonland.World; using HeroesOfDragonland.Interaction;
internal class CommandScript : IRunCommand {
 private void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);}
 public void Execute(ExecutionResult result){
 Check(Application.isPlaying,"Play Mode");
 var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
 var potion=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ManaPotion.asset");
 var sword=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/IronSword.asset");
 var legendary=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/Dragonblade.asset");
 var table=ScriptableObject.CreateInstance<LootTable>();var objects=new System.Collections.Generic.List<GameObject>();
 try{
 var ts=new SerializedObject(table);ts.FindProperty("minimumDrops").intValue=1;ts.FindProperty("maximumDrops").intValue=1;var entries=ts.FindProperty("entries");entries.arraySize=2;
 for(int i=0;i<2;i++){var e=entries.GetArrayElementAtIndex(i);e.FindPropertyRelative("item").objectReferenceValue=i==0?sword:legendary;e.FindPropertyRelative("weight").floatValue=1;e.FindPropertyRelative("minimumQuantity").intValue=1;e.FindPropertyRelative("maximumQuantity").intValue=3;}ts.ApplyModifiedPropertiesWithoutUndo();
 int common=0,rare=0;var random=new System.Random(123);
 for(int i=0;i<10000;i++){Check(table.TryRoll(random,out var rewards,out _)&&rewards.Length==1,"draw count");Check(rewards[0].Amount>=1&&rewards[0].Amount<=3,"quantity range");if(rewards[0].ItemId==sword.Id)common++;else rare++;}
 Check(common>9000&&rare>200&&rare<800,"rarity weights");
 table.TryRoll(new System.Random(42),out var first,out _);table.TryRoll(new System.Random(42),out var second,out _);Check(first[0].ItemId==second[0].ItemId&&first[0].Amount==second[0].Amount,"seed reproducibility");
 entries.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue=0;entries.GetArrayElementAtIndex(1).FindPropertyRelative("weight").floatValue=0;ts.ApplyModifiedPropertiesWithoutUndo();Check(!table.TryRoll(random,out _,out _),"zero weights invalid");
 entries.arraySize=1;var entry=entries.GetArrayElementAtIndex(0);entry.FindPropertyRelative("item").objectReferenceValue=potion;entry.FindPropertyRelative("weight").floatValue=1;entry.FindPropertyRelative("minimumQuantity").intValue=15;entry.FindPropertyRelative("maximumQuantity").intValue=15;ts.ApplyModifiedPropertiesWithoutUndo();
 var actor=new GameObject("Loot validation actor");objects.Add(actor);actor.SetActive(false);actor.transform.position=new Vector3(200,10,200);
 var inv=actor.AddComponent<Inventory>();var ins=new SerializedObject(inv);ins.FindProperty("catalog").objectReferenceValue=catalog;ins.FindProperty("slotCount").intValue=1;ins.ApplyModifiedPropertiesWithoutUndo();var interactor=actor.AddComponent<PlayerInteractor>();actor.SetActive(true);
 var other=new GameObject("Other loot player");objects.Add(other);other.SetActive(false);other.transform.position=actor.transform.position;var otherInv=other.AddComponent<Inventory>();var os=new SerializedObject(otherInv);os.FindProperty("catalog").objectReferenceValue=catalog;os.ApplyModifiedPropertiesWithoutUndo();other.SetActive(true);
 var root=new GameObject("Loot validation chest");objects.Add(root);root.transform.position=actor.transform.position+new Vector3(0,1,2);var chest=root.AddComponent<LootChest>();var cs=new SerializedObject(chest);cs.FindProperty("lootTable").objectReferenceValue=table;cs.ApplyModifiedPropertiesWithoutUndo();Physics.SyncTransforms();
 int events=0;chest.Changed+=()=>events++;
 Check(!chest.IsOpen,"closed default");
 chest.SetAuthority(false);Check(!chest.TryInteract(inv,out _)&&!chest.IsOpen,"chest authority gate");chest.SetAuthority(true);
 inv.SetAuthority(false);Check(!chest.TryInteract(inv,out _)&&!chest.IsOpen,"recipient authority gate");inv.SetAuthority(true);
 actor.transform.position+=Vector3.back*10;Check(!chest.TryInteract(inv,out _)&&!chest.IsOpen,"range gate");actor.transform.position=other.transform.position;
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(wall);wall.transform.position=actor.transform.position+new Vector3(0,1,1);wall.transform.localScale=new Vector3(4,4,0.1f);Physics.SyncTransforms();Check(!chest.TryInteract(inv,out _)&&!chest.IsOpen,"obstruction gate");wall.SetActive(false);Physics.SyncTransforms();
 inv.TryAdd(sword);
 Check(interactor.TryInteract(out _)&&chest.IsOpen&&chest.PendingRewards[0].Amount==15&&events==1,"open full inventory preserves loot");
 long revision=chest.Revision;Check(!chest.TryInteract(inv,out _)&&chest.Revision==revision&&chest.PendingRewards[0].Amount==15,"no reroll on repeat");
 Check(!chest.TryInteract(otherInv,out _)&&otherInv.GetAmount(potion.Id)==0,"claimant ownership");
 inv.Clear();bool consistent=false;Action<InventorySnapshot> observe=s=>consistent=chest.PendingRewards.Count==1&&chest.PendingRewards[0].Amount==5&&chest.Revision==2;inv.Changed+=observe;
 Check(chest.TryInteract(inv,out _)&&inv.GetAmount(potion.Id)==10&&consistent,"partial grant atomic");inv.Changed-=observe;
 inv.Clear();Check(chest.TryInteract(inv,out _)&&inv.GetAmount(potion.Id)==5&&chest.PendingRewards.Count==0&&events==3,"recover exact remainder");
 Check(!chest.TryInteract(inv,out _)&&events==3&&inv.GetAmount(potion.Id)==5,"exhausted chest never grants twice");
 chest.enabled=false;chest.enabled=true;Check(chest.IsOpen&&!chest.TryInteract(inv,out _),"disable preserves open state");
 var sceneTable=AssetDatabase.LoadAssetAtPath<LootTable>("Assets/Game/Data/TestChestLoot.asset");
 for(int i=0;i<100;i++){Check(sceneTable.TryRoll(random,out var rewards,out _)&&rewards.Length>=2&&rewards.Length<=4,"configured scene drop count");}
 result.Log("PASS: 10,000 weighted rarity rolls, quantity/count ranges, deterministic RNG, invalid weights, range/LOS, authority, interactor, full inventory, exact partial recovery, owner reservation, one-use/events.");
 }finally{foreach(var obj in objects)UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(table);}
 } }
