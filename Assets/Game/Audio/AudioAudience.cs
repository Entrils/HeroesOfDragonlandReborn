using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;

namespace HeroesOfDragonland.Audio
{
    public static class AudioAudience
    {
        public static GameAudioService Service(NetworkRunner runner) => runner != null
            ? runner.GetComponentInParent<MultiplayerBootstrap>()?.GetComponent<GameAudioService>() : null;
        public static TeamId LocalTeam(NetworkRunner runner)
        {
            return runner != null && runner.LocalPlayer != PlayerRef.None && runner.TryGetPlayerObject(runner.LocalPlayer, out var local)
                && local != null && local.IsValid ? VfxAudience.Team(local.gameObject) : TeamId.Unassigned;
        }
        public static bool Action(VfxSequence sequence, NetworkObject actor, int number, int tick, bool busy, float duration)
        {
            double now = actor.HasStateAuthority ? actor.Runner.LocalRenderTime : actor.Runner.RemoteRenderTime;
            double age = now - tick * (double)actor.Runner.DeltaTime;
            // A proxy may receive a new action before its delayed render timeline reaches it.
            // Defer consumption so that action can still play when the render clock catches up.
            if (busy && age < 0) return false;
            return sequence.Observe(number) && busy && age >= 0 && age < duration;
        }
    }
}
