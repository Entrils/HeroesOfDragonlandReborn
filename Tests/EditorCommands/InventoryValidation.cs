using System; using UnityEngine; using UnityEditor; using HeroesOfDragonland.Items; using HeroesOfDragonland.InventorySystem;
internal class CommandScript : IRunCommand {
 private void Check(bool ok,string label) { if(!ok) throw new Exception("FAIL: "+label); }
 public void Execute(ExecutionResult result) {
 var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Data/Items/TestItemCatalog.asset");
 string potion=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/ManaPotion.asset").Id;
 string sword=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Data/Items/IronSword.asset").Id;
 var a=new InventoryState(catalog,2); var b=new InventoryState(catalog,2); int events=0;
 a.Changed+= snapshot=> { events++; Check(snapshot.Revision==a.Revision,"event after commit"); };
 Check(a.TryAdd(potion,17)&&a.GetSlot(0).Amount==10&&a.GetSlot(1).Amount==7,"stack split");
 var old=a.CaptureSnapshot();
 Check(!a.HasEmptySlot&&!a.IsFull&&a.CanAdd(potion,3)&&!a.CanAdd(sword),"item-specific capacity");
 Check(!a.TryAdd(potion,4)&&!a.TryRemove(potion,18)&&!a.TryAdd("unknown",1)&&!a.TryAdd(potion,0)&&!a.TryRemove(potion,-1),"reject invalid operations");
 Check(events==1&&a.GetAmount(potion)==17&&a.Revision==1,"failures atomic and silent");
 Check(a.TryAdd(potion,3)&&a.IsFull,"full stacks");
 Check(a.TryRemove(potion,11)&&a.GetAmount(potion)==9&&a.GetSlot(0).IsEmpty,"remove across stacks");
 Check(a.TryAdd(potion,7)&&a.GetSlot(1).Amount==10&&a.GetSlot(0).Amount==6,"fill existing stack first");
 Check(old.Slots[1].Amount==7&&b.GetAmount(potion)==0,"snapshot isolation and per-owner state");
 var json=JsonUtility.ToJson(a.CaptureSnapshot()); var incoming=JsonUtility.FromJson<InventorySnapshot>(json); int replicaEvents=0; b.Changed+=s=>replicaEvents++;
 Check(b.TryApplySnapshot(incoming)&&b.GetAmount(potion)==16&&replicaEvents==1,"JSON replication");
 Check(!b.TryApplySnapshot(incoming)&&!b.TryApplySnapshot(old),"stale/duplicate rejection");
 Check(!b.TryApplySnapshot(new InventorySnapshot(100,new[]{new InventorySlot(potion,11),default(InventorySlot)})),"oversized stack rejected");
 Check(!b.TryApplySnapshot(new InventorySnapshot(100,new[]{new InventorySlot("missing",1),default(InventorySlot)})),"unknown snapshot ID rejected");
 Check(!b.TryApplySnapshot(new InventorySnapshot(100,new[]{new InventorySlot(potion,0),default(InventorySlot)})),"malformed empty rejected");
 Check(!b.TryApplySnapshot(new InventorySnapshot(100,new InventorySlot[1])),"slot count mismatch rejected");
 Check(b.GetAmount(potion)==16&&replicaEvents==1,"invalid snapshots atomic");
 Check(a.Clear()&&!a.Clear()&&events==5,"clear and no-op events");
 Check(a.TryAdd(sword,2)&&a.IsFull&&!a.TryAdd(sword,1),"unstackable equipment");
 bool badSlots=false; try {new InventoryState(catalog,0);} catch(ArgumentOutOfRangeException) {badSlots=true;} Check(badSlots,"invalid capacity");
 var random=new System.Random(7); var test=new InventoryState(catalog,3); long expected=0;
 for(int n=0;n<300;n++) {int amount=random.Next(1,35); bool add=random.Next(2)==0; bool should=add?expected+amount<=30:expected>=amount; bool actual=add?test.TryAdd(potion,amount):test.TryRemove(potion,amount); Check(actual==should,"random operation result"); if(actual)expected+=add?amount:-amount; Check(test.GetAmount(potion)==expected,"random quantity conservation"); for(int i=0;i<3;i++)Check(test.GetSlot(i).Amount>=0&&test.GetSlot(i).Amount<=10,"stack invariant");}
 var indexed=new InventoryState(catalog,2); indexed.TryAdd(potion,17); long indexedRevision=indexed.Revision;
 Check(!indexed.TryRemoveAt(-1,1)&&!indexed.TryRemoveAt(2,1)&&!indexed.TryRemoveAt(0,0)&&!indexed.TryRemoveAt(1,8)
     &&indexed.Revision==indexedRevision,"indexed remove rejects invalid input atomically");
 Check(indexed.TryRemoveAt(1,3)&&indexed.GetSlot(0).Amount==10&&indexed.GetSlot(1).Amount==4,"indexed remove preserves other stacks");
 Check(indexed.TryRemoveAt(1,4)&&indexed.GetSlot(1).IsEmpty,"indexed remove clears depleted slot");
 Check(!EditorUtility.scriptCompilationFailed,"compilation");
 result.Log("PASS: stacking, capacity/full, atomic add/remove, events, clear, isolation, ID JSON snapshots, stale/invalid rejection, unstackable equipment, 300 randomized quantity checks.");
 } }
