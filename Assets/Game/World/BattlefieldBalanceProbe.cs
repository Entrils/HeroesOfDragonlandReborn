using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Reflection;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Progression;
#endif

namespace HeroesOfDragonland.World
{
    // Opt-in integration test only. Timer deadlines are shortened on the server,
    // while the saved production delays remain unchanged.
    public sealed class BattlefieldBalanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool active;
        private int stage;
        private float started, changed;
        private NetworkCamp low, center;
        private NetworkId oldChest;
        private NetworkObject[] actors;
        private long[] xp;
        private void Awake() => active=Environment.GetCommandLineArgs().Contains("-hodrBalance");
        private static void Check(bool condition,string name)
        {
            if(!condition) throw new InvalidOperationException("[BalanceCheck] FAIL "+name);
            Debug.Log("[BalanceCheck] PASS "+name);
        }
        private void Deadline(NetworkCamp camp) => typeof(NetworkCamp).GetProperty("NextSpawnTimer")
            .SetValue(camp,TickTimer.CreateFromSeconds(camp.Runner,.05f));
        private void Kill(NetworkCamp camp)
        {
            foreach(var enemy in FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None))
                if(enemy.Runner==camp.Runner && Vector3.Distance(enemy.transform.position,camp.transform.position)<6)
                    enemy.GetComponent<CharacterStats>().ApplyDamage(100000);
        }
        private void LateUpdate()
        {
            if(!active)return;
            var bootstrap=GetComponent<MultiplayerBootstrap>();var runner=bootstrap.Runner;
            var match=GetComponent<NetworkFortressSpawner>().Match;
            if(runner==null || !runner.IsRunning || !runner.IsServer || match==null || match.State!=MatchPhase.Playing)return;
            if(started==0)started=Time.unscaledTime;
            if(Time.unscaledTime-started>60){active=false;Check(false,"schedule-timeout");}
            if(stage==0)
            {
                if(Time.unscaledTime-started<8)return;
                var camps=FindObjectsByType<NetworkCamp>(FindObjectsSortMode.None);
                low=camps.First(c=>c.Runner==runner && c.Definition.RespawnDelay==75);
                center=camps.First(c=>c.Runner==runner && c.Definition.InitialSpawnDelay==150);
                Check(center.State==NetworkCampState.Pending && center.EnemyCount==0 && !center.TryInitialize(),"center-cannot-spawn-early");
                Check(center.NextSpawnTimer.RemainingTime(runner)>130,"center-delay-starts-at-playing");
                actors=runner.ActivePlayers.Select(p=>runner.TryGetPlayerObject(p,out var obj)?obj:null).ToArray();
                xp=actors.Select(a=>a.GetComponent<SessionProgression>().CurrentXP).ToArray();
                for(int i=0;i<actors.Length;i++) actors[i].GetComponent<NetworkTransform>().Teleport(low.transform.position+new Vector3(0,1,-3),Quaternion.identity);
                Kill(low);stage=1;changed=Time.unscaledTime;
            }
            else if(stage==1 && Time.unscaledTime-changed>.3f)
            {
                Check(low.State==NetworkCampState.Cleared && low.CompletionCount==1,"first-clear");
                Check(low.NextSpawnTimer.RemainingTime(runner)>73,"respawn-timed-from-clear");
                for(int i=0;i<actors.Length;i++)Check(actors[i].GetComponent<SessionProgression>().CurrentXP==xp[i]+25,"first-xp-"+i);
                Check(!low.TryComplete(),"clear-idempotent");oldChest=low.ChestId;
                Check(oldChest!=default,"first-chest");Deadline(low);stage=2;changed=Time.unscaledTime;
            }
            else if(stage==2 && Time.unscaledTime-changed>.3f)
            {
                Check(low.State==NetworkCampState.Active && low.DeadCount==0 && low.EnemyCount==2,"camp-reactivated");
                Check(!runner.TryFindObject(oldChest,out _),"old-chest-cleaned-up");
                Kill(low);stage=3;changed=Time.unscaledTime;
            }
            else if(stage==3 && Time.unscaledTime-changed>.3f)
            {
                Check(low.CompletionCount==2 && low.ChestId!=default && low.ChestId!=oldChest,"second-clear-new-chest");
                for(int i=0;i<actors.Length;i++)Check(actors[i].GetComponent<SessionProgression>().CurrentXP==xp[i]+50,"repeat-xp-"+i);
                Deadline(center);stage=4;changed=Time.unscaledTime;
            }
            else if(stage==4 && Time.unscaledTime-changed>.3f)
            {
                Check(center.State==NetworkCampState.Active && center.EnemyCount==3,"delayed-center-activates");
                active=false;Debug.Log("[BalanceComplete] PASS recurring rewards, cleanup and delayed center");
            }
        }
#endif
    }
}
