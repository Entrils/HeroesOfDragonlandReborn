using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Stats;
#endif

namespace HeroesOfDragonland.Presentation
{
    // Opt-in real host/client acceptance. Ordinary sessions never install this component.
    public sealed class CharacterVisualNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(!Environment.GetCommandLineArgs().Contains("-hodrHumanoidCheck"))return;
            var bootstrap=FindFirstObjectByType<MultiplayerBootstrap>();
            if(bootstrap!=null)bootstrap.gameObject.AddComponent<CharacterVisualNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private float began=-1, reportAt;
        private bool sawLocalMotion,sawRemoteMotion,sawRemoteWeapon,sawRemoteDeath,sawRemoteRevive,remoteWasDead,killed,complete;
        private void Awake()=>bootstrap=GetComponent<MultiplayerBootstrap>();
        private void Check(bool value,string label) { if(!value)throw new InvalidOperationException("[HumanoidNetwork] FAIL "+label); }
        private void Update()
        {
            var runner=bootstrap.Runner;
            if(runner==null||!runner.IsRunning||!NetworkMatch.AllowsGameplay(runner))return;
            var actors=FindObjectsByType<NetworkTestPlayer>(FindObjectsSortMode.None).Where(p=>p.Object!=null&&p.Object.IsValid&&p.Runner==runner).ToArray();
            if(actors.Count(p=>!p.IsBot)<2)return;
            if(began<0)began=Time.realtimeSinceStartup;
            float age=Time.realtimeSinceStartup-began;
            if(runner.TryGetPlayerObject(runner.LocalPlayer,out var local))
            {
                float sign=local.GetComponent<NetworkTestPlayer>().Team==HeroesOfDragonland.Teams.TeamId.TeamA?1:-1;
                bootstrap.TestInput=age<7?new Vector2(sign,0):Vector2.zero;
                bootstrap.TestFacing=new Vector2(sign,0); bootstrap.TestAttack=age>6&&age<9;
            }
            foreach(var actor in actors)
            {
                var visual=actor.GetComponent<NetworkCharacterVisual>()?.Visual;
                Check(visual!=null&&visual.Animator.avatar.isValid&&visual.Animator.isHuman&&!visual.Animator.applyRootMotion,"valid separate humanoid");
                Check(Quaternion.Angle(visual.transform.rotation,actor.transform.rotation)<.1f,"visual follows accepted player rotation");
                bool remote=!actor.IsBot&&!actor.HasInputAuthority;
                if(actor.HasInputAuthority&&visual.State.Speed>.25f)sawLocalMotion=true;
                if(remote)
                {
                    if(visual.State.Speed>.25f)sawRemoteMotion=true;
                    if(visual.GetAttachment(CharacterSocket.RightHand)!=null)sawRemoteWeapon=true;
                    if(visual.State.Dead&&visual.Animator.GetCurrentAnimatorStateInfo(CharacterAnimationModel.ActionLayer).IsName("Death")) {sawRemoteDeath=true;remoteWasDead=true;}
                    if(remoteWasDead&&!visual.State.Dead&&age>13&&visual.Action!=CharacterAction.Death)sawRemoteRevive=true;
                }
                if(runner.IsServer&&age<5)
                {
                    var equipment=actor.GetComponent<Equipment>(); var inventory=actor.GetComponent<Inventory>();
                    if(equipment.GetItem(EquipmentSlot.MainHand)==null)
                    {
                        var sword=inventory.Catalog.Items.First(i=>i.Weapon!=null&&i.Rarity==ItemRarity.Common&&i.Weapon.Archetype==WeaponArchetype.Sword);
                        if(inventory.GetAmount(sword.Id)==0)actor.GetComponent<NetworkInventory>().TryAddItem(sword.Id);
                        for(int i=0;i<inventory.SlotCount;i++)if(inventory.GetSlot(i).ItemId==sword.Id){equipment.TryEquip(i,EquipmentSlot.MainHand,out _);break;}
                    }
                }
            }
            if(runner.IsServer&&age>10&&!killed)
            {
                killed=true; foreach(var human in actors.Where(p=>!p.IsBot))human.GetComponent<CharacterStats>().ApplyDamage(10000);
            }
            if(!complete&&sawLocalMotion&&sawRemoteMotion&&sawRemoteWeapon&&sawRemoteDeath&&sawRemoteRevive)
            {
                complete=true;
                Debug.Log("[HumanoidNetwork] PASS local and real remote humanoid locomotion, replicated equipment attachment, death Animator and respawn; server="+runner.IsServer);
            }
            if(Time.realtimeSinceStartup>reportAt)
            {
                reportAt=Time.realtimeSinceStartup+3;
                Debug.Log("[HumanoidNetwork] server="+runner.IsServer+" age="+age.ToString("F1")+" actors="+actors.Length+" localMove="+sawLocalMotion+" remoteMove="+sawRemoteMotion+" remoteWeapon="+sawRemoteWeapon+" remoteDeath="+sawRemoteDeath+" remoteRevive="+sawRemoteRevive+" complete="+complete);
            }
        }
        private void OnDisable()
        {
            if(bootstrap==null)return;
            bootstrap.TestInput=null;bootstrap.TestFacing=null;bootstrap.TestAttack=null;
        }
#endif
    }
}
