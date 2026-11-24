using HeroesOfDragonland.AI;
using HeroesOfDragonland.Multiplayer;
using UnityEngine;

namespace HeroesOfDragonland.UI
{
    [DefaultExecutionOrder(110), DisallowMultipleComponent, RequireComponent(typeof(GameplayHudPresenter))]
    public sealed class BossHealthPresenter : MonoBehaviour
    {
        private GameplayHudPresenter hud;
        private GameplayHudView view;
        private void Awake() { hud = GetComponent<GameplayHudPresenter>(); view = GetComponent<GameplayHudView>(); }
        private void LateUpdate()
        {
            var local = hud.LocalActor;
            NetworkEnemy selected = null;
            float nearest = float.PositiveInfinity;
            if (local != null && local.IsValid && local.GetComponent<NetworkCharacterStats>()?.Dead != true
                && NetworkMatch.AllowsGameplay(local.Runner))
                foreach (var boss in BossEncounter.Active)
                {
                    if (boss == null) continue;
                    var enemy = boss.GetComponent<NetworkEnemy>();
                    if (enemy == null || enemy.Object == null || !enemy.Object.IsValid || enemy.Runner != local.Runner || enemy.Definition.Boss == null
                        || enemy.GetComponent<NetworkCharacterStats>().Dead) continue;
                    float distance = (enemy.transform.position - local.transform.position).sqrMagnitude;
                    if (distance > enemy.Definition.Boss.HealthBarDistance * enemy.Definition.Boss.HealthBarDistance || distance >= nearest) continue;
                    selected = enemy; nearest = distance;
                }
            if (selected == null) { view.SetBoss(false); return; }
            var stats = selected.GetComponent<NetworkCharacterStats>();
            string status = selected.BossPhase == BossPhase.Transition ? "ENRAGING - prepare for shockwaves"
                : selected.BossPhase == BossPhase.PhaseTwo ? "PHASE 2 - ENRAGED" : "PHASE 1";
            if (selected.State == EnemyState.Return) status = "RESETTING";
            string move = selected.AttackBusy && selected.PresentedAttackSet != null ? selected.PresentedAttackSet.Get(selected.AttackIndex).DisplayName : "";
            view.SetBoss(true, selected.Definition.Boss.DisplayName, status, stats.Health, stats.MaxHealth, move);
        }
        private void OnDisable() { if (view != null) view.SetBoss(false); }
    }
}
