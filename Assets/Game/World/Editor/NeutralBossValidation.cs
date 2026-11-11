using System;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Targeting;
using HeroesOfDragonland.Teams;
using HeroesOfDragonland.World.Camps;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HeroesOfDragonland.World.Editor
{
    public static class NeutralBossValidation
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception("Boss validation: " + message); }
        private static GameObject Player(Transform parent, string name, TeamId team, Vector3 position)
        {
            var actor = new GameObject(name); actor.transform.SetParent(parent); actor.transform.position = position;
            actor.AddComponent<CharacterStats>(); actor.AddComponent<DamageReceiver>(); actor.AddComponent<EnemyThreatTarget>();
            actor.AddComponent<CapsuleCollider>().center = Vector3.up;
            actor.AddComponent<TeamIdentity>().TryConfigure(team, CombatantKind.Player);
            return actor;
        }

        public static void Validate()
        {
            Check(Application.isPlaying, "Run in Play Mode");
            const string folder = BattlefieldSceneBuilder.Folder;
            var camp = AssetDatabase.LoadAssetAtPath<NetworkCampDefinition>(folder + "/BossCamp.asset");
            Check(camp.TryValidate(out var reason), reason);
            Check(camp.Enemies.Count == 1 && camp.ExperienceReward == 300, "Single high value boss");
            var saved = camp.Enemies[0].Prefab.gameObject;
            Check(saved.GetComponent<Targetable>() != null && saved.GetComponent<BossTelegraphPresentation>() != null, "Target and warning components");
            Check(saved.GetComponent<NetworkFortressGuard>() == null && (TeamIdentity.Find(saved) == null || !TeamIdentity.Find(saved).HasTeam), "Neutral, no fortress affiliation");
            var definition = saved.GetComponent<EnemyBrain>().Definition;
            Check(definition.Stats.MaxHealth == 1800 && definition.AttackSet.Count == 3, "Boss HP and three attacks");
            var container = new GameObject("Temporary boss combat validation"); container.SetActive(false);
            try
            {
                Check(NavMesh.SamplePosition(Vector3.zero, out var home, 1, NavMesh.AllAreas), "Center NavMesh");
                // Exercise the shared combat code without requiring a live Fusion room.
                var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/AI/Prefabs/TestBandit.prefab"), home.position, Quaternion.identity, container.transform);
                var brain = actor.GetComponent<EnemyBrain>();
                Check(brain.TryConfigureSpawn(definition, true), "Configure shared enemy AI");
                var a = Player(container.transform, "Team A attacker", TeamId.TeamA, home.position + Vector3.forward * 3);
                var b = Player(container.transform, "Team B invader", TeamId.TeamB, home.position + Vector3.back * 3);
                container.SetActive(true); Physics.SyncTransforms();
                var attack = actor.GetComponent<EnemyMeleeAttack>();
                var receiver = actor.GetComponent<DamageReceiver>();
                var sa = a.GetComponent<CharacterStats>(); var sb = b.GetComponent<CharacterStats>();
                var ta = a.GetComponent<EnemyThreatTarget>(); var tb = b.GetComponent<EnemyThreatTarget>();
                Check(actor.GetComponent<Targetable>().IsValid && actor.GetComponent<CharacterStats>().MaxHealth == 1800, "Alive and targetable");
                receiver.TakeDamage(1, a); Check(brain.AggroTarget == ta, "Team A gains aggro");
                Check(attack.TryStart(ta) && attack.AttackIndex == 0, "Cleave starts");
                Vector3 direction = attack.AttackForward;
                receiver.TakeDamage(1, b);
                Check(brain.AggroTarget == tb && attack.IsBusy && attack.AttackForward == direction, "Team B steals aggro without redirecting warning");
                attack.Tick(attack.Windup - .01f); Check(sa.CurrentHealth == 100 && sb.CurrentHealth == 100, "No early damage");
                attack.Tick(.02f); Check(sa.CurrentHealth == 66 && sb.CurrentHealth == 100, "Cleave hits committed sector only");
                attack.Tick(.01f); Check(sa.CurrentHealth == 66 && !attack.TryStart(ta), "No duplicate hit or cooldown bypass");
                attack.Tick(5); sa.ResetToFull(); sb.ResetToFull();
                b.transform.position = home.position + new Vector3(-2, 0, 2); Physics.SyncTransforms();
                Check(attack.TryStart(ta) && attack.AttackIndex == 1, "Sweep starts");
                attack.Tick(attack.Windup + .01f); Check(sa.CurrentHealth == 72 && sb.CurrentHealth == 72, "Sweep damages both teams once");
                attack.Tick(5); sa.ResetToFull(); sb.ResetToFull();
                b.transform.position = home.position + Vector3.back * 3; Physics.SyncTransforms();
                Check(attack.TryStart(ta) && attack.AttackIndex == 2, "Stomp starts");
                attack.Tick(attack.Windup + .01f); Check(sa.CurrentHealth == 60 && sb.CurrentHealth == 60, "Stomp hits both sides and teams");
                attack.Tick(5); sa.ResetToFull(); sb.ResetToFull();
                Check(attack.TryStart(ta) && attack.AttackIndex == 0, "Attack cycle repeats");
                a.transform.position = home.position + Vector3.forward * 8; Physics.SyncTransforms();
                attack.Tick(attack.Windup + .01f); Check(sa.CurrentHealth == 100, "Leaving telegraph avoids impact");
                attack.Tick(5); a.transform.position = home.position + Vector3.forward * 3; Physics.SyncTransforms();
                Check(attack.TryStart(ta), "Cancellable attack starts"); attack.Cancel(); attack.Tick(5);
                Check(sa.CurrentHealth == 100, "Cancelled windup has no damage");
                Check(attack.TryStart(ta), "Start before obstruction");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(container.transform);
                wall.transform.position = home.position + new Vector3(0, 1.5f, 1.5f);
                wall.transform.localScale = new Vector3(6, 4, .2f); Physics.SyncTransforms();
                attack.Tick(attack.Windup + .01f);
                Check(sa.CurrentHealth == 100, "Obstacle blocks damage at impact");
                wall.SetActive(false); Physics.SyncTransforms();
                attack.Tick(5);
                var regular = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(folder + "/HighKnight.asset");
                attack.Configure(regular);
                a.transform.position = home.position + Vector3.forward * 1.4f; Physics.SyncTransforms();
                Check(!attack.UsesAttackSet && attack.TryStart(ta), "Regular enemy retains original attack");
                attack.Tick(regular.AttackWindup - .01f); Check(sa.CurrentHealth == 100, "Regular windup unchanged");
                attack.Tick(.02f); Check(sa.CurrentHealth == 100 - regular.Damage, "Regular single target damage unchanged");
                attack.Configure(definition);
                Check(TeamIdentity.CanDamage(a, b) && TeamIdentity.CanDamage(b, a), "PvP remains available during boss combat");
                brain.SetAuthority(false); Check(!brain.HasAuthority, "Simulation authority disabled");
                var state = brain.State; brain.Tick(10); Check(brain.State == state, "Non-authority brain does not advance");
                brain.SetAuthority(true);
                var stats = actor.GetComponent<CharacterStats>(); stats.ApplyDamage(10000);
                Check(brain.State == EnemyState.Dead && !actor.GetComponent<Targetable>().IsValid && !attack.TryStart(ta), "Death disables boss and targeting");
                Debug.Log("[NeutralBossValidation] PASS neutral identity, targetability, 1800 HP, aggro across teams, committed windups, three AoEs, evasion, cooldown, cancellation, death and PvP rules.");
            }
            finally { container.SetActive(false); UnityEngine.Object.DestroyImmediate(container); }
        }
    }
}
