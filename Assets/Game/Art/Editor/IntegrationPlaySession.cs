using System;
using System.Threading.Tasks;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class IntegrationPlaySession
    {
        public static string Status { get; private set; }="Not run";
        public static async void StartLocal()
        {
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Use Play Mode.");
                Status="Starting local six-actor session";
                var bootstrap=UnityEngine.Object.FindFirstObjectByType<MultiplayerBootstrap>();
                bootstrap.GetComponent<DeveloperBotService>().SetBotCount(5);
                await bootstrap.StartSession(GameMode.Single,"m54-m62-local-validation");
                for(int i=0;i<100;i++)
                {
                    if(!Application.isPlaying)return;
                    var match=bootstrap.GetComponent<NetworkFortressSpawner>().Match;
                    if(match!=null&&match.Object!=null&&match.Object.IsValid&&bootstrap.GetComponent<DeveloperBotService>().SpawnedCount==5)
                    {
                        match.RPC_SetReady(true);
                        await Task.Delay(250);
                        if(match.TryStartLobby()){Status="Local six-actor match starting";return;}
                    }
                    await Task.Delay(200);
                }
                throw new InvalidOperationException("Local match did not become ready.");
            }
            catch(Exception error){Status="FAILED "+error.Message;Debug.LogException(error);}
        }
    }
}
