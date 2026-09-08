using System; using UnityEngine; using UnityEditor; using HeroesOfDragonland.Items; using HeroesOfDragonland.Items.Editor; using HeroesOfDragonland.InventorySystem; using HeroesOfDragonland.EquipmentSystem; using HeroesOfDragonland.Combat; using HeroesOfDragonland.Stats;
internal class CommandScript : IRunCommand {
 private void Check(bool ok,string label) {if(!ok)throw new Exception("FAIL: "+label);}
 public void Execute(ExecutionResult result) {
 Check(Application.isPlaying,"Play Mode"); ItemDefinitionValidation.ValidateAll();
 var sourceCatalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
 var sword=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/IronSword.asset");
 var armor=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ScoutArmor.asset");
 var dragon=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/Dragonblade.asset");
 var potion=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ManaPotion.asset");
 var catalog=ScriptableObject.CreateInstance<ItemCatalog>();
 var clones=new ItemDefinition[6];
 var objects=new System.Collections.Generic.List<GameObject>();
 try {
 var cs=new SerializedObject(catalog); var entries=cs.FindProperty("items"); entries.arraySize=sourceCatalog.Items.Count+6;
 for(int i=0;i<sourceCatalog.Items.Count;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=sourceCatalog.Items[i];
 for(int i=0;i<6;i++) {var item=UnityEngine.Object.Instantiate(sword); clones[i]=item; var so=new SerializedObject(item); so.FindProperty("id").stringValue=Guid.NewGuid().ToString("N"); so.FindProperty("equipmentSlot").intValue=i+1; so.FindProperty("maximumStackSize").intValue=2; so.ApplyModifiedPropertiesWithoutUndo(); entries.GetArrayElementAtIndex(sourceCatalog.Items.Count+i).objectReferenceValue=item;}
 cs.ApplyModifiedPropertiesWithoutUndo();
 var actor=new GameObject("Equipment validation actor"); objects.Add(actor); actor.SetActive(false);
 var inv=actor.AddComponent<Inventory>(); var ins=new SerializedObject(inv); ins.FindProperty("catalog").objectReferenceValue=catalog; ins.FindProperty("slotCount").intValue=2; ins.ApplyModifiedPropertiesWithoutUndo();
 actor.AddComponent<CharacterAttributes>(); var eq=actor.AddComponent<Equipment>(); actor.SetActive(true);
 int equipmentEvents=0, inventoryEvents=0; eq.Changed+=(slot,oldId,newId)=>equipmentEvents++; inv.Changed+=snapshot=>inventoryEvents++;
 Check(inv.TryAdd(sword),"seed sword"); Check(eq.TryEquip(0,EquipmentSlot.MainHand,out var reason),"equip sword: "+reason);
 Check(inv.GetAmount(sword.Id)==0&&eq.GetItemId(EquipmentSlot.MainHand)==sword.Id&&equipmentEvents==1,"transfer conservation and event");
 Check(eq.GetModifiedStat(ItemStat.AttackDamage,25)==35,"flat modifier");
 Check(!eq.TryEquip(-1,EquipmentSlot.MainHand,out reason)&&!string.IsNullOrEmpty(reason),"invalid source reason");
 Check(inv.TryAdd(dragon),"seed requirements failure"); var rev=inv.Revision;
 Check(!eq.TryEquip(0,EquipmentSlot.MainHand,out reason)&&reason.Contains("Strength")&&inv.Revision==rev&&equipmentEvents==1,"requirement refuses atomically");
 Check(!eq.TryEquip(0,EquipmentSlot.Head,out reason)&&reason.Contains("cannot"),"wrong equipment slot");
 inv.Clear(); inv.TryAdd(potion); Check(!eq.TryEquip(0,EquipmentSlot.MainHand,out reason),"consumable refusal"); inv.Clear();
 inv.TryAdd(clones[0],2); inv.TryAdd(potion,10); rev=inv.Revision;
 Check(!eq.TryUnequip(EquipmentSlot.MainHand,out reason)&&reason.Contains("room")&&inv.Revision==rev,"full inventory unequip");
 Check(!eq.TryEquip(0,EquipmentSlot.MainHand,out reason)&&inv.GetAmount(clones[0].Id)==2&&eq.GetItemId(EquipmentSlot.MainHand)==sword.Id,"full swap with stacked source rollback");
 inv.TryRemove(clones[0],1); int before=inventoryEvents;
 bool observedAtomic=false; Action<InventorySnapshot> observe=s=>observedAtomic=eq.GetItemId(EquipmentSlot.MainHand)==clones[0].Id&&inv.GetAmount(sword.Id)==1;
 inv.Changed+=observe; Check(eq.TryEquip(0,EquipmentSlot.MainHand,out reason),"full swap uses vacated slot"); inv.Changed-=observe;
 Check(observedAtomic&&inventoryEvents==before+1,"both owners committed before one inventory event");
 inv.Clear(); Check(eq.TryUnequip(EquipmentSlot.MainHand,out reason),"unequip return"); Check(inv.GetAmount(clones[0].Id)==1&&eq.GetModifiedStat(ItemStat.AttackDamage,25)==25,"modifier removed");
 inv.Clear();
 for(int i=0;i<6;i++){inv.TryAdd(clones[i]); Check(eq.TryEquip(0,(EquipmentSlot)(i+1),out reason),"all six slots");}
 Check(eq.GetModifiedStat(ItemStat.AttackDamage,25)==85,"multiple gear bonuses");
 for(int i=0;i<6;i++){Check(eq.TryUnequip((EquipmentSlot)(i+1),out reason),"unequip each slot");inv.Clear();}
 inv.TryAdd(sword); inv.SetAuthority(false); Check(!eq.TryEquip(0,EquipmentSlot.MainHand,out reason)&&reason.Contains("authority"),"replica gate"); inv.SetAuthority(true);
 Check(eq.TryEquip(0,EquipmentSlot.MainHand,out reason),"re-equip for combat");
 var melee=actor.AddComponent<MeleeAttack>(); var ms=new SerializedObject(melee); ms.FindProperty("definition").objectReferenceValue=AssetDatabase.LoadAssetAtPath<MeleeAttackDefinition>("Assets/Game/Data/LightAttack.asset"); ms.ApplyModifiedPropertiesWithoutUndo();
 actor.transform.position=new Vector3(150,10,150);
 var target=GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(target); target.transform.position=actor.transform.position+new Vector3(0,1,1.2f); target.AddComponent<CharacterStats>(); target.AddComponent<DamageReceiver>(); Physics.SyncTransforms();
 Check(melee.TryAttack(Vector3.forward),"attack with equipment"); melee.TickAttack(0.3f); Check(target.GetComponent<CharacterStats>().CurrentHealth==65,"melee applies equipped damage"); melee.enabled=false;
 inv.Clear(); eq.TryUnequip(EquipmentSlot.MainHand,out reason); inv.Clear();
 var attributes=actor.GetComponent<CharacterAttributes>(); var ats=new SerializedObject(attributes); var vals=ats.FindProperty("baseAttributes"); vals.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue=20; ats.ApplyModifiedPropertiesWithoutUndo();
 inv.TryAdd(dragon); Check(eq.TryEquip(0,EquipmentSlot.MainHand,out reason)&&Mathf.Approximately(eq.GetModifiedStat(ItemStat.AttackDamage,25),30),"requirements success and percentage modifier");
 eq.TryUnequip(EquipmentSlot.MainHand,out reason); inv.Clear();
 vals.arraySize=0; ats.ApplyModifiedPropertiesWithoutUndo(); inv.TryAdd(sword); Check(!eq.TryEquip(0,EquipmentSlot.MainHand,out reason)&&reason.Contains("Missing attribute"),"missing attribute fails closed");
 Check(!EditorUtility.scriptCompilationFailed,"compile");
 result.Log("PASS: all six slots, no class restrictions, requirements/reasons, full-inventory rollback and swap, atomic events, item conservation, authority, flat/percent modifiers, real melee damage.");
 } finally {foreach(var obj in objects)UnityEngine.Object.DestroyImmediate(obj); foreach(var item in clones)if(item!=null)UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(catalog);}
 } }
