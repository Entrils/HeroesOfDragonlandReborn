using UnityEngine;

namespace HeroesOfDragonland.AI
{
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyDebugVisual : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        [SerializeField] private Transform body;
        private EnemyBrain brain;
        private EnemyMeleeAttack attack;
        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            attack = GetComponent<EnemyMeleeAttack>();
        }
        private void OnEnable()
        {
            brain.StateChanged += Refresh;
            attack.AttackStarted += RefreshAttack;
            attack.AttackFinished += RefreshAttack;
            Refresh(brain.State);
        }
        private void OnDisable()
        {
            brain.StateChanged -= Refresh;
            attack.AttackStarted -= RefreshAttack;
            attack.AttackFinished -= RefreshAttack;
        }
        private void RefreshAttack() => Refresh(brain.State);
        private void Refresh(EnemyState state)
        {
            if (label != null)
            {
                string enemyName = brain.Definition != null ? brain.Definition.name : "Enemy";
                label.text = enemyName + "\n" + (attack.IsBusy ? "WINDUP!" : state.ToString());
                label.color = attack.IsBusy ? Color.red : Color.white;
            }
            if (state == EnemyState.Dead && body != null)
            { body.localRotation = Quaternion.Euler(0, 0, 90); body.localPosition = Vector3.up * 0.45f; }
        }
        private void LateUpdate() { if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation; }
    }
}
