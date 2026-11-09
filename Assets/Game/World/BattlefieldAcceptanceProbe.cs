using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine.AI;
#endif

namespace HeroesOfDragonland.World
{
    // Opt-in six-process smoke test. Normal matches do not run this code.
    public sealed class BattlefieldAcceptanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, complete, worldChecked, arrivalChecked;
        private float playingAt;
        private int waypoint;
        private NavMeshPath route;
        private Vector3 destination;
        private void Awake() => active=Environment.GetCommandLineArgs().Contains("-hodrBattlefield");
        private void Check(bool condition, string label)
        {
            if(!condition) { active=false; throw new InvalidOperationException("[BattlefieldCheck] FAIL "+label); }
            Debug.Log("[BattlefieldCheck] PASS "+label);
        }
        private void LateUpdate()
        {
            if(!active || complete) return;
            var bootstrap=GetComponent<MultiplayerBootstrap>(); var runner=bootstrap.Runner;
            var match=GetComponent<NetworkFortressSpawner>().Match;
            if(runner==null || !runner.IsRunning || match==null || !match.Object.IsValid || match.State!=MatchPhase.Playing) return;
            if(playingAt==0) playingAt=Time.unscaledTime;
            if(runner.IsServer && !worldChecked && Time.unscaledTime-playingAt>1)
            {
                var camps=FindObjectsByType<NetworkCamp>(FindObjectsInactive.Exclude).Where(camp=>camp.Runner==runner).ToArray();
                Check(match.TeamACount==3 && match.TeamBCount==3 && match.RequiredPlayers==6,"actual-3v3-match-start");
                Check(camps.Length==9 && camps.Count(camp=>camp.State==NetworkCampState.Active)==8
                    && camps.Count(camp=>camp.State==NetworkCampState.Pending && camp.Definition.InitialSpawnDelay==150)==1,"eight-active-and-delayed-center");
                Check(camps.Count(camp=>camp.Definition.ExperienceReward==25)==4
                    && camps.Count(camp=>camp.Definition.ExperienceReward==50)==4
                    && camps.Count(camp=>camp.Definition.ExperienceReward==90)==1,"four-low-four-medium-one-high");
                Check(camps.All(camp=>camp.Definition.ExperienceRadius==9 && camp.Definition.RespawnDelay>0),"renewable-farming-requires-presence");
                var actors=runner.ActivePlayers.Select(player=>runner.TryGetPlayerObject(player,out var obj)?obj:null).ToArray();
                Check(actors.Length==6 && actors.All(actor=>actor!=null && actor.IsValid),"all-six-player-avatars");
                for(int i=0;i<actors.Length;i++) for(int j=i+1;j<actors.Length;j++)
                    Check(Vector3.Distance(actors[i].transform.position,actors[j].transform.position)>1,"no-stacked-spawns");
                worldChecked=true;
            }
            if(!runner.TryGetPlayerObject(runner.LocalPlayer,out var actor) || actor==null || !actor.IsValid) return;
            var playerState=actor.GetComponent<NetworkTestPlayer>();
            if(route==null)
            {
                Check(actor.HasInputAuthority,"local-owner");
                Check(bootstrap.GetComponent<TeamSpawnService>().GetArea(playerState.Team).Contains(actor.transform.position),"spawn-inside-own-fortress");
                // Leave each base through its main gate; spread destinations by participant.
                float sign=playerState.Team==TeamId.TeamA?-1:1;
                destination=new Vector3(20*sign,0,(playerState.ParticipantNumber%3-1)*3);
                route=new NavMeshPath();
                Check(NavMesh.SamplePosition(actor.transform.position,out var start,2,NavMesh.AllAreas)
                    && NavMesh.CalculatePath(start.position,destination,NavMesh.AllAreas,route)
                    && route.status==NavMeshPathStatus.PathComplete,"spawn-to-open-battlefield-route");
                waypoint=1;
            }
            bootstrap.TestAttack=false; bootstrap.TestDodge=false;
            if(waypoint<route.corners.Length)
            {
                Vector3 delta=route.corners[waypoint]-actor.transform.position; delta.y=0;
                if(delta.magnitude<.6f) waypoint++;
                else bootstrap.TestInput=new Vector2(delta.x,delta.z).normalized;
            }
            else if(!arrivalChecked)
            {
                bootstrap.TestInput=Vector2.zero;
                Check(Mathf.Abs(actor.transform.position.x)<23,"server-motor-reaches-outside-fortress");
                arrivalChecked=true;
            }
            if(Time.unscaledTime-playingAt>35 && !arrivalChecked) Check(false,"movement-route-timeout");
            if(arrivalChecked && (!runner.IsServer || worldChecked))
            {
                complete=true; Debug.Log("[BattlefieldComplete] participant="+playerState.ParticipantNumber+" server="+runner.IsServer);
            }
        }
#endif
    }
}
