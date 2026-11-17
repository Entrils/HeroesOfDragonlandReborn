using System;
using System.Collections;
using System.Linq;
using HeroesOfDragonland.Audio;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class AudioAmbiencePlayValidation
    {
        public static string Status { get; private set; }="Not run";
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Ambience: "+reason);}
        public static void Start()
        {
            Check(Application.isPlaying&&!Application.isBatchMode,"Use non-headless Play Mode");
            Status="RUNNING";var host=new GameObject("Temporary ambience validation").AddComponent<ArtPipelineValidationHost>();host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            GameObject fixture=null;
            try
            {
                fixture=new GameObject("Temporary ambience pool");fixture.SetActive(false);
                var listener=new GameObject("Presentation position only").transform;listener.SetParent(fixture.transform);listener.position=Vector3.right*1000;
                var zone=new GameObject("Temporary fortress zone").AddComponent<AudioAmbienceZone>();zone.transform.SetParent(fixture.transform);
                var service=fixture.AddComponent<GameAudioService>();var data=new SerializedObject(service);
                var catalog=AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioSystemSetup.CatalogPath);
                data.FindProperty("catalog").objectReferenceValue=catalog;data.FindProperty("listener").objectReferenceValue=listener;
                data.FindProperty("voiceCount").intValue=8;data.ApplyModifiedPropertiesWithoutUndo();
                var controller=fixture.AddComponent<AudioAmbienceController>();data=new SerializedObject(controller);
                data.FindProperty("zones").arraySize=1;data.FindProperty("zones").GetArrayElementAtIndex(0).objectReferenceValue=zone;data.ApplyModifiedPropertiesWithoutUndo();
                fixture.SetActive(true);yield return new WaitForSecondsRealtime(.7f);
                var bed=fixture.GetComponentsInChildren<AudioSource>().Single(s=>s.name=="Ambience");
                Check(bed.clip==catalog.Find(AudioCue.ForestAmbience).clips[0]&&bed.isPlaying,"Forest fallback did not start");
                listener.position=Vector3.zero;
                float deadline=Time.realtimeSinceStartup+8;
                while(Time.realtimeSinceStartup<deadline && (bed.clip!=catalog.Find(AudioCue.FortressAmbience).clips[0]||!bed.isPlaying||bed.volume<=0))yield return null;
                Check(bed.clip==catalog.Find(AudioCue.FortressAmbience).clips[0]&&bed.isPlaying&&bed.volume>0,
                    "Fortress fade/switch failed: clip="+bed.clip+" playing="+bed.isPlaying+" volume="+bed.volume+" zoneContains="+zone.Contains(listener.position));
                listener.position=Vector3.right*1000;yield return new WaitForSecondsRealtime(1.3f);
                Check(bed.clip==catalog.Find(AudioCue.ForestAmbience).clips[0]&&bed.isPlaying,"Forest return failed");
                controller.enabled=false;controller.enabled=true;yield return new WaitForSecondsRealtime(.7f);
                Check(bed.isPlaying,"Controller reenable did not resume bed");
                service.enabled=false;yield return null;Check(!bed.isPlaying,"Disabled service retained ambience");
                service.enabled=true;yield return new WaitForSecondsRealtime(.7f);Check(bed.isPlaying,"Service reenable did not recover ambience");
                Check(fixture.GetComponentsInChildren<AudioSource>().Length==11,"Ambience allocated additional sources");
                Status="PASS native forest/fortress/forest bed transitions, controller/service reenable and fixed source allocation; no additional AudioListener created.";
                Debug.Log("[M60 Ambience] "+Status);
            }
            finally{if(fixture!=null)UnityEngine.Object.Destroy(fixture);UnityEngine.Object.Destroy(host);if(Status=="RUNNING")Status="FAILED; inspect Console";}
        }
    }
}
