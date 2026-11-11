using HeroesOfDragonland.AI;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkEnemy))]
    public sealed class NetworkEnemyPresentation : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private TextMesh label;
        private NetworkEnemy enemy;
        private NetworkCharacterStats stats;
        private Vector3 restPosition;
        private Quaternion restRotation;
        public bool AttackVisible { get; private set; }
        public EnemyState DisplayedState { get; private set; }
        private void Awake()
        {
            enemy = GetComponent<NetworkEnemy>();
            stats = GetComponent<NetworkCharacterStats>();
            if (body != null) { restPosition = body.localPosition; restRotation = body.localRotation; }
        }
        private void LateUpdate()
        {
            if (enemy.Object == null || !enemy.Object.IsValid) return;
            double time = enemy.HasStateAuthority ? enemy.Runner.LocalRenderTime : enemy.Runner.RemoteRenderTime;
            float age = (float)(time - enemy.AttackStartTick * (double)enemy.Runner.DeltaTime);
            AttackVisible = NetworkMatch.AllowsGameplay(enemy.Runner) && !stats.Dead && enemy.AttackSequence > 0 && age >= 0 && age < enemy.AttackWindup
                && (enemy.AttackEndTick == 0 || time < enemy.AttackEndTick * (double)enemy.Runner.DeltaTime);
            DisplayedState = stats.Dead ? EnemyState.Dead : enemy.State;
            if (body != null)
            {
                body.localPosition = stats.Dead ? Vector3.up * 0.45f : restPosition;
                body.localRotation = restRotation * (stats.Dead ? Quaternion.Euler(0, 0, 90)
                    : AttackVisible ? Quaternion.Euler(-20f * age / enemy.AttackWindup, 0, 0) : Quaternion.identity);
            }
            if (label != null)
            {
                label.text = enemy.Definition.name + " / " + Mathf.CeilToInt(stats.Health) + "\n"
                    + (AttackVisible ? enemy.Definition.AttackSet != null && enemy.Definition.AttackSet.Count > 0
                        ? enemy.Definition.AttackSet.Get(enemy.AttackIndex).DisplayName : "WINDUP" : DisplayedState.ToString());
                label.color = AttackVisible ? Color.red : Color.white;
                if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
