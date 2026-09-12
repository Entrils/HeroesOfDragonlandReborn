using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;

internal class CommandScript : IRunCommand
{
    private int checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); checks++; }
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) throw new Exception("Use Play Mode");
        Check(NavMesh.SamplePosition(new Vector3(25, 0, 25), out var home, 2f, NavMesh.AllAreas), "test NavMesh");
        foreach (string profile in new[] { "Wolf", "Bear", "Bandit" })
        {
            var container = new GameObject("Temporary PvE offline validation");
            result.RegisterObjectCreation(container); container.SetActive(false);
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Game/Data/Enemies/" + profile + ".asset");
                var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/AI/Prefabs/TestBandit.prefab"), home.position, Quaternion.identity, container.transform);
                actor.SetActive(true);
                var brain = actor.GetComponent<EnemyBrain>();
                Check(brain.TryConfigureSpawn(definition, true), "configure " + profile);
                var target = new GameObject("Target"); target.transform.SetParent(container.transform);
                target.transform.position = home.position + Vector3.forward * 1.1f;
                var targetStats = target.AddComponent<CharacterStats>();
                target.AddComponent<DamageReceiver>(); var threat = target.AddComponent<EnemyThreatTarget>();
                target.AddComponent<CapsuleCollider>().center = Vector3.up;
                container.SetActive(true); Physics.SyncTransforms();
                var stats = actor.GetComponent<CharacterStats>();
                var attack = actor.GetComponent<EnemyMeleeAttack>();
                Check(stats.MaxHealth == definition.Stats.MaxHealth && actor.GetComponent<NavMeshAgent>().isOnNavMesh, "stats and navigation");
                brain.Tick(0.01f);
                Check(brain.AggroTarget == threat, "acquire target " + profile + " authority=" + brain.HasAuthority
                    + " targetAlive=" + threat.IsAlive + " visible=" + actor.GetComponent<EnemyPerception>().CanSee(threat)
                    + " state=" + brain.State + " actor=" + actor.transform.position + " target=" + threat.NavigationPosition);
                brain.SetAuthority(false);
                Check(attack.TryStart(threat), "manual offline attack");
                attack.Tick(definition.AttackWindup - 0.01f); Check(targetStats.CurrentHealth == 100, "windup");
                attack.Tick(0.02f); Check(targetStats.CurrentHealth == 100 - definition.Damage, "one legal hit");
                attack.Tick(0.01f); Check(targetStats.CurrentHealth == 100 - definition.Damage && !attack.TryStart(threat), "no duplicate and cooldown");
                attack.Tick(definition.AttackCooldown + 1);
                Check(attack.TryStart(threat), "next attack");
                target.transform.position += Vector3.forward * 10; Physics.SyncTransforms();
                attack.Tick(definition.AttackWindup + 1); Check(targetStats.CurrentHealth == 100 - definition.Damage, "range checked at impact");
                target.transform.position = home.position + Vector3.forward * 1.1f;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(container.transform);
                wall.transform.position = home.position + new Vector3(0, 1, 0.5f); wall.transform.localScale = new Vector3(3, 3, 0.1f);
                Physics.SyncTransforms(); Check(!attack.TryStart(threat), "wall blocks attack");
                wall.SetActive(false); result.DestroyObject(wall); Physics.SyncTransforms();
                brain.SetAuthority(true); targetStats.ApplyDamage(1000); brain.Tick(0.1f);
                Check(brain.AggroTarget == null, "dead target released");
                int deaths = 0; stats.Died += () => deaths++;
                stats.ApplyDamage(10000); stats.ApplyDamage(10000);
                Check(deaths == 1 && brain.State == EnemyState.Dead && !actor.GetComponent<NavMeshAgent>().enabled
                    && !actor.GetComponent<Collider>().enabled && !attack.TryStart(threat), "death exactly once stops AI");
            }
            finally { container.SetActive(false); result.DestroyObject(container); }
        }
        result.Log("PASS: " + checks + " offline wolf/bear/humanoid AI, timing, geometry and death checks");
    }
}
