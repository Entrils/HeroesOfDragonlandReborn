using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Presentation.Vfx;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Audio
{
    [DefaultExecutionOrder(230), DisallowMultipleComponent]
    public sealed class WorldAudioPresentation : MonoBehaviour
    {
        private NetworkObject actor;
        private NetworkCamp camp;
        private NetworkLootChest chest;
        private NetworkMatch match;
        private readonly VfxSequence completions = new(), finishes = new();
        private bool seeded, open;
        private MatchPhase phase;
        private int round = -1;
        private void Awake() { actor = GetComponent<NetworkObject>(); camp = GetComponent<NetworkCamp>(); chest = GetComponent<NetworkLootChest>(); match = GetComponent<NetworkMatch>(); }
        private void OnEnable() => ResetBaseline();
        private void ResetBaseline() { seeded = false; round = -1; completions.Reset(); finishes.Reset(); }
        private void LateUpdate()
        {
            if (actor == null || !actor.IsValid) { ResetBaseline(); return; }
            var service = AudioAudience.Service(actor.Runner);
            bool startedMatch = match != null && seeded && phase != MatchPhase.Playing && match.State == MatchPhase.Playing;
            var activeMatch = match != null ? match : NetworkMatchPacing.MatchFor(actor.Runner);
            if (activeMatch != null && activeMatch.Object != null && activeMatch.Object.IsValid && activeMatch.RoundNumber != round)
            { ResetBaseline(); round = activeMatch.RoundNumber; }
            if (camp != null && completions.Observe(camp.CompletionCount)) service?.Play(AudioCue.CampComplete, transform.position, this);
            if (chest != null)
            {
                if (seeded && !open && chest.IsOpen) service?.Play(AudioCue.Chest, transform.position, this);
                open = chest.IsOpen;
            }
            if (match != null)
            {
                // Starting -> Playing is a live transition. Joining an ongoing match remains silent.
                if (startedMatch) service?.Play(AudioCue.MatchStart, transform.position, this);
                if (finishes.Observe(match.FinishCount) && match.MatchEnded)
                {
                    var team = AudioAudience.LocalTeam(actor.Runner);
                    if (team != TeamId.Unassigned && match.Winner != TeamId.Unassigned)
                        service?.Play(team == match.Winner ? AudioCue.Victory : AudioCue.Defeat, transform.position, this);
                }
                phase = match.State;
            }
            seeded = true;
        }
    }
}
