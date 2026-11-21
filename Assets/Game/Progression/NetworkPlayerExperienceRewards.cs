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
        private readonly DamageContributionLedger ledger = new();
        private readonly Dictionary<NetworkId, float> lastVictimRewards = new();
        private DamageReceiver receiver;
        private CharacterStats stats;
        private NetworkPlayerRespawn respawn;
        private NetworkObject lethalAttacker;
        private int processedDeath, round;
        private bool pendingDeath;
        private void Awake() { receiver = GetComponent<DamageReceiver>(); stats = GetComponent<CharacterStats>(); respawn = GetComponent<NetworkPlayerRespawn>(); }
        public override void Spawned()
        {
            if (!Runner.IsServer || !HasStateAuthority) return;
            receiver.DamageTaken += OnDamage; stats.Died += OnDeath;
        }
        private void OnDeath() => pendingDeath = true;
        private void OnDamage(float amount, GameObject source)
        {
            if (!NetworkParticipant.IsAdmitted(Object) || !NetworkMatch.AllowsGameplay(Runner)) return;
            var attacker = source != null ? source.GetComponent<NetworkObject>() : null;
            if (!DamageContributionLedger.IsEnemyPlayer(attacker, Object)) return;
            ledger.Record(attacker, amount, (float)Runner.SimulationTime);
            if (stats.IsDead)
            {
                lethalAttacker = attacker;
                // Profile kills count accepted lethal hits, independently of the XP repeat-victim cooldown.
                MatchSessionData.Credit(attacker, MatchSessionData.Metric.Kill);
            }
        }
        public override void FixedUpdateNetwork()
        {
            if (!NetworkParticipant.IsAdmitted(Object)) return;
            var match = NetworkMatchPacing.MatchFor(Runner); var balance = match?.Balance;
            if (match == null || balance == null) return;
            if (round != match.RoundNumber)
            {
                round = match.RoundNumber; Kills = Assists = 0; lastVictimRewards.Clear(); ledger.Clear();
                processedDeath = respawn.DeathSequence; pendingDeath = false; lethalAttacker = null;
            }
            if (!match.CanPlay) { ledger.Clear(); pendingDeath = false; lethalAttacker = null; return; }
            if (!pendingDeath) return;
            pendingDeath = false;
            if (!stats.IsDead || processedDeath == respawn.DeathSequence) return;
            processedDeath = respawn.DeathSequence;
            string id = "player-death:" + round + ":" + Object.Id + ":" + processedDeath;
            var progression = GetComponent<SessionProgression>();
            progression.TryApplyDeathLoss(balance.DeathExperienceLoss(progression.CurrentXP), progression.SessionId, id, out _);
            float now = (float)Runner.SimulationTime;
            // No last-hit credit for environmental/NPC deaths; no suicide or friendly rewards.
            if (DamageContributionLedger.IsEnemyPlayer(lethalAttacker, Object))
            {
                var killer = lethalAttacker.GetComponent<NetworkPlayerExperienceRewards>();
                if (killer != null && killer.TryAcceptVictim(Object.Id, now, balance))
                {
                    killer.Kills++;
                    lethalAttacker.GetComponent<NetworkSessionProgression>()?.TryGrantExperience(balance.KillExperience, id + ":kill");
                    var assists = new List<NetworkPlayerExperienceRewards>();
                    foreach (var entry in ledger.Recent(now, balance.AssistWindowSeconds, balance.MinimumContributionDamage))
                    {
                        if (entry.Actor == lethalAttacker || !DamageContributionLedger.IsEnemyPlayer(entry.Actor, Object)
                            || entry.Actor.GetComponent<NetworkTestPlayer>().Team != lethalAttacker.GetComponent<NetworkTestPlayer>().Team) continue;
                        var helper = entry.Actor.GetComponent<NetworkPlayerExperienceRewards>();
                        if (helper != null && helper.TryAcceptVictim(Object.Id, now, balance)) assists.Add(helper);
                    }
                    int award = assists.Count == 0 ? 0 : balance.AssistExperiencePool / assists.Count;
                    foreach (var helper in assists) { helper.Assists++; helper.GetComponent<NetworkSessionProgression>()?.TryGrantExperience(award, id + ":assist"); }
                }
            }
            ledger.Clear(); lethalAttacker = null;
        }
        private bool TryAcceptVictim(NetworkId victim, float now, MatchBalanceConfig balance)
        {
            if (lastVictimRewards.TryGetValue(victim, out var previous) && now - previous < balance.RepeatedVictimRewardCooldown) return false;
            lastVictimRewards[victim] = now; return true;
        }
        public override void Despawned(NetworkRunner runner, bool hasState) { receiver.DamageTaken -= OnDamage; stats.Died -= OnDeath; }
    }
}
