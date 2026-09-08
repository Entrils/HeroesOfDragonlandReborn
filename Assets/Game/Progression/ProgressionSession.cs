using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.Progression
{
    // Establish the scene's match before camp/source Awake/Start callbacks.
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class ProgressionSession : MonoBehaviour
    {
        [SerializeField] private SessionProgression[] players = Array.Empty<SessionProgression>();
        private bool processing;
        public string SessionId { get; private set; }
        private void Awake()
        {
            if (!TryStartSession(Guid.NewGuid().ToString("N"), out string reason)) Debug.LogError(reason, this);
        }

        public bool TryStartSession(string sessionId, out string reason)
        {
            reason = null;
            if (processing) { reason = "A session reset is already running."; return false; }
            if (string.IsNullOrWhiteSpace(sessionId) || players == null || players.Length == 0)
            { reason = "Provide a session ID and player roster."; return false; }
            if (SessionId == sessionId) return true;
            var unique = new HashSet<SessionProgression>();
            foreach (var player in players)
            {
                if (player == null || !unique.Add(player)) { reason = "Player roster contains a missing or duplicate participant."; return false; }
                if (!player.CanBeginSession(out reason)) return false;
            }
            processing = true;
            try
            {
                foreach (var player in players)
                    if (!player.TryBeginSession(sessionId, out reason)) return false;
                SessionId = sessionId;
                return true;
            }
            finally { processing = false; }
        }

        [ContextMenu("Progression/Start New Session")]
        private void StartNewSession()
        {
            if (!Application.isPlaying) return;
            if (!TryStartSession(Guid.NewGuid().ToString("N"), out string reason)) Debug.LogWarning(reason, this);
        }
    }
}
