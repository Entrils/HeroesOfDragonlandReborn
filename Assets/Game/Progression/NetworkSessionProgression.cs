using System;
using Fusion;
using HeroesOfDragonland.Items;
using HeroesOfDragonland.Progression;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(SessionProgression))]
    public sealed class NetworkSessionProgression : NetworkBehaviour, IProgressionAuthority
    {
        [Networked] public NetworkString<_32> SessionId { get; private set; }
        [Networked] public int Level { get; private set; }
        [Networked] public long CurrentXP { get; private set; }
        [Networked] public long Points { get; private set; }
        [Networked] public long Revision { get; private set; }
        [Networked, Capacity(5)] public NetworkArray<int> Attributes => default;
        private SessionProgression progression;
        private static readonly ItemStat[] attributeKinds = { ItemStat.Vitality, ItemStat.Endurance, ItemStat.Mind, ItemStat.Strength, ItemStat.Dexterity };
        public bool CanMutateProgression => isActiveAndEnabled && Object != null && Object.IsValid
            && Runner.IsServer && HasStateAuthority && Object.InputAuthority != PlayerRef.None;
        public bool IsAdmitted => CanMutateProgression
            && Runner.TryGetPlayerObject(Object.InputAuthority, out var player) && player == Object;
        private void Awake() => progression = GetComponent<SessionProgression>();
        public override void Spawned()
        {
            if (!CanMutateProgression) return;
            var bootstrap = Runner.GetComponentInParent<MultiplayerBootstrap>();
            if (bootstrap == null || !progression.TryBeginSession(bootstrap.SessionId, out _))
                throw new InvalidOperationException("Network player needs a valid server progression session/curve.");
            SessionId = progression.SessionId;
            progression.Changed += Publish;
            Publish();
        }
        private void Publish()
        {
            if (!CanMutateProgression) return;
            Level = progression.Level; CurrentXP = progression.CurrentXP; Points = progression.ProgressionPoints;
            for (int i = 0; i < attributeKinds.Length; i++) Attributes.Set(i, progression.GetAllocatedPoints(attributeKinds[i]));
            Revision = progression.Revision + 1;
        }
        public bool TryGrantExperience(int amount, string rewardId)
            => IsAdmitted && progression.TryAddExperience(amount, SessionId.ToString(), rewardId, out _);
        public override void Render()
        {
            if (HasStateAuthority || Revision == 0 || (progression.IsInitialized && progression.Revision >= Revision)) return;
            var values = new int[5];
            for (int i = 0; i < values.Length; i++) values[i] = Attributes[i];
            progression.ApplyNetworkState(SessionId.ToString(), Level, CurrentXP, Points, Revision, values);
        }
        public override void Despawned(NetworkRunner runner, bool hasState) => progression.Changed -= Publish;
    }
}
