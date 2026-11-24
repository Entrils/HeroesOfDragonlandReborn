using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkPlayerRespawn), typeof(SessionProgression), typeof(DamageReceiver))]
    public sealed class NetworkPlayerExperienceRewards : NetworkBehaviour
    {
        [Networked] public int Kills { get; private set; }
        [Networked] public int Assists { get; private set; }
        private readonly Dictionary<NetworkId, double> lastVictimRewards = new();
        private CharacterStats stats;
        private NetworkPlayerRespawn respawn;
        private int round;
        private bool pendingDeath;
        private void Awake() { stats = GetComponent<CharacterStats>(); respawn = GetComponent<NetworkPlayerRespawn>(); }
        public override void Spawned()
        {
            if (Runner.IsServer && HasStateAuthority) stats.Died += OnDeath;
        }

        private void OnDeath() => pendingDeath = true;

        private void EnsureRound(int current)
        {
            if (round == current) return;
            round = current;
            Kills = Assists = 0;
            lastVictimRewards.Clear();
        }

        internal bool TryRecordReward(NetworkId victim, double now, MatchBalanceConfig balance, bool assist)
        {
            if (!NetworkParticipant.IsAdmitted(Object) || NetworkMatchPacing.MatchFor(Runner) is not { CanPlay: true } match)
                return false;
            EnsureRound(match.RoundNumber);
            if (lastVictimRewards.TryGetValue(victim, out var previous) && now - previous < balance.RepeatedVictimRewardCooldown)
                return false;
            lastVictimRewards[victim] = now;
            if (assist) Assists++; else Kills++;
            return true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!NetworkParticipant.IsAdmitted(Object)) return;
            var match = NetworkMatchPacing.MatchFor(Runner);
            var balance = match?.Balance;
            if (balance == null) return;
            EnsureRound(match.RoundNumber);
            if (!pendingDeath) return;
            pendingDeath = false;
            if (!match.CanPlay || !stats.IsDead) return;
            string id = "player-death:" + round + ":" + Object.Id + ":" + respawn.DeathSequence;
            var progression = GetComponent<SessionProgression>();
            progression.TryApplyDeathLoss(balance.DeathExperienceLoss(progression.CurrentXP), progression.SessionId, id, out _);
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => stats.Died -= OnDeath;
    }
}
