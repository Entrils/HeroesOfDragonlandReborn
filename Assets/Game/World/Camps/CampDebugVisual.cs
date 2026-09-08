using UnityEngine;

namespace HeroesOfDragonland.World.Camps
{
    [RequireComponent(typeof(Camp))]
    public sealed class CampDebugVisual : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        private Camp camp;
        private void Awake() => camp = GetComponent<Camp>();
        private void OnEnable() { camp.Changed += Refresh; Refresh(); }
        private void OnDisable() => camp.Changed -= Refresh;
        private void Refresh()
        {
            if (label == null) return;
            label.text = camp.IsCleared ? "CAMP CLEARED\n+" + camp.AwardedExperience + " XP\nOpen the chest [F]"
                : "NEUTRAL CAMP\nDefeated: " + camp.DeadCount + "/" +
                    (camp.IsInitialized ? camp.Enemies.Count : camp.Definition != null ? camp.Definition.EnemyCount : 0);
            label.color = camp.IsCleared ? Color.green : Color.white;
        }
        private void LateUpdate()
        {
            if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
