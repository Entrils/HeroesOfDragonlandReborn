using System; using System.Collections; using UnityEngine; using UnityEngine.AI; using UnityEditor; using HeroesOfDragonland.AI; using HeroesOfDragonland.Player; using HeroesOfDragonland.Stats; using HeroesOfDragonland.Combat; using HeroesOfDragonland.Targeting;
internal class CommandScript : IRunCommand {
 private bool previousRunInBackground;
 private void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);Debug.Log("Enemy PASS: "+label);}
 private void Position(PlayerController p,Vector3 position){var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=position;cc.enabled=true;Physics.SyncTransforms();}
 public void Execute(ExecutionResult result){
 if(!Application.isPlaying)throw new Exception("Run in Play Mode");
 previousRunInBackground=Application.runInBackground;Application.runInBackground=true;
 var sceneEnemy=UnityEngine.Object.FindFirstObjectByType<EnemyBrain>();sceneEnemy.SetAuthority(false);
 var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/AI/Prefabs/TestBandit.prefab");
 Check(NavMesh.SamplePosition(new Vector3(25,0,25),out var home,2,NavMesh.AllAreas),"test area on NavMesh");
 var enemy=UnityEngine.Object.Instantiate(prefab,home.position,Quaternion.identity);enemy.name="Enemy Validation";result.RegisterObjectCreation(enemy);
 var brain=enemy.GetComponent<EnemyBrain>();SessionState.SetString("EnemyValidation","RUNNING");
 brain.StartCoroutine(Run(brain,player));
 result.Log("Enemy validation started; coroutine tests real NavMesh movement.");
 }
 private IEnumerator Run(EnemyBrain brain,PlayerController player){
 var agent=brain.GetComponent<NavMeshAgent>();var nav=brain.GetComponent<EnemyNavigation>();var attack=brain.GetComponent<EnemyMeleeAttack>();var stats=brain.GetComponent<CharacterStats>();var receiver=brain.GetComponent<DamageReceiver>();var target=player.GetComponent<EnemyThreatTarget>();var ps=player.GetComponent<CharacterStats>();var home=brain.SpawnPosition;
 try{
 Check(stats.MaxHealth==85&&agent.isOnNavMesh&&brain.State==EnemyState.Idle,"profile health, NavMesh, initial Idle");
 Position(player,home+Vector3.back*20);brain.Tick(2.1f);Check(brain.State==EnemyState.Patrol,"Idle to Patrol");
 nav.Stop();agent.Warp(home);Position(player,home+Vector3.forward*6);brain.Tick(0.1f);Check(brain.State==EnemyState.Alert&&brain.AggroTarget==target,"detection and Alert");
 brain.Tick(0.5f);Check(brain.State==EnemyState.Chase,"Alert to Chase");
 var before=brain.transform.position;float deadline=Time.time+6;
 while(brain.State!=EnemyState.Attack&&Time.time<deadline)yield return null;
 Check(brain.State==EnemyState.Attack&&Vector3.Distance(before,brain.transform.position)>2,"real NavMesh chase reaches Attack");
 Position(player,home+Vector3.forward*20);yield return null;yield return null;
 Check(brain.State==EnemyState.Return&&brain.AggroTarget==null,"leash cancels aggro and returns");
 deadline=Time.time+6;
 while(brain.State==EnemyState.Return&&Time.time<deadline)yield return null;
 Check(brain.State==EnemyState.Idle&&Vector3.Distance(brain.transform.position,home)<0.6f,"real return reaches spawn");
 brain.SetAuthority(false);agent.Warp(home);Position(player,home+Vector3.forward*1.1f);ps.ResetToFull();attack.Cancel();attack.Tick(10);
 Check(attack.TryStart(target),"attack start");attack.Tick(0.2f);Check(ps.CurrentHealth==100,"windup telegraph");attack.Tick(0.21f);Check(ps.CurrentHealth==88,"IDamageable hit damage");Check(!attack.TryStart(target),"cooldown prevents spam");attack.Tick(0.1f);Check(ps.CurrentHealth==88,"one hit per attack");
 attack.Tick(2);Check(attack.TryStart(target),"cooldown expires");Position(player,home+Vector3.forward*4);attack.Tick(0.5f);Check(ps.CurrentHealth==88,"moving out of range avoids hit");
 Position(player,home+Vector3.forward*1.1f);attack.Tick(2);
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Enemy Validation Wall";wall.transform.position=home+new Vector3(0,1,0.55f);wall.transform.localScale=new Vector3(3,3,0.1f);Physics.SyncTransforms();Check(!attack.TryStart(target),"wall blocks attack");UnityEngine.Object.DestroyImmediate(wall);
 ps.ResetToFull();var movement=player.GetComponent<PlayerMovement>();for(int i=0;i<20;i++)movement.Tick(Vector3.zero,false,0.02f);
 Check(attack.TryStart(target),"start hit before dodge");var dodge=player.GetComponent<PlayerDodge>();Check(dodge.TryDodge(Vector3.right),"dodge starts");dodge.TickDodge(0.07f);attack.Tick(0.41f);Check(ps.CurrentHealth==100&&dodge.IsInvulnerable,"enemy hit respects dodge invulnerability");dodge.Cancel();
 agent.Warp(home+Vector3.right*2);Position(player,home+Vector3.forward*3);brain.SetAuthority(true);brain.Tick(0.1f);brain.Tick(0.5f);
 ps.ApplyDamage(1000);brain.Tick(0.1f);Check(brain.State==EnemyState.Return&&brain.AggroTarget==null,"dead target released");ps.ResetToFull();Position(player,home+Vector3.back*20);agent.Warp(home);brain.Tick(0.1f);
 Check(brain.State==EnemyState.Idle,"return resets state");receiver.TakeDamage(1,player.gameObject);Check(brain.AggroTarget==null,"out of leash retaliation refused");
 Position(player,home+Vector3.forward*3);receiver.TakeDamage(1,player.gameObject);Check(brain.State==EnemyState.Alert&&brain.AggroTarget==target,"damage retaliation");
 brain.SetAuthority(false);var lockOn=player.GetComponent<TargetLock>();lockOn.Unlock();Camera.main.transform.SetPositionAndRotation(home+new Vector3(0,2,-5),Quaternion.LookRotation(home+Vector3.up-(home+new Vector3(0,2,-5))));lockOn.Toggle();Check(lockOn.CurrentTarget==brain.GetComponent<Targetable>(),"existing lock-on acquires enemy");
 receiver.TakeDamage(1000,player.gameObject);lockOn.Tick(false);
 Check(brain.State==EnemyState.Dead&&!agent.enabled&&!brain.GetComponent<Targetable>().IsValid&&lockOn.CurrentTarget==null,"death stops navigation and clears lock-on");
 Check(!brain.GetComponent<Collider>().enabled&&!attack.TryStart(target),"dead enemy cannot block or attack");
 SessionState.SetString("EnemyValidation","PASS");Debug.Log("ENEMY VALIDATION PASSED");
 }finally{Application.runInBackground=previousRunInBackground;if(SessionState.GetString("EnemyValidation","")!="PASS")SessionState.SetString("EnemyValidation","FAILED (see Console)");UnityEngine.Object.Destroy(brain.gameObject);}
 } }
