using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
#endif

namespace HeroesOfDragonland.World
{
    // Opt-in six-process smoke test. Normal matches do not run this code.
    public sealed class BattlefieldAcceptanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active, complete, worldChecked, arrivalChecked, dodgeChecked;
        private float playingAt;
        private bool planned;
        private Vector3 start, destination;
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
                Check(camps.Length==11 && camps.Count(camp=>camp.State==NetworkCampState.Active)==8
                    && camps.Count(camp=>camp.State==NetworkCampState.Pending && camp.ConfiguredFirstSpawn==150)==3,"eight-active-and-three-delayed");
                Check(camps.Count(camp=>camp.Definition.Difficulty==CampDifficulty.Tier1)==4
                    && camps.Count(camp=>camp.Definition.Difficulty==CampDifficulty.Tier2)==4
                    && camps.Count(camp=>camp.Definition.Difficulty==CampDifficulty.Tier3)==2
                    && camps.Count(camp=>camp.Definition.Difficulty==CampDifficulty.Contested)==1,"four-low-four-medium-two-high-one-boss");
                Check(camps.All(camp=>camp.ConfiguredRadius>0 && camp.ConfiguredRespawn>0 && camp.CanMutateCamp),"server-owns-renewable-camps");
                var actors=runner.ActivePlayers.Select(player=>runner.TryGetPlayerObject(player,out var obj)?obj:null).ToArray();
                Check(actors.Length==6 && actors.All(actor=>actor!=null && actor.IsValid),"all-six-player-avatars");
                for(int i=0;i<actors.Length;i++) for(int j=i+1;j<actors.Length;j++)
                    Check(Vector3.Distance(actors[i].transform.position,actors[j].transform.position)>1,"no-stacked-spawns");
                worldChecked=true;
            }
            // Check the untouched roster and spawn spacing before any fixture starts walking.
            if (Time.unscaledTime-playingAt<2) return;
            if(!runner.TryGetPlayerObject(runner.LocalPlayer,out var actor) || actor==null || !actor.IsValid) return;
            var playerState=actor.GetComponent<NetworkTestPlayer>();
            if(!planned)
            {
                if (!runner.IsServer)
                {
                    var enemies = FindObjectsByType<NetworkEnemy>().Where(enemy=>enemy.Object != null && enemy.Object.IsValid && enemy.Runner==runner).ToArray();
                    if (enemies.Length == 0) return;
                    Check(enemies.All(enemy=>!enemy.CanSimulateEnemy),"replicas-do-not-simulate-enemy-ai");
                }
                Check(actor.HasInputAuthority,"local-owner");
                Check(bootstrap.GetComponent<TeamSpawnService>().GetArea(playerState.Team).Contains(actor.transform.position),"spawn-inside-own-fortress");
                // Move each spawn straight inward on its own lane; flank traversal is tested separately.
                float sign=playerState.Team==TeamId.TeamA?-1:1;
                start=actor.transform.position;
                destination=new Vector3(31.5f*sign,start.y,start.z);
                planned=true;
            }
            bootstrap.TestAttack=false; bootstrap.TestDodge=false;
            if(!arrivalChecked)
            {
                Vector3 delta=destination-actor.transform.position; delta.y=0;
                arrivalChecked=delta.sqrMagnitude<.36f;
                bootstrap.TestInput=arrivalChecked ? Vector2.zero : new Vector2(delta.x,delta.z).normalized;
                if(arrivalChecked) Check(Mathf.Abs(actor.transform.position.x-start.x)>2.5f,"accepted-and-replicated-courtyard-movement");
                else if(Time.unscaledTime-playingAt>20) Check(false,"movement-timeout position="+actor.transform.position);
            }
            if(arrivalChecked && !dodgeChecked)
            {
                bootstrap.TestInput=Vector2.zero;
                dodgeChecked=actor.GetComponent<NetworkPlayerDodge>().DodgeSequence>0;
                bootstrap.TestDodge=!dodgeChecked;
                if (dodgeChecked) Check(true,"server-accepted-and-replicated-dodge");
                else if (Time.unscaledTime-playingAt>25) Check(false,"dodge-timeout");
            }
            if(dodgeChecked && (!runner.IsServer || worldChecked))
            {
                complete=true; Debug.Log("[BattlefieldComplete] participant="+playerState.ParticipantNumber+" server="+runner.IsServer);
            }
        }
#endif
    }
}
