using System.Collections.Generic;
using Fusion;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Player;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.UI.Minimap;
using HeroesOfDragonland.World.Camps;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    public sealed partial class GameplayHudPresenter
    {
        private readonly Dictionary<NetworkId, int> campCompletions = new();
        private NetworkChestInteractor chest;
        private NetworkInventory deployables;
        private LocalGameplayInputGate inputGate;
        private int lastLevel, contextRound = -1;
        private float kingAHealth = -1, kingBHealth = -1, kingWarningUntil, nextCampCheck, feedbackUntil;
        private bool lowStamina, feedbackWarning;
        private string contextMessage = "";
        private enum Notice { None, Hint, Progress, Warning }
        private Notice priority;
        private string deferredProgress;

        private void BindContext(NetworkObject value)
        {
            if (chest != null) chest.InteractionResolved -= OnInteraction;
            chest = value != null ? value.GetComponent<NetworkChestInteractor>() : null;
            if (chest != null) chest.InteractionResolved += OnInteraction;
            if (deployables != null) deployables.DeployableResolved -= OnInteraction;
            deployables = value != null ? value.GetComponent<NetworkInventory>() : null;
            if (deployables != null) deployables.DeployableResolved += OnInteraction;
            inputGate = bootstrap != null ? bootstrap.GetComponent<LocalGameplayInputGate>() : null;
            lastLevel = 0;
            lowStamina = false;
            ResetContext();
        }

        private void ResetContext()
        {
            contextRound = -1;
            campCompletions.Clear();
            nextCampCheck = kingWarningUntil = feedbackUntil = 0;
            priority = Notice.None;
            contextMessage = "";
            deferredProgress = null;
        }

        private void ObserveLevel(int level)
        {
            if (lastLevel > 0 && level > lastLevel)
                ShowNotice("Level " + level + " reached · Check skills [K]", Notice.Progress, 4);
            lastLevel = level;
        }

        private void OnLockFailed() => ShowNotice("No target in view · Aim at a nearby enemy", Notice.Hint, 2.5f);
        private void OnInteraction(bool accepted, string message) => ShowNotice(message, accepted ? Notice.Progress : Notice.Warning, 4);

        private void ShowNotice(string message, Notice importance, float duration)
        {
            if (match == null || !match.CanPlay || string.IsNullOrEmpty(message)) return;
            float now = Time.unscaledTime;
            if (now < feedbackUntil)
            {
                if (message == contextMessage) return;
                if (importance == Notice.Progress && priority >= Notice.Progress)
                {
                    deferredProgress = message;
                    return;
                }
                if (importance < priority) return;
                if (priority == Notice.Progress && importance == Notice.Warning) deferredProgress = contextMessage;
            }
            contextMessage = message;
            feedbackWarning = importance == Notice.Warning;
            priority = importance;
            feedbackUntil = now + duration;
        }

        private void SetKingBaseline(bool isA, float hp)
        {
            if (isA) kingAHealth = hp;
            else kingBHealth = hp;
        }

        private void ObserveKing(bool isA, float hp, float max)
        {
            float previous = isA ? kingAHealth : kingBHealth;
            SetKingBaseline(isA, max > 0 ? hp : -1);
            if (localTeam == (isA ? TeamId.TeamA : TeamId.TeamB) && match != null && match.CanPlay
                && max > 0 && previous >= 0 && hp > 0 && hp < previous)
                kingWarningUntil = Time.unscaledTime + 6;
        }

        private void RefreshContext()
        {
            bool playing = actor != null && actor.IsValid && stats != null && match != null && match.CanPlay;
            bool alive = playing && !stats.IsDead;
            float now = Time.unscaledTime;
            if (playing && contextRound != match.RoundNumber)
            {
                ResetContext();
                contextRound = match.RoundNumber;
            }
            if (playing && now >= nextCampCheck)
            {
                nextCampCheck = now + .15f;
                ObserveCamps(alive);
            }
            if (!alive) deferredProgress = null;
            if (alive && now >= feedbackUntil && deferredProgress != null)
            {
                string message = deferredProgress;
                deferredProgress = null;
                ShowNotice(message, Notice.Progress, 3);
            }
            lowStamina = alive && stats.MaxStamina > 0
                && stats.CurrentStamina / stats.MaxStamina <= (lowStamina ? .32f : .22f);
            view.SetStaminaWarning(lowStamina);
            view.SetKingWarning(playing && now < kingWarningUntil);
            bool showContext = alive && (inputGate == null || !inputGate.IsBlocked);
            view.SetContext(showContext && now < feedbackUntil ? contextMessage : "",
                showContext && chest != null ? chest.Prompt : "", feedbackWarning);
        }

        private void ObserveCamps(bool alive)
        {
            foreach (var subject in TacticalMapSubject.Active)
            {
                if (subject == null || !subject.Valid || subject.Camp == null || subject.Actor.Runner != actor.Runner) continue;
                var camp = subject.Camp;
                int count = camp.CompletionCount;
                bool newlyCleared = campCompletions.TryGetValue(subject.Actor.Id, out int previous) && count > previous;
                campCompletions[subject.Actor.Id] = count;
                if (!newlyCleared || !alive || (actor.transform.position - camp.transform.position).sqrMagnitude > 18 * 18) continue;
                ShowNotice(camp.Definition?.Difficulty == CampDifficulty.Contested
                    ? "Contested boss defeated · Look for the loot chest"
                    : "Camp cleared · Look for the loot chest", Notice.Progress, 4);
            }
        }
    }
}
