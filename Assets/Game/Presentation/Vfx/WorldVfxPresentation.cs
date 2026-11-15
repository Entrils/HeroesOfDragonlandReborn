using Fusion;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.Presentation.Vfx
{
    [DefaultExecutionOrder(220), DisallowMultipleComponent]
    public sealed class WorldVfxPresentation : MonoBehaviour
    {
        private NetworkObject actor;
        private NetworkCamp camp;
        private NetworkLootChest chest;
        private NetworkMatch match;
        private readonly VfxSequence completions = new(), finishes = new();
        private bool seededOpen, wasOpen;
        private int round = -1;
        private void Awake() { actor = GetComponent<NetworkObject>(); camp = GetComponent<NetworkCamp>(); chest = GetComponent<NetworkLootChest>(); match = GetComponent<NetworkMatch>(); }
        private void OnEnable()
        {
            completions.Reset(); finishes.Reset(); seededOpen = false; round = -1;
            if (chest != null) chest.OpenResolved += OpenResult;
        }
        private void OnDisable() { if (chest != null) chest.OpenResolved -= OpenResult; }
        private void OpenResult(bool accepted, string message)
        {
            if (!accepted || actor == null || !actor.IsValid) return;
            if (actor.Runner.LocalPlayer != PlayerRef.None && actor.Runner.TryGetPlayerObject(actor.Runner.LocalPlayer, out var local) && local != null)
                VfxAudience.Pool(actor.Runner)?.Play(VfxKind.ItemPickup, local.transform.position, VfxRelation.Friendly);
        }
        private void LateUpdate()
        {
            if (actor == null || !actor.IsValid) return;
            var pool = VfxAudience.Pool(actor.Runner);
            if (pool == null) return;
            var currentMatch = match != null ? match : NetworkMatchPacing.MatchFor(actor.Runner);
            if (currentMatch != null && currentMatch.Object != null && currentMatch.Object.IsValid && currentMatch.RoundNumber != round)
            { round = currentMatch.RoundNumber; completions.Reset(); finishes.Reset(); seededOpen = false; }
            if (camp != null && completions.Observe(camp.CompletionCount)) pool.Play(VfxKind.CampCleared, transform.position + Vector3.up * .6f);
            if (chest != null)
            {
                if (!seededOpen) { wasOpen = chest.IsOpen; seededOpen = true; }
                else
                {
                    if (!wasOpen && chest.IsOpen) pool.Play(VfxKind.ChestOpening, transform.position + Vector3.up * .5f);
                    wasOpen = chest.IsOpen;
                }
            }
            if (match != null && finishes.Observe(match.FinishCount) && match.MatchEnded
                && actor.Runner.LocalPlayer != PlayerRef.None && actor.Runner.TryGetPlayerObject(actor.Runner.LocalPlayer, out var player) && player != null && VfxAudience.Team(player.gameObject) == match.Winner)
                pool.Play(VfxKind.Victory, player.transform.position + Vector3.up * .8f, VfxRelation.Friendly);
        }
    }
}
