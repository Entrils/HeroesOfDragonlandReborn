using System;
using UnityEngine;
using UnityEditor;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Combat;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Data/MultiplayerFoundation/NetworkTestPlayer.prefab");
  if(root==null) throw new Exception("Missing player prefab");
  foreach(var type in new[]{typeof(NetworkCharacterStats),typeof(NetworkPlayerResources),typeof(NetworkPlayerCombat),typeof(CharacterStats),typeof(MeleeAttack),typeof(DamageReceiver),typeof(NetworkPlayerDodge),typeof(NetworkLocalTargeting),typeof(NetworkActionPresentation)})
   if(root.GetComponent(type)==null) throw new Exception("Missing "+type.Name);
  if(new SerializedObject(root.GetComponent<CharacterStats>()).FindProperty("configuration").objectReferenceValue==null) throw new Exception("Missing Stats config");
  if(new SerializedObject(root.GetComponent<MeleeAttack>()).FindProperty("definition").objectReferenceValue==null) throw new Exception("Missing attack definition");
  if(new SerializedObject(root.GetComponent<DamageReceiver>()).FindProperty("deactivateOnDeath").boolValue) throw new Exception("Network corpse would deactivate");
  // Resolve the network component by name: the MCP command compiler does not
  // automatically reference Fusion.Runtime for a NetworkBehaviour-to-Component cast.
  foreach(var pair in new[] {
   new { component=root.GetComponent("NetworkPlayerDodge"), field="definition" },
   new { component=(Component)root.GetComponent<NetworkActionPresentation>(), field="body" },
   new { component=(Component)root.GetComponent<NetworkActionPresentation>(), field="swingPivot" }
  })
   if(new SerializedObject(pair.component).FindProperty(pair.field).objectReferenceValue==null)
    throw new Exception("Missing " + pair.field);
  if(!root.GetComponent<Collider>().enabled) throw new Exception("Disabled hit collider");
  result.Log("PASS: combat prefab components, configurations and hit collider; corpse stays replicated");
 }
}
