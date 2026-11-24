using System;
using System.Collections.Generic;
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
        private readonly DeathRecapHistory recapHistory = new();
        private string pendingHeadline, pendingRecent, pendingAssists;
        private string recapHeadline, recapRecent, recapAssists;
        private int recapSequence;
        private float recapExpires;

        internal void RecordRecapHit(GameObject source, string action, float amount, bool lethal,
            NetworkObject killer, List<NetworkObject> assistants)
        {
            if (!IsServerOwner) return;
            recapHistory.Record(DeathRecapHistory.Describe(source, gameObject),
                action ?? (source == null ? "World damage" : "Attack"), amount, Runner.SimulationTime);
            if (!lethal) return;
            var names = new List<string>(2);
            if (assistants != null)
                foreach (var assistant in assistants)
                {
                    if (names.Count == 2) break;
                    names.Add(DeathRecapHistory.Describe(assistant.gameObject, gameObject));
                }
            recapHistory.Build(killer != null ? DeathRecapHistory.Describe(killer.gameObject, gameObject) : null,
                names.Count > 0 ? "Assists: " + string.Join(", ", names) : "",
                out pendingHeadline, out pendingRecent, out pendingAssists);
        }

        public bool TryGetDeathRecap(out string headline, out string recent, out string assists)
        {
            headline = recent = assists = "";
            if (Object == null || !Object.IsValid || !HasInputAuthority || !WaitingForRespawn
                || recapSequence != DeathSequence || Time.unscaledTime >= recapExpires || string.IsNullOrEmpty(recapHeadline)
                || !NetworkMatch.AllowsGameplay(Runner)) return false;
            headline = recapHeadline;
            recent = recapRecent;
            assists = recapAssists;
            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_DeathRecap([RpcTarget] PlayerRef recipient, int sequence, string headline,
            string recent, string assists)
        {
            if (recipient == PlayerRef.None || recipient != Runner.LocalPlayer || !HasInputAuthority
                || sequence <= recapSequence || sequence < DeathSequence) return;
            recapSequence = sequence;
            recapExpires = Time.unscaledTime + DeathRecapHistory.DisplaySeconds;
            recapHeadline = headline;
            recapRecent = recent;
            recapAssists = assists;
        }
        public event Action Died;
        public event Action<float> RespawnCountdownChanged;
        public event Action Respawned;
        private bool IsServerOwner => NetworkParticipant.IsAdmitted(Object);
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
            MatchSessionData.Credit(Object, new MatchStatistics { Deaths = 1 });
            var balance = fortress.Match != null ? fortress.Match.Balance : null;
            float elapsed = fortress.Match != null ? fortress.Match.PlayingSeconds : 0;
            float delay = balance != null ? balance.RespawnDelay(elapsed, GetComponent<HeroesOfDragonland.Progression.SessionProgression>()?.Level ?? 1)
                : fortress.RespawnConfig.DelayAt(elapsed);
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, delay);
            GetComponent<NetworkPlayerCombat>().CancelForRespawn();
            GetComponent<NetworkPlayerDodge>()?.CancelForRespawn();
            if (Object.InputAuthority != PlayerRef.None && !string.IsNullOrEmpty(pendingHeadline))
                RPC_DeathRecap(Object.InputAuthority, DeathSequence, pendingHeadline, pendingRecent, pendingAssists);
            pendingHeadline = pendingRecent = pendingAssists = null;
            NotifyObservers();
        }
        public override void FixedUpdateNetwork()
        {
            if (!IsServerOwner) return;
            if (NetworkMatch.AllowsGameplay(Runner)) recapHistory.Expire(Runner.SimulationTime);
            else
            {
                recapHistory.Clear();
                pendingHeadline = pendingRecent = pendingAssists = null;
            }
            if (!WaitingForRespawn) return;
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
        public override void Render()
        {
            if (recapHeadline != null && (Time.unscaledTime >= recapExpires || DeathSequence > recapSequence
                || (!WaitingForRespawn && DeathSequence == recapSequence) || !NetworkMatch.AllowsGameplay(Runner)))
                recapHeadline = recapRecent = recapAssists = null;
            NotifyObservers();
        }
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
