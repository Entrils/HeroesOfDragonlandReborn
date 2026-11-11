using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    // Observes accepted simulation. None of these presentation fields authorize movement, damage or defense.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkTestPlayer),typeof(DamageReceiver))]
    public sealed class NetworkCharacterAnimationState : NetworkBehaviour, IAfterTick
    {
        [Networked] public NetworkBool LockedOn {get;private set;}
        [Networked] public NetworkBool Grounded {get;private set;}
        [Networked] public float VerticalSpeed {get;private set;}
        [Networked] public int HitSequence {get;private set;}
        [Networked] public int HitTick {get;private set;}
        [Networked] public int DeathTick {get;private set;}
        [Networked] public int StaggerTick {get;private set;}
        private CharacterStats stats;
        private DamageReceiver receiver;
        private Vector3 previous;
        private bool wasStaggered;
        private bool IsOwner=>NetworkParticipant.IsAdmitted(Object);
        private void Awake(){stats=GetComponent<CharacterStats>();receiver=GetComponent<DamageReceiver>();}
        public override void Spawned()
        {
            previous=transform.position;
            if(Runner.IsServer&&HasStateAuthority){receiver.DamageTaken+=Hit;stats.Died+=Died;}
        }
        public override void FixedUpdateNetwork()
        {
            if(!IsOwner)return;
            GetInput(out NetworkPlayerInput input);
            LockedOn=NetworkMatch.AllowsGameplay(Runner)&&!stats.IsDead&&input.LockedOn;
        }
        public void AfterTick()
        {
            if(!IsOwner)return;
            var delta=transform.position-previous;previous=transform.position;
            VerticalSpeed=delta.magnitude<3&&NetworkMatch.AllowsGameplay(Runner)?Mathf.Clamp(delta.y/Runner.DeltaTime,-30,30):0;
            Grounded=false;
            var feet=transform.position-Vector3.up;
            foreach(var hit in Physics.RaycastAll(feet+Vector3.up*.15f,Vector3.down,.4f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(hit.transform!=transform&&!hit.transform.IsChildOf(transform)&&hit.normal.y>.5f){Grounded=true;break;}
            bool staggered=GetComponent<CombatDefense>().IsStaggered;
            if(staggered&&!wasStaggered)StaggerTick=Runner.Tick.Raw;
            wasStaggered=staggered;
        }
        private void Hit(float amount,GameObject source)
        {
            if(!IsOwner||stats.IsDead||!NetworkMatch.AllowsGameplay(Runner)||GetComponent<CombatDefense>().IsBlocking)return;
            HitSequence++;HitTick=Runner.Tick.Raw;
        }
        private void Died(){if(IsOwner)DeathTick=Runner.Tick.Raw;}
        public override void Despawned(NetworkRunner runner,bool hasState){receiver.DamageTaken-=Hit;stats.Died-=Died;}
    }
}
