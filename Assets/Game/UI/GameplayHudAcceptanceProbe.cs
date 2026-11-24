using UnityEngine;
#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using System.Reflection;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Progression;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.UI
{
    // Opt-in acceptance; the ordinary HUD never changes gameplay.
    [DefaultExecutionOrder(200)]
    public sealed class GameplayHudAcceptanceProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        private bool active, targetChecked, sawProgress, sawResources, sawRespawn, sawResult, resourcesSeeded, sawLowStamina, sawKingWarning;
        private int boundAvatars;
        private Fusion.NetworkObject previous;
        private float readyAt;
        private float playingAt = -1;
        private string screenshotPath;
        private GameplayHudPresenter presenter;
        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            active = args.Contains("-hodrHud");
            int capture = Array.IndexOf(args, "-hodrHudScreenshot");
            if (capture >= 0 && capture + 1 < args.Length && !Application.isBatchMode) screenshotPath = args[capture + 1];
            presenter = GetComponent<GameplayHudPresenter>();
        }
        private void Check(bool ok, string label)
        {
            if (!ok) { active = false; throw new InvalidOperationException("[HudCheck] FAIL " + label); }
        }
        private static float Ratio(float current, float max) => max > 0 ? Mathf.Clamp01(current / max) : 0;
        private void LateUpdate()
        {
            if (screenshotPath != null && presenter.LocalActor is { IsValid: true } viewer
                && NetworkMatchPacing.MatchFor(viewer.Runner) is { State: MatchPhase.Playing })
            {
                ScreenCapture.CaptureScreenshot(screenshotPath);
                screenshotPath = null;
            }
            if (!active) return;
            var view = presenter.View;
            var bootstrap = GetComponentInParent<MultiplayerBootstrap>();
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning) { Check(!view.HasLocalPlayer, "disconnected-private-ui-hidden"); return; }
            if (runner.LocalPlayer == Fusion.PlayerRef.None) { Check(!view.HasLocalPlayer, "dedicated-server-private-ui-hidden"); return; }
            var actor = presenter.LocalActor;
            if (actor == null) { Check(!view.HasLocalPlayer, "missing-avatar-hidden"); return; }
            Check(actor.IsValid && actor.HasInputAuthority && actor.InputAuthority == runner.LocalPlayer, "only-local-owner-bound");
            if (previous != actor) { previous = actor; boundAvatars++; readyAt = Time.realtimeSinceStartup; targetChecked = false; }
            if (Time.realtimeSinceStartup - readyAt < 1) return;
            var stats = actor.GetComponent<CharacterStats>();
            var progression = actor.GetComponent<SessionProgression>();
            Check(view.HasLocalPlayer && Mathf.Approximately(view.HealthValue, Ratio(stats.CurrentHealth, stats.MaxHealth)), "health-event-value");
            Check(Mathf.Approximately(view.StaminaValue, Ratio(stats.CurrentStamina, stats.MaxStamina)), "private-stamina-event-value");
            Check(Mathf.Approximately(view.ManaValue, Ratio(stats.CurrentMana, stats.MaxMana)), "private-mana-event-value");
            Check(view.IdentityText.Contains("Level " + progression.Level), "level-event-value");
            Check(Mathf.Approximately(view.ExperienceValue, progression.IsMaximumLevel ? 1 : Ratio(progression.CurrentXP, progression.XPRequiredForNextLevel)), "xp-event-value");
            if (progression.Level > 1) sawProgress = true;
            if (stats.CurrentMana < stats.MaxMana && stats.CurrentStamina < stats.MaxStamina) sawResources = true;
            var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (match == null || !match.Object.IsValid) return;
            if (match.State == MatchPhase.Playing)
            {
                if (playingAt < 0) playingAt = Time.realtimeSinceStartup;
                Check(view.ObjectiveText == "Kill the enemy King.", "active-objective-summary");
                if (Application.isBatchMode) Check(!view.HasObjectiveSummary, "objective-details-collapsed");
                sawLowStamina |= !string.IsNullOrEmpty(view.StaminaWarningText);
                if (!string.IsNullOrEmpty(view.KingWarningText))
                {
                    var threatenedTeam = match.RoundNumber == 1 ? TeamId.TeamA : TeamId.TeamB;
                    Check(actor.GetComponent<NetworkTestPlayer>().Team == threatenedTeam, "enemy-king-warning-hidden");
                    sawKingWarning = true;
                }
            }
            if (match.State == MatchPhase.Playing && !targetChecked)
            {
                var target = FindObjectsByType<Targetable>().FirstOrDefault(t => t.IsValid && t.GetComponentInParent<NetworkCharacterStats>() != null);
                Check(target != null, "available-lock-target");
                var targetLock = actor.GetComponent<TargetLock>();
                typeof(TargetLock).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(targetLock, new object[] { target });
                Check(view.HasTarget && Mathf.Approximately(view.TargetHealthValue, Ratio(target.GetComponentInParent<CharacterStats>().CurrentHealth, target.GetComponentInParent<CharacterStats>().MaxHealth)), "local-target-event-and-public-health");
                targetLock.Unlock();
                Check(!view.HasTarget, "unlock-clears-target");
                targetChecked = true;
            }
            if (runner.IsServer && match.State == MatchPhase.Playing && !resourcesSeeded
                && playingAt >= 0 && Time.realtimeSinceStartup - playingAt >= 1.75f)
            {
                resourcesSeeded = true;
                // Exercise owner-only resource replication for both host and client.
                foreach (var player in runner.ActivePlayers)
                    if (runner.TryGetPlayerObject(player, out var obj) && obj != null)
                    {
                        var resource = obj.GetComponent<CharacterStats>();
                        if (!resource.IsDead) { resource.TrySpendMana(25); resource.TrySpendStamina(resource.CurrentStamina); }
                    }
                Check(runner.TryFindObject(match.TeamAKingId, out var king)
                    && king.GetComponent<CharacterStats>().ApplyDamage(3) > 0, "seed-defending-king-warning");
            }
            var respawn = actor.GetComponent<NetworkPlayerRespawn>();
            if (respawn.WaitingForRespawn && respawn.SecondsUntilRespawn > 1)
            { Check(view.RespawnText.StartsWith("Respawning in "), "local-respawn-countdown"); sawRespawn = true; }
            if (match.State == MatchPhase.Finished)
            {
                Check(!string.IsNullOrEmpty(view.ResultText), "replicated-match-result");
                Check(!view.HasObjectiveSummary && string.IsNullOrEmpty(view.KingWarningText), "finished-notices-clear");
                Check(match.Winner == HeroesOfDragonland.Teams.TeamId.TeamA ? view.KingBValue == 0 : view.KingAValue == 0, "defeated-king-health");
                sawResult = true;
                bool respawnRun = Environment.GetCommandLineArgs().Contains("-hodrRespawn");
                if ((respawnRun && sawRespawn) || (!respawnRun && match.RoundNumber == 2))
                {
                    Check(sawProgress && sawResources && sawResult && sawLowStamina, "live-level-resource-result-updates");
                    Check(actor.GetComponent<NetworkTestPlayer>().Team != TeamId.TeamA || sawKingWarning, "own-king-warning-observed");
                    Check(respawnRun || boundAvatars >= 2, "round-reset-rebind");
                    Debug.Log("[HudComplete] server=" + runner.IsServer + " respawn=" + respawnRun);
                    active = false;
                }
            }
        }
#endif
    }
}
