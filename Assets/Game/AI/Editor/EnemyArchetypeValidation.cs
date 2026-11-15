using System;
using System.Collections.Generic;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.AI.Editor
{
    public static class EnemyArchetypeValidation
    {
        private static int checks;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("[Archetypes] FAIL " + message); checks++; }
        private static EnemyDefinition Load(string path) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        [MenuItem("Heroes of Dragonland/Enemies/Validate In Play Mode")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            checks = 0;
            var objects = new List<GameObject>();
            Vector3 origin = new Vector3(1000, 0, 1000);
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(60, 1, 60), transform = Matrix4x4.TRS(origin - Vector3.up * .5f, Quaternion.identity, Vector3.one), area = 0 } };
            var mesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, new Bounds(origin, new Vector3(64, 8, 64)), Vector3.zero, Quaternion.identity);
            var instance = NavMesh.AddNavMeshData(mesh);
            try
            {
                var targetObject = new GameObject("M46 target"); objects.Add(targetObject);
                targetObject.transform.position = origin + Vector3.forward * 8;
                targetObject.AddComponent<CharacterStats>(); targetObject.AddComponent<EnemyThreatTarget>();
                var collider = targetObject.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2;
                var target = targetObject.GetComponent<EnemyThreatTarget>(); var health = targetObject.GetComponent<CharacterStats>();
                var wolf = Spawn(Load("Assets/Game/Data/Enemies/Wolf.asset"), origin, objects);
                var bear = Spawn(Load("Assets/Game/Data/Enemies/Bear.asset"), origin + Vector3.right * 5, objects);
                var knight = Spawn(Load("Assets/Game/Data/MultiplayerPve/Knight.asset"), origin + Vector3.left * 5, objects);
                var archer = Spawn(Load("Assets/Game/Data/EnemyArchetypes/Archer.asset"), origin, objects);
                var mage = Spawn(Load("Assets/Game/Data/EnemyArchetypes/Mage.asset"), origin, objects);
                // Keep inactive fixtures out of perception and raycasts while exercising one actor.
                wolf.gameObject.SetActive(false); bear.gameObject.SetActive(false); knight.gameObject.SetActive(false); mage.gameObject.SetActive(false);
                Check(wolf.Definition.ChaseSpeed > bear.Definition.ChaseSpeed * 2, "wolf speed");
                Check(bear.Definition.Stats.MaxHealth > wolf.Definition.Stats.MaxHealth, "bear health");
                Check(bear.Definition.AttackWindup > 1 && bear.Definition.StaggerPower >= 75, "bear telegraph and stagger");
                Physics.SyncTransforms(); var attack = archer.GetComponent<EnemyMeleeAttack>();
                Check(attack.TryStart(target), "archer starts at range");
                float before = health.CurrentHealth;
                attack.Tick(.3f); Check(health.CurrentHealth == before, "no damage before release");
                attack.Tick(.36f); Check(health.CurrentHealth == before, "projectile has travel time");
                attack.Tick(.6f); Check(health.CurrentHealth < before, "swept ranged impact");
                float after = health.CurrentHealth; attack.Tick(3); Check(health.CurrentHealth == after, "single impact");
                health.ResetToFull(); Check(attack.TryStart(target), "second shot accepted");
                targetObject.transform.position += Vector3.right * 3; Physics.SyncTransforms(); attack.Tick(2);
                Check(health.CurrentHealth == health.MaxHealth, "committed aim can be dodged");
                attack.Tick(3); targetObject.transform.position = origin + Vector3.forward * 8;
                var wall = new GameObject("M46 wall"); objects.Add(wall); wall.transform.position = origin + Vector3.forward * 4 + Vector3.up;
                wall.AddComponent<BoxCollider>().size = new Vector3(3, 3, .4f); Physics.SyncTransforms();
                Check(!attack.TryStart(target), "obstructed target cannot start"); wall.SetActive(false); Physics.SyncTransforms();
                Check(attack.TryStart(target), "clear line accepts shot"); wall.SetActive(true); Physics.SyncTransforms(); attack.Tick(2);
                Check(health.CurrentHealth == health.MaxHealth, "geometry intercepts projectile"); wall.SetActive(false);
                archer.gameObject.SetActive(false); mage.gameObject.SetActive(true); Physics.SyncTransforms();
                attack = mage.GetComponent<EnemyMeleeAttack>(); var mp = mage.GetComponent<CharacterStats>();
                float mana = mp.CurrentMana; Check(attack.TryStart(target), "mage spell accepted");
                Check(mp.CurrentMana == mana - mage.Definition.SpellManaCost, "spell spends mana");
                attack.Cancel(); attack.Tick(4); mp.TrySpendMana(mp.CurrentMana);
                Check(!attack.TryStart(target), "empty mana rejects spell");
                mp.TickRegeneration(12); Check(attack.TryStart(target), "regeneration restores casting");
                attack.Cancel(); mage.gameObject.SetActive(false);
                knight.gameObject.SetActive(true); knight.GetComponent<NavMeshAgent>().Warp(origin);
                targetObject.transform.position = origin + Vector3.forward; Physics.SyncTransforms();
                knight.GetComponent<DamageReceiver>().TakeDamage(1, targetObject); knight.Tick(.5f);
                Check(knight.GetComponent<CombatDefense>().IsBlocking, "knight guards close target state=" + knight.State + " target=" + knight.AggroTarget + " pos=" + knight.transform.position + " stamina=" + knight.GetComponent<CharacterStats>().CurrentStamina + " visible=" + knight.GetComponent<EnemyPerception>().CanSee(target));
                float knightHp = knight.GetComponent<CharacterStats>().CurrentHealth;
                knight.GetComponent<DamageReceiver>().TakeDamage(10, targetObject);
                Check(knightHp - knight.GetComponent<CharacterStats>().CurrentHealth < 10, "guard mitigates damage");
                knight.Tick(1); Check(!knight.GetComponent<CombatDefense>().IsBlocking && knight.GetComponent<EnemyMeleeAttack>().IsBusy, "knight releases guard to attack");
                knight.gameObject.SetActive(false); archer.gameObject.SetActive(true); archer.transform.position = origin;
                archer.GetComponent<DamageReceiver>().TakeDamage(1, targetObject); archer.Tick(.5f);
                var agent = archer.GetComponent<NavMeshAgent>();
                Check(agent.hasPath && Vector3.Distance(agent.destination, target.NavigationPosition) > 5, "pressured archer repositions away");
                archer.SetAuthority(false); Check(!archer.GetComponent<EnemyMeleeAttack>().TryStart(target), "authority rejects attacks");
                archer.SetAuthority(true); archer.gameObject.SetActive(false);
                targetObject.transform.position = origin + Vector3.forward; health.ResetToFull();
                wolf.gameObject.SetActive(true); wolf.GetComponent<NavMeshAgent>().Warp(origin); Physics.SyncTransforms();
                wolf.GetComponent<DamageReceiver>().TakeDamage(1, targetObject); wolf.Tick(.11f);
                Check(wolf.GetComponent<EnemyMeleeAttack>().IsBusy, "wolf aggressively commits short bite");
                wolf.Tick(.2f);
                Check(health.CurrentHealth < health.MaxHealth && wolf.GetComponent<NavMeshAgent>().hasPath, "wolf bites then circles during recovery");
                wolf.gameObject.SetActive(false); health.ResetToFull();
                bear.gameObject.SetActive(true); bear.GetComponent<NavMeshAgent>().Warp(origin); Physics.SyncTransforms();
                attack = bear.GetComponent<EnemyMeleeAttack>(); Check(attack.TryStart(target), "bear commits heavy attack");
                attack.Tick(.5f); Check(health.CurrentHealth == health.MaxHealth, "bear telegraph leaves dodge window");
                attack.Tick(.7f); Check(health.CurrentHealth < health.MaxHealth && targetObject.GetComponent<CombatDefense>().IsStaggered, "bear impact breaks default poise");
                bear.gameObject.SetActive(false); targetObject.GetComponent<CombatDefense>().Tick(3);
                targetObject.transform.position = origin + Vector3.forward * 8; mage.gameObject.SetActive(true); Physics.SyncTransforms();
                mp.TrySpendMana(mp.CurrentMana); mage.GetComponent<DamageReceiver>().TakeDamage(1, targetObject); mage.Tick(.5f);
                Check(!mage.GetComponent<EnemyMeleeAttack>().IsBusy && mage.GetComponent<NavMeshAgent>().hasPath, "exhausted mage repositions instead of casting");
                mp.TickRegeneration(12); mage.Tick(3);
                Check(mage.GetComponent<EnemyMeleeAttack>().IsBusy, "mage resumes aggression after mana recovery");
                mage.GetComponent<CombatDefense>().ApplyPoiseDamage(1000);
                Check(!mage.GetComponent<EnemyMeleeAttack>().IsBusy, "stagger interrupts spell windup");
                Debug.Log("[Archetypes] PASS " + checks + " gameplay assertions.");
            }
            finally
            {
                foreach (var go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                instance.Remove(); UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        private static EnemyBrain Spawn(EnemyDefinition definition, Vector3 position, List<GameObject> objects)
        {
            var go = new GameObject("M46 " + definition.name); go.SetActive(false); objects.Add(go); go.transform.position = position;
            go.AddComponent<CharacterStats>(); var agent = go.AddComponent<NavMeshAgent>(); agent.enabled = false;
            var brain = go.AddComponent<EnemyBrain>();
            Check(brain.TryConfigureSpawn(definition, true), "configure " + definition.name);
            go.SetActive(true); agent.enabled = true; return brain;
        }
    }
}
