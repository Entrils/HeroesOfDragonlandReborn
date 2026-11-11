using System;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Skills
{
    [DisallowMultipleComponent, RequireComponent(typeof(SessionSkills), typeof(NetworkSessionProgression))]
    public sealed class NetworkSessionSkills : NetworkBehaviour, ISessionSkillAuthority
    {
        [Networked] public NetworkString<_32> SessionId { get; private set; }
        [Networked] public int SkillPoints { get; private set; }
        [Networked] public int LearnedMask { get; private set; }
        [Networked] public float GuardRemaining { get; private set; }
        [Networked, Capacity(SessionSkills.SkillCount)] public NetworkArray<float> Cooldowns => default;
        [Networked] public int Revision { get; private set; }
        private SessionSkills skills;
        private int appliedRevision;
        private float nextRequestTime;
        private bool IsServerOwner => isActiveAndEnabled && NetworkParticipant.IsAdmitted(Object);
        public bool CanMutateSkills => IsServerOwner && NetworkMatch.AllowsGameplay(Runner);
        public bool CanRequest => isActiveAndEnabled && Object != null && Object.IsValid && HasInputAuthority
            && Runner.TryGetPlayerObject(Runner.LocalPlayer, out var actor) && actor == Object && NetworkMatch.AllowsGameplay(Runner);
        public event Action<bool, string> RequestResolved;
        private void Awake() => skills = GetComponent<SessionSkills>();
        public override void Spawned()
        {
            if (!Runner.IsServer || !HasStateAuthority) return;
            skills.Changed += Publish; Publish();
        }
        private void Publish()
        {
            if (Object == null || !Object.IsValid || !Runner.IsServer || !HasStateAuthority) return;
            SessionId = skills.SessionId ?? ""; SkillPoints = skills.SkillPoints; LearnedMask = skills.LearnedMask;
            GuardRemaining = skills.GuardRemaining;
            for (int i = 0; i < SessionSkills.SkillCount; i++) Cooldowns.Set(i, skills.CooldownRemaining(i));
            Revision++;
        }
        public override void FixedUpdateNetwork() { if (CanMutateSkills) skills.Tick(Runner.DeltaTime); }
        public bool RequestLearn(int index) => Request(index, true);
        public bool RequestActivate(int index) => Request(index, false);
        private bool Request(int index, bool learn)
        {
            if (!CanRequest || index < 0 || index >= SessionSkills.SkillCount || string.IsNullOrEmpty(skills.SessionId)) return false;
            RPC_Request(index, learn, skills.SessionId); return true;
        }
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_Request(int index, bool learn, NetworkString<_32> sessionId, RpcInfo info = default)
        {
            if (!IsServerOwner || info.Source != Object.InputAuthority) return;
            string reason = "Skills are unavailable outside an active match.";
            bool accepted = false;
            float now = (float)Runner.SimulationTime;
            if (now < nextRequestTime) reason = "Please wait before requesting another skill action.";
            else
            {
                nextRequestTime = now + .15f;
                if (CanMutateSkills) accepted = learn ? skills.TryLearn(index, sessionId.ToString(), out reason)
                    : skills.TryActivate(index, sessionId.ToString(), out reason);
            }
            RPC_Result(accepted, accepted ? (learn ? "Skill learned." : "Skill activated.") : reason);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_Result(bool accepted, string reason) => RequestResolved?.Invoke(accepted, reason);
        public override void Render()
        {
            if (HasStateAuthority || Revision == 0 || appliedRevision == Revision) return;
            var remaining = new float[SessionSkills.SkillCount];
            for (int i = 0; i < remaining.Length; i++) remaining[i] = Cooldowns[i];
            skills.ApplySnapshot(SessionId.ToString(), SkillPoints, LearnedMask, GuardRemaining, remaining); appliedRevision = Revision;
        }
        public override void Despawned(NetworkRunner runner, bool hasState) => skills.Changed -= Publish;
    }
}
