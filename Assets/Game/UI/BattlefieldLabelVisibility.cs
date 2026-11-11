using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    // Overhead map/debug text is not part of the third-person combat HUD.
    [DefaultExecutionOrder(500), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class BattlefieldLabelVisibility : MonoBehaviour
    {
        private TextMesh[] labels = System.Array.Empty<TextMesh>();
        private float nextScan;
        private Camera view;
        private void Awake() => view = GetComponent<Camera>();
        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                labels = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
                nextScan = Time.unscaledTime + .5f;
            }
            foreach (var label in labels)
            {
                if (label == null || !label.TryGetComponent<Renderer>(out var renderer)) continue;
                var enemy = label.GetComponentInParent<NetworkEnemyPresentation>();
                bool warning = enemy != null && enemy.AttackVisible && Vector3.Distance(transform.position, label.transform.position) < 14;
                var point = view.WorldToViewportPoint(label.transform.position);
                warning &= point.z > 0 && point.x > .1f && point.x < .9f && point.y > .1f && point.y < .9f;
                renderer.enabled = warning;
                if (!warning) continue;
                var actor = enemy.GetComponent<NetworkEnemy>();
                label.text = actor.Definition.AttackSet != null && actor.Definition.AttackSet.Count > 0
                    ? actor.Definition.AttackSet.Get(actor.AttackIndex).DisplayName : "ATTACK";
                label.characterSize = .065f;
            }
        }
    }
}
