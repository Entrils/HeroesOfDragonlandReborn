using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Progression
{
    [DisallowMultipleComponent]
    public sealed class SessionProgression : MonoBehaviour
    {
        [SerializeField] private LevelProgressionDefinition definition;
        [SerializeField] private bool hasAuthority = true;
        private readonly HashSet<string> rewardedIds = new HashSet<string>(StringComparer.Ordinal);
        private int[] thresholds;
        private int[] pointRewards;
        private bool processing;
        public bool HasAuthority => hasAuthority;
        public bool IsInitialized => thresholds != null;
        public string SessionId { get; private set; }
        public int Level { get; private set; } = 1;
        public long CurrentXP { get; private set; }
        public long ProgressionPoints { get; private set; }
        public int MaximumLevel => thresholds == null ? 1 : thresholds.Length + 1;
        public bool IsMaximumLevel => IsInitialized && Level == MaximumLevel;
        public int XPRequiredForNextLevel => !IsInitialized || IsMaximumLevel ? 0 : thresholds[Level - 1];
        public long Revision { get; private set; }
        public event Action<string> SessionStarted;
        public event Action<long> ExperienceGained;
        public event Action<LevelUpEvent> LeveledUp;
        public event Action Changed;

        public bool SetAuthority(bool value)
        {
            if (processing) return false;
            hasAuthority = value;
            return true;
        }
        public bool CanBeginSession(out string reason)
        {
            reason = null;
            if (!hasAuthority || processing) reason = "Session reset requires available authority.";
            else if (definition == null) reason = "Assign a level progression definition.";
            else if (!definition.TryValidate(out reason)) return false;
            return reason == null;
        }

        // The match host uses the same unique ID for every participant. Repeating
        // that ID is idempotent; a different ID is the explicit match reset boundary.
        public bool TryBeginSession(string sessionId, out string reason)
        {
            if (!CanBeginSession(out reason)) return false;
            if (string.IsNullOrWhiteSpace(sessionId)) { reason = "Session ID is required."; return false; }
            if (IsInitialized && SessionId == sessionId) return true;
            processing = true;
            try
            {
                definition.CopyCurve(out thresholds, out pointRewards);
                SessionId = sessionId;
                Level = 1; CurrentXP = 0; ProgressionPoints = 0; Revision = 0;
                rewardedIds.Clear();
                Notify(SessionStarted, sessionId);
                Notify(Changed);
                return true;
            }
            finally { processing = false; }
        }

        // All XP sources share this boundary. IDs are unique per player/session,
        // e.g. camp:<campId>, quest:<questId> or kill:<server-issued-deathId>.
        // A session ID rejects delayed rewards from a previous match.
        public bool TryAddExperience(long amount, string sessionId, string rewardId, out string reason)
        {
            reason = null;
            if (!hasAuthority || processing) reason = "XP grants require available authority.";
            else if (!IsInitialized || sessionId != SessionId) reason = "XP reward belongs to an inactive session.";
            else if (amount <= 0 || string.IsNullOrWhiteSpace(rewardId)) reason = "Provide positive XP and a unique reward ID.";
            else if (rewardedIds.Contains(rewardId)) reason = "This reward was already received in this session.";
            else if (IsMaximumLevel) reason = "Maximum level reached.";
            if (reason != null) return false;

            processing = true;
            try
            {
                long remaining = amount;
                var levelEvents = new List<LevelUpEvent>();
                // Subtract thresholds instead of adding to XP: even long.MaxValue
                // rewards cannot overflow. Work is bounded by the configured level cap.
                while (!IsMaximumLevel)
                {
                    long needed = XPRequiredForNextLevel - CurrentXP;
                    if (remaining < needed) { CurrentXP += remaining; break; }
                    remaining -= needed;
                    int points = pointRewards[Level - 1];
                    Level++;
                    CurrentXP = 0;
                    ProgressionPoints += points;
                    levelEvents.Add(new LevelUpEvent(Level, points, ProgressionPoints));
                    if (remaining == 0) break;
                }
                rewardedIds.Add(rewardId);
                Revision++;
                // Observers see the fully committed grant. One level event is emitted
                // per crossed threshold, in order, with that step's points/total.
                Notify(ExperienceGained, amount);
                foreach (var levelEvent in levelEvents) Notify(LeveledUp, levelEvent);
                Notify(Changed);
                return true;
            }
            finally { processing = false; }
        }

        private void Notify(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
