using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Combat
{
    internal sealed class MatchCombatAttribution
    {
        private enum TargetKind { Player, Guard, Boss }
        private readonly Dictionary<NetworkId, CombatContributionHistory<NetworkObject>> histories = new();
        private long deathSequence;

        internal void Clear() { histories.Clear(); deathSequence = 0; }
        internal void Forget(NetworkId victim) => histories.Remove(victim);

        internal void Record(NetworkObject victim, GameObject source, float damage, bool lethal, NetworkMatch match,
            out NetworkObject killer, out List<NetworkObject> assists)
        {
            killer = null;
            assists = null;
            var balance = match.Balance;
            if (balance == null || !TryClassify(victim, out var target)) return;
            var attacker = source != null ? source.GetComponent<NetworkObject>() : null;
            bool Eligible(NetworkObject candidate) => IsEligible(candidate, victim, target);
            bool hostile = Eligible(attacker);
            double time = match.Runner.SimulationTime;

            histories.TryGetValue(victim.Id, out var history);
            if (hostile)
            {
                if (history == null) histories.Add(victim.Id, history = new CombatContributionHistory<NetworkObject>());
                history.Record(attacker, damage, time, balance.AssistWindowSeconds);
                if (target == TargetKind.Player) MatchSessionData.Credit(attacker, new MatchStatistics { PlayerDamage = damage });
            }

            if (!lethal || history == null) return;
            histories.Remove(victim.Id);
            if (!history.Resolve(attacker, hostile, time, balance.AssistWindowSeconds, balance.MinimumContributionDamage,
                Eligible, SameTeam, out killer, out assists)) return;

            Credit(killer, target, assist: false);
            foreach (var assistant in assists) Credit(assistant, target, assist: true);
            string rewardId = $"combat:{match.RoundNumber}:{victim.Id}:{++deathSequence}";
            Reward(victim.Id, target, killer, assists, time, balance, rewardId);
        }

        private static bool TryClassify(NetworkObject victim, out TargetKind kind)
        {
            kind = TargetKind.Player;
            if (victim.TryGetComponent<NetworkTestPlayer>(out _)) return NetworkParticipant.IsAdmitted(victim);
            if (victim.TryGetComponent<NetworkFortressGuard>(out _)) { kind = TargetKind.Guard; return true; }
            if (victim.TryGetComponent<NetworkEnemy>(out var enemy) && enemy.Definition?.Boss != null)
            { kind = TargetKind.Boss; return true; }
            return false;
        }

        private static bool IsEligible(NetworkObject actor, NetworkObject victim, TargetKind target)
        {
            if (target == TargetKind.Player) return DamageContributionLedger.IsEnemyPlayer(actor, victim);
            return NetworkParticipant.IsAdmitted(actor) && actor.Runner == victim.Runner
                && actor.TryGetComponent<NetworkTestPlayer>(out var participant)
                && participant.Team is TeamId.TeamA or TeamId.TeamB
                && (target == TargetKind.Boss || participant.Team != victim.GetComponent<NetworkFortressGuard>().Team);
        }

        private static bool SameTeam(NetworkObject a, NetworkObject b) =>
            a.GetComponent<NetworkTestPlayer>().Team == b.GetComponent<NetworkTestPlayer>().Team;

        private static void Credit(NetworkObject actor, TargetKind target, bool assist)
        {
            if (assist) MatchSessionData.Credit(actor, new MatchStatistics { Assists = 1 });
            else MatchSessionData.Credit(actor, target switch
            {
                TargetKind.Player => new MatchStatistics { Kills = 1 },
                TargetKind.Guard => new MatchStatistics { GuardsKilled = 1 },
                _ => new MatchStatistics { BossesKilled = 1 }
            });
        }

        private static void Reward(NetworkId victim, TargetKind target, NetworkObject killer, List<NetworkObject> assists,
            double now, MatchBalanceConfig balance, string id)
        {
            bool pvp = target == TargetKind.Player;
            int killXp = target == TargetKind.Guard ? balance.GuardKillExperience
                : target == TargetKind.Boss ? balance.BossKillExperience : balance.KillExperience;
            int pool = target == TargetKind.Guard ? balance.GuardAssistExperiencePool
                : target == TargetKind.Boss ? balance.BossAssistExperiencePool : balance.AssistExperiencePool;

            // Each recipient is checked independently. A finisher on cooldown cannot
            // suppress another player's eligible assist or shift the attribution.
            if (!pvp || AcceptPlayerReward(killer, victim, now, balance, false)) Grant(killer, killXp, id + ":kill");
            if (pvp) assists = assists.FindAll(actor => AcceptPlayerReward(actor, victim, now, balance, true));
            for (int i = 0; i < assists.Count; i++)
                Grant(assists[i], pool / assists.Count + (i < pool % assists.Count ? 1 : 0), id + ":assist");
        }

        private static bool AcceptPlayerReward(NetworkObject actor, NetworkId victim, double now, MatchBalanceConfig balance, bool assist)
            => actor.GetComponent<NetworkPlayerExperienceRewards>()?.TryRecordReward(victim, now, balance, assist) == true;

        private static void Grant(NetworkObject actor, int xp, string id)
        {
            if (xp > 0) actor.GetComponent<NetworkSessionProgression>()?.TryGrantExperience(xp, id);
        }
    }
}
