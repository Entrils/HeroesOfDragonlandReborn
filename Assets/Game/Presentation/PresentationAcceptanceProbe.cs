using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Fusion;
using HeroesOfDragonland.Audio;
using HeroesOfDragonland.EquipmentSystem;
using HeroesOfDragonland.InventorySystem;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.UI.Minimap;
#endif

namespace HeroesOfDragonland.Presentation
{
    /// <summary>Opt-in development acceptance controls; never active in normal play.</summary>
    [DisallowMultipleComponent]
    public sealed class PresentationAcceptanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string control, last;
        private MultiplayerBootstrap bootstrap;
        private GameAudioService audioService;
        private readonly int[] cues = new int[Enum.GetValues(typeof(AudioCue)).Length];
        private float nextSample;
        private int frames;
        private double totalFrameTime;
        private float worstFrame;
        private readonly FrameTiming[] frameTiming=new FrameTiming[1];
        private readonly List<double> cpuMs=new(),gpuMs=new();
        private void Awake()
        {
            bootstrap=GetComponent<MultiplayerBootstrap>();audioService=GetComponent<GameAudioService>();
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-hodrPresentationControl")control=args[i+1];
            if(string.IsNullOrEmpty(control)){enabled=false;return;}
            Application.runInBackground=true;Application.targetFrameRate=60;
            if(audioService!=null)audioService.CuePlayed+=Played;
        }
        private void Played(AudioCue cue) => cues[(int)cue]++;
        private void OnDestroy(){if(audioService!=null)audioService.CuePlayed-=Played;}
        private NetworkObject Local()
        {
            var runner=bootstrap.Runner;
            if(runner==null||!runner.IsRunning||!runner.TryGetPlayerObject(runner.LocalPlayer,out var actor))throw new InvalidOperationException("Local actor unavailable");
            return actor;
        }
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        private void Update()
        {
            frames++;totalFrameTime+=Time.unscaledDeltaTime;worstFrame=Mathf.Max(worstFrame,Time.unscaledDeltaTime);
            FrameTimingManager.CaptureFrameTimings();
            if(FrameTimingManager.GetLatestTimings(1,frameTiming)>0&&cpuMs.Count<3600)
            {if(frameTiming[0].cpuFrameTime>0)cpuMs.Add(frameTiming[0].cpuFrameTime);if(frameTiming[0].gpuFrameTime>0)gpuMs.Add(frameTiming[0].gpuFrameTime);}
            if(Time.unscaledTime<nextSample)return;nextSample=Time.unscaledTime+.2f;
            if(!File.Exists(control))return;
            string command;
            try{command=File.ReadAllText(control).Trim();}catch(IOException){return;}
            if(string.IsNullOrEmpty(command)||command==last)return;last=command;
            string action=command.Split(' ').Last();
            try{Execute(action);Debug.Log("[PresentationCommand] DONE "+command);}
            catch(Exception error){Debug.LogError("[PresentationCheck] FAIL "+command+" "+error);}
        }
        private void Execute(string action)
        {
            var runner=bootstrap.Runner;
            if(action=="inspect"){Inspect();return;}
            if(action=="screenshot"){ScreenCapture.CaptureScreenshot(control+".png");return;}
            if(action=="restore-settings")
            {var saved=File.ReadAllText(control+".prefs").Split(',');var preferences=GetComponent<AudioPreferences>();for(int i=0;i<3;i++)preferences.Set(i,float.Parse(saved[i],CultureInfo.InvariantCulture));preferences.Save();return;}
            if(action=="check-settings")
            {var preferences=GetComponent<AudioPreferences>();Check(Mathf.Abs(preferences.Get(0)-.73f)<.001f&&Mathf.Abs(preferences.Get(1)-.41f)<.001f&&Mathf.Abs(preferences.Get(2)-.62f)<.001f,"Preferences did not persist");return;}
            if(action=="seed")
            {
                Check(runner!=null&&runner.IsServer,"Only server may seed existing test items");
                foreach(var actor in bootstrap.Avatars.Where(a=>a!=null&&a.IsValid&&a.InputAuthority!=PlayerRef.None))
                {
                    var inventory=actor.GetComponent<Inventory>();
                    var items=inventory.Catalog.Items.Where(i=>i!=null&&i.EquipmentSlot==EquipmentSlot.Chest).Take(1)
                        .Concat(inventory.Catalog.Items.Where(i=>i!=null&&i.Weapon!=null).GroupBy(i=>i.Weapon.Archetype).Select(g=>g.OrderBy(i=>i.EquipmentRequirements.Sum(r=>r.MinimumValue)).First()));
                    foreach(var item in items)if(inventory.GetAmount(item.Id)==0)Check(actor.GetComponent<NetworkInventory>().TryAddItem(item.Id),"Seed failed "+item.DisplayName);
                }
                return;
            }
            var local=Local();var network=local.GetComponent<NetworkEquipment>();
            if(action.StartsWith("equip:"))
            {
                string family=action.Substring(6);var inventory=local.GetComponent<Inventory>();
                for(int i=0;i<inventory.SlotCount;i++)
                {
                    if(!inventory.TryResolveItem(inventory.GetSlot(i).ItemId,out var item))continue;
                    if(family=="Chest"?item.EquipmentSlot!=EquipmentSlot.Chest:item.Weapon==null||item.Weapon.Archetype.ToString()!=family)continue;
                    network.RequestResolved-=Resolved;network.RequestResolved+=Resolved;
                    Check(network.RequestEquip(i,item.EquipmentSlot),"Equip request not sent");return;
                }
                throw new InvalidOperationException("No existing inventory item for "+family);
            }
            if(action=="unequip"||action=="unequip-chest")
            {network.RequestResolved-=Resolved;network.RequestResolved+=Resolved;Check(network.RequestUnequip(action=="unequip"?EquipmentSlot.MainHand:EquipmentSlot.Chest),"Unequip not sent");return;}
            if(action=="stale")
            {network.RequestResolved-=Resolved;network.RequestResolved+=Resolved;network.RequestStaleForValidation(0,EquipmentSlot.MainHand,-99,-99);return;}
            if(action=="reobserve")
            {
                foreach(var subject in TacticalMapSubject.Active.Where(s=>s!=null&&s.Valid&&s.Player!=null&&s.Actor.Runner==runner))
                {var view=subject.Actor.GetComponent<CharacterEquipmentVisual>();view.enabled=false;view.enabled=true;}
                return;
            }
            if(action=="attack"||action=="idle"){bootstrap.TestAttack=action=="attack";return;}
            if(action=="die")
            {Check(runner.IsServer,"Only authority may apply test damage");foreach(var actor in bootstrap.Avatars.Where(a=>a.InputAuthority!=PlayerRef.None))actor.GetComponent<HeroesOfDragonland.Stats.CharacterStats>().ApplyDamage(999999);return;}
            if(action=="king-hit"||action=="king-death")
            {
                Check(runner.IsServer,"Only authority may apply objective test damage");
                var match=NetworkMatchPacing.MatchFor(runner);var team=local.GetComponent<NetworkTestPlayer>().Team;
                var id=team==HeroesOfDragonland.Teams.TeamId.TeamA?match.TeamAKingId:match.TeamBKingId;
                Check(runner.TryFindObject(id,out var king),"Published defending King missing");
                king.GetComponent<NetworkKingObjective>().Stats.ApplyDamage(action=="king-hit"?10:999999);return;
            }
            if(action=="settings")
            {var preferences=GetComponent<AudioPreferences>();File.WriteAllText(control+".prefs",string.Join(",",Enumerable.Range(0,3).Select(i=>preferences.Get(i).ToString(CultureInfo.InvariantCulture))));preferences.Set(0,.73f);preferences.Set(1,.41f);preferences.Set(2,.62f);preferences.Save();return;}
            throw new InvalidOperationException("Unknown acceptance action "+action);
        }
        private void Resolved(bool accepted,string reason)=>Debug.Log("[PresentationEquip] accepted="+accepted+" reason="+reason);
        private void Inspect()
        {
            var runner=bootstrap.Runner;var source=GetComponent<TacticalMapSource>();source.Refresh();
            bool localPresent=runner!=null&&runner.IsRunning&&runner.TryGetPlayerObject(runner.LocalPlayer,out _);
            if(!localPresent){Check(!source.Visible&&source.Markers.Count==0,"Offline map leaked markers");Debug.Log("[PresentationCheck] PASS offline fail closed");return;}
            var local=Local();var team=local.GetComponent<NetworkTestPlayer>().Team;
            foreach(var actor in TacticalMapSubject.Active.Where(s=>s!=null&&s.Valid&&s.Player!=null&&s.Actor.Runner==runner).Select(s=>s.Actor).Distinct())
            {
                var player=actor.GetComponent<NetworkTestPlayer>();var equipment=actor.GetComponent<Equipment>();
                var view=actor.GetComponent<CharacterEquipmentVisual>();
                Check(view!=null,"Missing equipment view");
                for(int i=1;i<=6;i++)
                {
                    var item=equipment.GetItem((EquipmentSlot)i);var recipe=view.Catalog.Resolve(item);
                    int expected=item==null?0:i<=2?1:recipe!=null?recipe.Parts.Count:-1;
                    Check(view.VisualCount((EquipmentSlot)i)==expected,"Equipment visual count differs on "+actor.Id);
                }
                if(player.Team!=team)Check(!source.Markers.Any(m=>m.Id=="ally:"+actor.Id),"Opponent on tactical map");
                Debug.Log("[PresentationActor] id="+actor.Id+" team="+player.Team+" local="+actor.HasInputAuthority+" suspended="+player.IsSuspended+" dead="+actor.GetComponent<NetworkCharacterStats>().Dead+" main="+equipment.GetItemId(EquipmentSlot.MainHand)+" chest="+equipment.GetItemId(EquipmentSlot.Chest));
            }
            Check(source.Markers.All(m=>m.Kind!=MapMarkerKind.RevealedEnemy),"Unexpected reveal producer");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled)==1,"Listener count");
            Check(audioService.GetComponentsInChildren<AudioSource>(true).Length==audioService.Capacity+3,"Audio source growth");
            Debug.Log("[PresentationCheck] PASS local team="+team+" mapVisible="+source.Visible+" markers="+source.Markers.Count+" compass="+source.Heading+" music="+GetComponent<DynamicMusicDirector>().State+" voices="+audioService.ActiveVoices+" sources="+audioService.GetComponentsInChildren<AudioSource>(true).Length+" pool="+GetComponent<VfxPool>().name);
            Debug.Log("[PresentationAudio] "+string.Join(",",Enum.GetValues(typeof(AudioCue)).Cast<AudioCue>().Where(c=>cues[(int)c]>0).Select(c=>c+"="+cues[(int)c])));
            Debug.Log("[PresentationPublic] kingCleared="+source.Markers.Count(m=>m.Kind==MapMarkerKind.King&&m.State==MapMarkerState.Cleared)
                +" campCleared="+source.Markers.Count(m=>(m.Kind==MapMarkerKind.Camp||m.Kind==MapMarkerKind.BossCamp)&&m.State==MapMarkerState.Cleared)
                +" gateCleared="+source.Markers.Count(m=>m.Kind==MapMarkerKind.Objective&&m.State==MapMarkerState.Cleared));
            Debug.Log("[PresentationTiming] frames="+frames+" meanMs="+(totalFrameTime/Math.Max(1,frames)*1000).ToString("F2")+" worstMs="+(worstFrame*1000).ToString("F2")+" note=wall-frame-time-including-cap-and-startup-not-GPU-profile");
            if(cpuMs.Count>0){var sorted=cpuMs.OrderBy(v=>v).ToArray();Debug.Log("[PresentationFrameTiming] cpuSamples="+sorted.Length+" medianMs="+sorted[sorted.Length/2]+" p95Ms="+sorted[(int)((sorted.Length-1)*.95)]);}
            if(gpuMs.Count>0){var sorted=gpuMs.OrderBy(v=>v).ToArray();Debug.Log("[PresentationFrameTiming] gpuSamples="+sorted.Length+" medianMs="+sorted[sorted.Length/2]+" p95Ms="+sorted[(int)((sorted.Length-1)*.95)]);}
            if(cpuMs.Count==0||gpuMs.Count==0)Debug.Log("[PresentationFrameTiming] Native CPU/GPU timing unavailable for one or both counters; wall frame time is not a GPU measurement.");
        }
#endif
    }
}
