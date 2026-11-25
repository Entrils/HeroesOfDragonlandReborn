using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public sealed partial class NetworkTestPlayer
    {
        [SerializeField, Range(2, 15)] private float pingLifetime = 6f;
        [SerializeField, Range(.5f, 5)] private float pingCooldown = 1f;
        [SerializeField, Range(1, 4)] private int maxActivePings = 3;
        private readonly Queue<int> pingExpirations = new();
        private readonly List<TeamPing> receivedPings = new();
        private int pingRound = -1;
        private int nextPingTick;

        internal bool RequestTeamPing(TeamPingKind intent, Vector3 point, NetworkId target)
        {
            if (Object == null || !Object.IsValid || !HasInputAuthority || IsSuspended
                || networkStats == null || networkStats.Dead || !NetworkMatch.AllowsGameplay(Runner)) return false;
            var match = NetworkMatchPacing.MatchFor(Runner);
            if (match == null) return false;
            RPC_RequestTeamPing(match.RoundNumber, intent, point, target);
            return true;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_RequestTeamPing(int round, TeamPingKind intent, Vector3 point, NetworkId target, RpcInfo info = default)
        {
            if (!NetworkParticipant.OwnsRequest(Object, info.Source))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.Sender);
                return;
            }
            if (networkStats == null || networkStats.Dead || !TeamPings.PlayableTeam(Team)) return;
            var match = NetworkMatchPacing.MatchFor(Runner);
            if (match == null || !match.CanPlay || round != match.RoundNumber) return;
            int now = Runner.Tick.Raw;
            if (pingRound != round) { pingRound = round; nextPingTick = now; pingExpirations.Clear(); }
            if (now < nextPingTick) return;
            // Charge the request before any physics work, including rejected requests.
            nextPingTick = now + Mathf.CeilToInt(Mathf.Clamp(pingCooldown, .5f, 5) / Runner.DeltaTime);
            if (!float.IsFinite(point.sqrMagnitude) || intent is not (TeamPingKind.Location or TeamPingKind.Danger or TeamPingKind.Retreat))
            {
                ServerActionAudit.Record(Runner, info.Source, ServerRejection.PingPayload);
                return;
            }
            while (pingExpirations.Count != 0 && pingExpirations.Peek() <= now) pingExpirations.Dequeue();
            if (pingExpirations.Count >= Mathf.Clamp(maxActivePings, 1, 4)
                || !TeamPings.TryResolve(this, intent, point, target, out var kind, out var position)) return;
            int expires = now + Mathf.CeilToInt(Mathf.Clamp(pingLifetime, 2, 15) / Runner.DeltaTime);
            pingExpirations.Enqueue(expires);
            foreach (var member in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(member, out var actor) || !NetworkParticipant.IsAdmitted(actor)
                    || !actor.TryGetComponent<NetworkTestPlayer>(out var recipient) || recipient.Team != Team) continue;
                recipient.RPC_ReceiveTeamPing(member, round, Team, ParticipantNumber, kind, position, expires);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_ReceiveTeamPing([RpcTarget] PlayerRef recipient, int round, TeamId team, int source,
            TeamPingKind kind, Vector3 position, int expires)
        {
            if (!HasInputAuthority || recipient != Runner.LocalPlayer || Team != team || IsSuspended
                || expires <= Runner.Tick.Raw) return;
            var match = NetworkMatchPacing.MatchFor(Runner);
            if (match == null || !match.CanPlay || match.RoundNumber != round) return;
            PruneTeamPings(round);
            // Six participants, at most four concurrent pings per participant.
            if (receivedPings.Count < 24)
                receivedPings.Add(new TeamPing(round, team, source, kind, position, expires));
        }

        internal IReadOnlyList<TeamPing> ReadTeamPings(int round)
        {
            PruneTeamPings(round);
            return receivedPings;
        }

        private void PruneTeamPings(int round)
        {
            for (int i = receivedPings.Count - 1; i >= 0; i--)
            {
                var ping = receivedPings[i];
                if (ping.Round != round || ping.Team != Team || ping.Expires <= Runner.Tick.Raw || IsSuspended)
                    receivedPings.RemoveAt(i);
            }
        }
    }
}
