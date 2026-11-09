using System;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    // Revives the same avatar: inventory, equipment, attributes and roster never reset.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkTestPlayer), typeof(NetworkCharacterStats))]
    public sealed class NetworkPlayerRespawn : NetworkBehaviour, IInvulnerabilityProvider
    {
        [Networked] public NetworkBool WaitingForRespawn { get; private set; }
        [Networked] public TickTimer RespawnTimer { get; private set; }
        [Networked] public TickTimer ProtectionTimer { get; private set; }
        [Networked] public int DeathSequence { get; private set; }
        [Networked] public int RespawnSequence { get; private set; }
        private CharacterStats stats;
        private NetworkTestPlayer player;
        private NetworkFortressSpawner fortress;
        private int observedDeath, observedRespawn;
        private float observedCountdown = -1f;
        public event Action Died;
        public event Action<float> RespawnCountdownChanged;
        public event Action Respawned;
        private bool IsServerOwner => Object != null && Object.IsValid && Runner.IsServer && HasStateAuthority
            && Runner.TryGetPlayerObject(Object.InputAuthority, out var avatar) && avatar == Object;
        public float SecondsUntilRespawn => Object != null && Object.IsValid && WaitingForRespawn
            ? RespawnTimer.RemainingTime(Runner) ?? 0f : 0f;
        public bool IsSpawnProtected => Object != null && Object.IsValid && !WaitingForRespawn
            && !(IsServerOwner ? stats.IsDead : (bool)GetComponent<NetworkCharacterStats>().Dead)
            && ProtectionTimer.IsRunning && !ProtectionTimer.Expired(Runner);
        public bool IsInvulnerable => IsServerOwner && IsSpawnProtected;

        private void Awake() { stats = GetComponent<CharacterStats>(); player = GetComponent<NetworkTestPlayer>(); }
        public override void Spawned()
        {
            fortress = Runner.GetComponentInParent<NetworkFortressSpawner>();
            observedDeath = DeathSequence; observedRespawn = RespawnSequence;
            if (Runner.IsServer && HasStateAuthority) stats.Died += OnDeath;
        }
        private void OnDeath()
        {
            if (!IsServerOwner || WaitingForRespawn || fortress == null || fortress.RespawnConfig == null
                || !NetworkMatch.AllowsGameplay(Runner)) return;
            ProtectionTimer = TickTimer.None;
            WaitingForRespawn = true;
            DeathSequence++;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, fortress.RespawnConfig.DelayAt(fortress.Match != null ? fortress.Match.PlayingSeconds : 0));
            GetComponent<NetworkPlayerCombat>().CancelForRespawn();
            GetComponent<NetworkPlayerDodge>()?.CancelForRespawn();
            NotifyObservers();
        }
        public override void FixedUpdateNetwork()
        {
            if (!IsServerOwner || !WaitingForRespawn) return;
            if (!NetworkMatch.AllowsGameplay(Runner))
            {
                WaitingForRespawn = false; RespawnTimer = TickTimer.None;
                return;
            }
            if (!RespawnTimer.Expired(Runner) || !stats.IsDead || fortress == null || fortress.RespawnConfig == null
                || !fortress.TryChooseRespawnPose(Runner, player, out var position, out var rotation)) return;
            // Protection commits before health listeners run. Teleport avoids interpolation across the arena.
            GetComponent<NetworkTransform>().Teleport(position, rotation);
            WaitingForRespawn = false;
            RespawnTimer = TickTimer.None;
            ProtectionTimer = TickTimer.CreateFromSeconds(Runner, fortress.RespawnConfig.ProtectionDuration);
            stats.RestoreForRespawn(fortress.RespawnConfig.Mana, fortress.RespawnConfig.Stamina);
            RespawnSequence++;
            NotifyObservers();
        }
        public override void Render() => NotifyObservers();
        private void NotifyObservers()
        {
            if (observedDeath != DeathSequence) { observedDeath = DeathSequence; InvokeSafely(Died); }
            if (observedRespawn != RespawnSequence) { observedRespawn = RespawnSequence; InvokeSafely(Respawned); }
            float seconds = SecondsUntilRespawn;
            if (seconds == observedCountdown) return;
            observedCountdown = seconds;
            if (RespawnCountdownChanged != null)
                foreach (Action<float> handler in RespawnCountdownChanged.GetInvocationList())
                    try { handler(seconds); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private void InvokeSafely(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        public override void Despawned(NetworkRunner runner, bool hasState) => stats.Died -= OnDeath;
    }
}
