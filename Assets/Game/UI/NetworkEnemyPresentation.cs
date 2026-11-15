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
        private LineRenderer missile;
        private Vector3 restPosition;
        private Quaternion restRotation;
        public bool AttackVisible { get; private set; }
        public EnemyState DisplayedState { get; private set; }
        private void Awake()
        {
            enemy = GetComponent<NetworkEnemy>();
            stats = GetComponent<NetworkCharacterStats>();
            if (enemy.Definition.IsRanged)
            {
                var visual = new GameObject("Enemy projectile cue"); visual.transform.SetParent(transform);
                missile = visual.AddComponent<LineRenderer>(); missile.positionCount = 2;
                missile.startWidth = .12f; missile.endWidth = .04f;
                missile.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                missile.startColor = missile.endColor = enemy.Definition.Archetype == EnemyArchetype.Mage ? Color.magenta : Color.yellow;
                missile.enabled = false;
            }
            if (body != null) { restPosition = body.localPosition; restRotation = body.localRotation; }
        }
        private void OnDestroy() { if (missile != null) Destroy(missile.sharedMaterial); }
        private void LateUpdate()
        {
            if (enemy.Object == null || !enemy.Object.IsValid) return;
            double time = enemy.HasStateAuthority ? enemy.Runner.LocalRenderTime : enemy.Runner.RemoteRenderTime;
            float age = (float)(time - enemy.AttackStartTick * (double)enemy.Runner.DeltaTime);
            AttackVisible = NetworkMatch.AllowsGameplay(enemy.Runner) && !stats.Dead && enemy.AttackSequence > 0 && age >= 0 && age < enemy.AttackWindup
                && (enemy.AttackEndTick == 0 || time < enemy.AttackEndTick * (double)enemy.Runner.DeltaTime);
            if (missile != null)
            {
                float distance = (age - enemy.AttackWindup) * enemy.Definition.ProjectileSpeed;
                missile.enabled = NetworkMatch.AllowsGameplay(enemy.Runner) && !stats.Dead && enemy.AttackSequence > 0 && distance >= 0 && distance < enemy.Definition.AttackRange
                    && (enemy.AttackEndTick == 0 || time < enemy.AttackEndTick * (double)enemy.Runner.DeltaTime);
                Vector3 point = enemy.AttackOrigin + Vector3.up + enemy.AttackForward * Mathf.Max(0, distance);
                missile.SetPosition(0, point); missile.SetPosition(1, point - enemy.AttackForward * .6f);
            }
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
                    + (AttackVisible ? enemy.PresentedAttackSet != null && enemy.PresentedAttackSet.Count > 0
                        ? enemy.PresentedAttackSet.Get(enemy.AttackIndex).DisplayName : "WINDUP" : enemy.Blocking ? "BLOCK" : DisplayedState.ToString());
                label.color = AttackVisible ? Color.red : Color.white;
                if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
