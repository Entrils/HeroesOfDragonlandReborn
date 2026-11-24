using Fusion;
using HeroesOfDragonland.Presentation;
using HeroesOfDragonland.Cosmetics;
using HeroesOfDragonland.Profiles;

namespace HeroesOfDragonland.Multiplayer
{
    public sealed partial class NetworkTestPlayer
    {
        private const uint AppearanceSubmittedFlag = 1u << 16;
        [Networked] public uint AppearanceState { get; private set; }
        [Networked] public uint CosmeticState { get; private set; }
        private bool sentAppearance;
        public CosmeticVisualLoadout Cosmetics
        {
            get
            {
                CosmeticVisualLoadout.TryDecode(CosmeticState, out var loadout);
                return loadout;
            }
        }
        public bool HasSubmittedAppearance => (AppearanceState & AppearanceSubmittedFlag) != 0;
        public CharacterAppearance Appearance
        {
            get
            {
                CharacterAppearance.TryDecode((int)(AppearanceState & 0xffff), out var appearance);
                return appearance;
            }
        }

        private void SubmitLocalAppearance()
        {
            if (sentAppearance || HasSubmittedAppearance || IsBot || !HasInputAuthority || IsSuspended
                || !Runner.TryGetPlayerObject(Runner.LocalPlayer, out var localActor) || localActor != Object) return;
            sentAppearance = true;
            var profile = LocalPlayerProfile.Current;
            RPC_SubmitAppearance((ushort)profile.EffectiveAppearance.Code, profile.CosmeticItems.VisualLoadout.Packed);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_SubmitAppearance(ushort code, uint cosmetics, RpcInfo info = default)
        {
            if (HasSubmittedAppearance || IsBot || info.Source != Object.InputAuthority || !NetworkParticipant.IsAdmitted(Object)
                || !CharacterAppearance.TryDecode(code, out _) || !CosmeticVisualLoadout.TryDecode(cosmetics, out _)) return;
            // The local collection enforces ownership. The session validates catalog indices and carries visual state only.
            CosmeticState = cosmetics;
            AppearanceState = AppearanceSubmittedFlag | code;
        }
    }
}
