using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkCamp))]
    public sealed class NetworkCampPresentation : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        private NetworkCamp camp;
        private void Awake() => camp = GetComponent<NetworkCamp>();
        private void LateUpdate()
        {
            if (label == null || camp.Object == null || !camp.Object.IsValid) return;
            float remaining = camp.NextSpawnTimer.RemainingTime(camp.Runner) ?? 0;
            string tier = camp.Definition == null ? "NEUTRAL CAMP" : camp.Definition.Difficulty == HeroesOfDragonland.World.Camps.CampDifficulty.Contested
                ? "CONTESTED" : "TIER " + ((int)camp.Definition.Difficulty + 1);
            string status;
            if (camp.State == NetworkCampState.Cleared)
            {
                bool chestAvailable = camp.ChestId != default && camp.Runner.TryFindObject(camp.ChestId, out var reward)
                    && reward != null && reward.TryGetComponent<NetworkLootChest>(out var chest) && !chest.IsOpen;
                status = chestAvailable ? "Loot chest available" : "Cleared";
                if (remaining > 0) status += " · Returns in " + Mathf.CeilToInt(remaining) + "s";
            }
            else if (camp.State == NetworkCampState.Pending) status = "Opens in " + Mathf.CeilToInt(remaining) + "s";
            else if (camp.State == NetworkCampState.Faulted) status = "Unavailable";
            else status = (camp.EnemyCount - camp.DeadCount) + " enemies left · " + camp.AvailableExperience + " XP";
            label.text = tier + "  /  " + status;
            label.color = camp.State == NetworkCampState.Cleared ? Color.green : Color.white;
            if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
