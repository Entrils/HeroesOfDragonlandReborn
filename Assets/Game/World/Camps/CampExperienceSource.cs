using System;
using HeroesOfDragonland.Progression;
using UnityEngine;

namespace HeroesOfDragonland.World.Camps
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camp))]
    public sealed class CampExperienceSource : MonoBehaviour
    {
        [Tooltip("Explicit reward recipients. Each receives the full camp XP; no proximity/party rules yet.")]
        [SerializeField] private SessionProgression[] recipients = Array.Empty<SessionProgression>();
        private Camp camp;
        private string[] sessionIds;
        private SessionProgression[] boundRecipients;
        private void Awake()
        {
            camp = GetComponent<Camp>();
            boundRecipients = recipients == null ? Array.Empty<SessionProgression>() : (SessionProgression[])recipients.Clone();
            // Bind this camp incarnation to its original match; do not replay an old
            // cleared camp into a new session merely because a player was reset.
            sessionIds = new string[boundRecipients.Length];
            for (int i = 0; i < boundRecipients.Length; i++)
                sessionIds[i] = boundRecipients[i] != null ? boundRecipients[i].SessionId : null;
        }
        private void OnEnable()
        {
            camp.ExperienceAwarded += OnReward;
            TryDeliverReward();
        }
        private void OnDisable() => camp.ExperienceAwarded -= OnReward;
        private void OnReward(CampExperienceReward reward) => TryDeliverReward();

        // Also supports retry after a temporarily disabled source/recipient authority.
        // Per-player reward IDs prevent duplicate grants, including duplicate adapters.
        public void TryDeliverReward()
        {
            if (!isActiveAndEnabled || !camp.HasAuthority || !camp.IsCleared || camp.AwardedExperience <= 0) return;
            for (int i = 0; i < boundRecipients.Length; i++)
                if (boundRecipients[i] != null)
                    boundRecipients[i].TryAddExperience(camp.AwardedExperience, sessionIds[i], "camp:" + camp.Id, out _);
        }
    }
}
