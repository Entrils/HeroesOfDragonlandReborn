using System.Linq;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(GameplayHudPresenter), typeof(GameplayHudView))]
    public sealed class MatchPacingPresenter : MonoBehaviour
    {
        [SerializeField] private MultiplayerBootstrap bootstrap;
        private GameplayHudView view;
        private float nextRefresh;
        private void Awake() => view = GetComponent<GameplayHudView>();
        private void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            var match = bootstrap != null ? bootstrap.GetComponent<NetworkFortressSpawner>()?.Match : null;
            if (match == null || match.Object == null || !match.Object.IsValid || match.Balance == null || !match.CanPlay)
            { view.SetPacing(""); return; }
            var config = match.Balance;
            var pacing = match.GetComponent<NetworkMatchPacing>();
            bool late = config.IsLate(match.PlayingSeconds);
            var camps = FindObjectsByType<NetworkCamp>().Where(c => c.Object != null && c.Object.IsValid && c.Runner == match.Runner).ToArray();
            string objective = string.Join(" / ", camps.Where(c => c.Definition.Difficulty == CampDifficulty.Contested)
                .Select(c => (c.State == NetworkCampState.Active ? "WARDEN ACTIVE" : "WARDEN IN " + Mathf.CeilToInt(c.NextSpawnTimer.RemainingTime(match.Runner) ?? 0) + "s") + " / " + c.AvailableExperience + " XP"));
            string opportunity = late ? "LATE OPPORTUNITY: flank and Warden XP increased."
                : "Flank + Warden surge in " + Mathf.CeilToInt(Mathf.Max(0, config.LateMatchStartsAtSeconds - match.PlayingSeconds)) + "s.";
            string bonuses = pacing != null ? "Camp catch-up: A +" + Mathf.RoundToInt((pacing.CampMultiplier(TeamId.TeamA) - 1) * 100)
                + "% / B +" + Mathf.RoundToInt((pacing.CampMultiplier(TeamId.TeamB) - 1) * 100) + "%." : "";
            view.SetPacing(objective + "\n" + opportunity + "\n" + bonuses + "\nCamp XP: nearby surviving contributors of the higher-damage team. Loot stays contested."
                + "\nKill " + config.KillExperience + " / assist pool " + config.AssistExperiencePool + " XP; same victim " + config.RepeatedVictimRewardCooldown + "s."
                + "\nDeath XP loss " + config.DeathProgressLossPercent + "% (max " + config.MaximumDeathExperienceLoss + "). Keep levels/gear.");
        }
        private void OnDisable() { if (view != null) view.SetPacing(""); }
    }
}
