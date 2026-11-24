using System;
using System.Collections;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.UI.Minimap;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.Art.Editor
{
    public static class BattlefieldModelPlayValidation
    {
        public static string Status { get; private set; }="Not run";
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Map models: "+reason);}
        public static void Start()
        {
            Check(Application.isPlaying,"Use Play Mode in an active local match");Status="RUNNING";
            var host=new GameObject("Temporary model lifecycle check").AddComponent<ArtPipelineValidationHost>();host.StartCoroutine(Run(host.gameObject));
        }
        private static IEnumerator Run(GameObject host)
        {
            try
            {
                var bootstrap=UnityEngine.Object.FindAnyObjectByType<MultiplayerBootstrap>();
                Check(bootstrap!=null&&bootstrap.Running&&bootstrap.Runner.IsServer,"Admitted local authority required");
                var gates=UnityEngine.Object.FindObjectsByType<NetworkFortressGate>().Where(g=>g.Runner==bootstrap.Runner&&g.Object!=null&&g.Object.IsValid).ToArray();
                Check(gates.Length==2,"Both gate prefab instances required");
                foreach(var gate in gates)
                {
                    var body=gate.transform.Find("Gate body");var imported=body!=null?body.Find("Imported wooden gate"):null;
                    Check(imported!=null&&imported.gameObject.activeInHierarchy&&!body.GetComponent<MeshRenderer>().enabled,"Imported closed gate missing or old cube visible");
                    Check(imported.GetComponentsInChildren<Collider>(true).Length==0,"Visual gate introduced collision");
                    Check(gate.GetComponent<BoxCollider>().enabled&&gate.GetComponent<NavMeshObstacle>().enabled,"Original closed blocker missing");
                    Check(imported.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh!=null&&f.name.EndsWith("_LOD0")),"Gate did not use imported geometry");
                }
                foreach(var gate in gates)gate.GetComponent<CharacterStats>().ApplyDamage(999999);
                yield return null;yield return null;
                foreach(var gate in gates)
                {
                    Check(gate.IsOpen&&!gate.transform.Find("Gate body").gameObject.activeInHierarchy,"Imported art failed to follow authoritative gate destruction");
                    Check(!gate.GetComponent<BoxCollider>().enabled&&!gate.GetComponent<NavMeshObstacle>().enabled,"Destroyed gate retained blocker");
                }
                var map=bootstrap.GetComponent<TacticalMapSource>();map.Refresh();
                Check(map.Markers.Count(m=>m.Kind==MapMarkerKind.Objective&&m.State==MapMarkerState.Cleared)==2,"Public minimap did not observe opened gates");
                Status="PASS both imported gate visuals, retained closed blockers, authoritative destruction hiding visuals/removing blockers, and both public gate map states.";
                Debug.Log("[Map models Play] "+Status);
            }
            finally{UnityEngine.Object.Destroy(host);if(Status=="RUNNING")Status="FAILED; inspect Console";}
        }
    }
}
