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
            label.text = (camp.State == NetworkCampState.Pending || camp.State == NetworkCampState.Cleared) && remaining > 0
                ? "CAMP IN " + Mathf.CeilToInt(remaining) + "s"
                : camp.State == NetworkCampState.Cleared ? "CAMP CLEARED / XP " + camp.AwardedExperience
                : "NEUTRAL CAMP / " + camp.DeadCount + "/" + camp.EnemyCount;
            label.color = camp.State == NetworkCampState.Cleared ? Color.green : Color.white;
            if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
