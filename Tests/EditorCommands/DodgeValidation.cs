using UnityEngine;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Combat;
internal class CommandScript : IRunCommand {
 private void Check(bool ok,string name) { if(!ok) throw new System.Exception("FAIL: "+name); Debug.Log("PASS: "+name); }
 public void Execute(ExecutionResult result) {
 Check(Application.isPlaying,"Play Mode");
 var player=Object.FindFirstObjectByType<PlayerController>();
 player.enabled=false;
 var d=player.GetComponent<PlayerDodge>(); var m=player.GetComponent<PlayerMovement>(); var s=player.GetComponent<CharacterStats>(); var cc=player.GetComponent<CharacterController>(); var hit=player.GetComponent<DamageReceiver>(); var attack=player.GetComponent<MeleeAttack>();
 var original=player.transform.position;
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); result.RegisterObjectCreation(floor); floor.transform.position=new Vector3(100, -0.5f,100); floor.transform.localScale=new Vector3(30,1,30);
 cc.enabled=false; player.transform.position=new Vector3(100,0.1f,100); cc.enabled=true; Physics.SyncTransforms();
 for(int i=0;i<20;i++) m.Tick(Vector3.zero,false,0.02f);
 s.ResetToFull(); var start=player.transform.position;
 Check(d.TryDodge(Vector3.right,Vector3.forward),"grounded directional dodge");
 Check(Mathf.Approximately(s.CurrentStamina,75),"stamina cost once");
 for(int i=0;i<20;i++) Check(!d.TryDodge(Vector3.left),"repeated request rejected");
 Check(Mathf.Approximately(s.CurrentStamina,75),"repeated input no extra cost");
 Check(!attack.TryAttack(),"attack rejected during dodge");
 Check(hit.TakeDamage(1,null)==1,"damage before window");
 d.TickDodge(0.4f * 0.15f,Vector3.forward);
 Check(d.IsInvulnerable && hit.TakeDamage(10,null)==0 && s.ApplyDamage(10)==0,"window start and both damage paths");
 d.TickDodge(0.4f * 0.75f - 0.4f * 0.15f,Vector3.forward);
 Check(!d.IsInvulnerable && hit.TakeDamage(1,null)==1,"window end exclusive");
 d.TickDodge(0.1f,Vector3.forward);
 Check(Mathf.Abs(player.transform.position.x-start.x-4)<0.05f,"configured roll distance");
 Check(Vector3.Dot(player.transform.forward,Vector3.forward)>0.99f,"lock facing preserved during strafe");
 Check(d.IsBusy && !d.TryDodge(Vector3.right),"recovery rejects dodge");
 d.TickDodge(0.31f);
 Check(!d.IsBusy,"recovery completes");
 s.TrySpendStamina(s.CurrentStamina);
 Check(!d.TryDodge(Vector3.right),"insufficient stamina");
 s.ResetToFull();
 Check(d.TryDodge(Vector3.zero,Vector3.forward) && Vector3.Dot(d.Direction,Vector3.back)>0.99f,"lock idle fallback away");
 d.Cancel();
 Check(d.TryDodge(Vector3.zero) && Vector3.Dot(d.Direction,player.transform.forward)>0.99f,"free idle fallback forward");
 d.TickDodge(0.1f); d.enabled=false; Check(!d.IsInvulnerable && !d.IsBusy,"disable cancels protection"); d.enabled=true;
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); result.RegisterObjectCreation(wall); wall.transform.position=player.transform.position+Vector3.right*2+Vector3.up; wall.transform.localScale=new Vector3(0.2f,4,8); Physics.SyncTransforms();
 s.ResetToFull(); start=player.transform.position; Check(d.TryDodge(Vector3.right),"wall dodge starts"); d.TickDodge(0.8f);
 Check(player.transform.position.x < wall.transform.position.x-0.1f && player.transform.position.x>start.x,"collision blocks long-frame roll");
 cc.enabled=false; player.transform.position+=Vector3.up*5; cc.enabled=true; m.Tick(Vector3.zero,false,0.01f); Check(!d.TryDodge(Vector3.right),"air dodge rejected");
 result.DestroyObject(wall); result.DestroyObject(floor);
 cc.enabled=false; player.transform.position=original; cc.enabled=true; s.ResetToFull(); player.enabled=true;
 result.Log("All dodge checks passed");
 } }
