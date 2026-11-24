#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using HeroesOfDragonland.Profiles;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    internal sealed class RankedMatchmakingProbe : MonoBehaviour
    {
        private int finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-hodrVerifyRanked") && LocalPlayerProfile.IsTestProfile)
            {
                var profile = LocalPlayerProfile.Current;
                var rank = profile.Data.Ranked;
                bool won = rank.Wins == 1;
                if (rank.MatchesPlayed != 1 || rank.Wins + rank.Losses != 1 || !rank.Provisional
                    || rank.Rating != (won ? 1025 : 975)
                    || profile.RecordRound(profile.Data.RecordedRounds[0], won, 10f,
                        new MatchStatistics { HighestLevel = 1 }, RankedRatingSettings.Default))
                    throw new InvalidOperationException("[RankedCheck] FAIL persisted rating or duplicate receipt rejection");
                Debug.Log("[RankedCheck] PASS persisted profile and duplicate receipt rejection");
                Application.Quit();
            }
            if (args.Contains("-hodrRankedCheck"))
                FindAnyObjectByType<MultiplayerBootstrap>()?.gameObject.AddComponent<RankedMatchmakingProbe>();
        }

        private void Update()
        {
            var bootstrap = GetComponent<MultiplayerBootstrap>();
            var match = GetComponent<NetworkFortressSpawner>().Match;
            if (match == null || !match.IsServerOwner || !bootstrap.IsRankedQueue || !match.CanPlay
                || match.PlayingSeconds < 1 || finished == match.RoundNumber || match.RoundNumber > 2) return;
            finished = match.RoundNumber;
            if (finished == 2)
            {
                var participant = bootstrap.Avatars.First(a => a.InputAuthority != match.Runner.LocalPlayer);
                participant.GetComponent<NetworkTestPlayer>().SetSuspended(true);
            }
            if (!match.Runner.TryFindObject(match.TeamBKingId, out var king)
                || king.GetComponent<CharacterStats>().ApplyDamage(float.MaxValue) <= 0)
                throw new InvalidOperationException("[RankedCheck] FAIL authoritative finish fixture");
        }
    }
}
#endif
