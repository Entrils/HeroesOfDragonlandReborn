using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Fusion;
using HeroesOfDragonland.AI;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Skills;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
using UnityEngine.AI;
#endif

namespace HeroesOfDragonland.World
{
    // Opt-in development acceptance; ordinary matches never install this fixture.
    public sealed class FortressAssaultNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrAssaultCheck")) return;
            var bootstrap = FindAnyObjectByType<MultiplayerBootstrap>();
            if (bootstrap != null) bootstrap.gameObject.AddComponent<FortressAssaultNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private float began = -1, secondBegan = -1, bothBreachedAt = -1, nextRequest, nextReport;
        private bool initialized, friendlyA, friendlyB, seedA, seedB, denied, finished, complete;
        private int firstRound;
        private NetworkId originalA, originalB;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("[AssaultNetwork] FAIL " + label); }
        private void LateUpdate()
        {
            var runner = bootstrap.Runner; var match = bootstrap.GetComponent<NetworkFortressSpawner>().Match;
            if (complete || runner == null || !runner.IsRunning || match == null || match.Object == null || !match.Object.IsValid) return;
            var humans = FindObjectsByType<NetworkTestPlayer>().Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length != 2 || (!initialized && !match.CanPlay)) return;
            var gates = FindObjectsByType<NetworkFortressGate>().Where(g => g.Object != null && g.Object.IsValid && g.Runner == runner).ToArray();
            if (finished)
            {
                if (match.RoundNumber > firstRound && match.CanPlay && gates.Length == 2)
                {
                    foreach (var gate in gates) Check(!gate.IsOpen && gate.DestructionCount == 0 && gate.GetComponent<NetworkCharacterStats>().Health == 600
                        && gate.GetComponent<BoxCollider>().enabled && gate.GetComponent<NavMeshObstacle>().enabled, "round reset restores closed full-health gates");
                    Check(gates.All(g => g.Object.Id != originalA && g.Object.Id != originalB), "old round gates removed");
                    complete = true; Debug.Log("[AssaultNetwork] PASS friendly/enemy spell damage on both teams, server-only gate health, replicated breach and collision, single destruction and full round reset; server=" + runner.IsServer);
                }
                return;
            }
            if (!match.CanPlay || gates.Length != 2) return;
            var aGate = gates.Single(g => g.Team == TeamId.TeamA); var bGate = gates.Single(g => g.Team == TeamId.TeamB);
            if (!initialized)
            {
                initialized = true; firstRound = match.RoundNumber; originalA = aGate.Object.Id; originalB = bGate.Object.Id; began = Time.realtimeSinceStartup;
                Check(FindObjectsByType<NetworkFortressGuard>().Count(g => g.GetComponent<NetworkEnemy>().Object != null && g.GetComponent<NetworkEnemy>().Object.IsValid && g.GetComponent<NetworkEnemy>().Runner == runner) == 8, "eight defensive posts");
                if (runner.IsServer)
                {
                    foreach (var avatar in bootstrap.Avatars) if (avatar.GetComponent<NetworkTestPlayer>().IsBot) { avatar.GetComponent<NetworkTestPlayer>().enabled = false; avatar.GetComponent<MeleeAttack>().enabled = false; }
                    foreach (var enemy in FindObjectsByType<NetworkEnemy>().Where(e => e.Object != null && e.Object.IsValid && e.Runner == runner)) enemy.GetComponent<EnemyBrain>().enabled = false;
                    foreach (var human in humans)
                    {
                        var skills = human.GetComponent<SessionSkills>(); Check(skills.TryLearn(3, skills.SessionId, out _), "learn normal spell");
                        bool a = human.Team == TeamId.TeamA;
                        human.GetComponent<NetworkTransform>().Teleport(new Vector3(-21, 1, a ? -1.2f : 1.2f), Quaternion.LookRotation(Vector3.left));
                    }
                }
            }
            float age = Time.realtimeSinceStartup - began;
            if (!runner.IsServer && !denied)
            {
                float health = aGate.GetComponent<CharacterStats>().CurrentHealth;
                Check(!aGate.CanMutateGate && aGate.GetComponent<CharacterStats>().ApplyDamage(10000) == 0
                    && aGate.GetComponent<CharacterStats>().CurrentHealth == health, "replica health mutation denied"); denied = true;
            }
            if (runner.IsServer && !friendlyA && age > 3)
            { Check(aGate.GetComponent<NetworkCharacterStats>().Health == 600, "Team A cannot damage own gate"); friendlyA = true; }
            if (runner.IsServer && !seedA && age > 7)
            {
                var health = aGate.GetComponent<CharacterStats>(); Check(health.CurrentHealth < 600 && health.CurrentHealth > 0, "Team B damages Team A gate");
                health.ApplyDamage(health.CurrentHealth - 20); seedA = true;
            }
            if (aGate.IsOpen && secondBegan < 0)
            {
                secondBegan = Time.realtimeSinceStartup;
                Check(aGate.DestructionCount == 1 && !aGate.GetComponent<BoxCollider>().enabled && !aGate.GetComponent<NavMeshObstacle>().enabled, "replicated Team A breach removes blockers");
                if (runner.IsServer) foreach (var human in humans)
                {
                    bool a = human.Team == TeamId.TeamA;
                    human.GetComponent<NetworkTransform>().Teleport(new Vector3(21, 1, a ? -1.2f : 1.2f), Quaternion.LookRotation(Vector3.right));
                }
            }
            float secondAge = secondBegan < 0 ? -1 : Time.realtimeSinceStartup - secondBegan;
            if (runner.IsServer && !friendlyB && secondAge > 7)
            { Check(bGate.GetComponent<NetworkCharacterStats>().Health == 600, "Team B cannot damage own gate"); friendlyB = true; }
            if (runner.IsServer && !seedB && secondAge > 11.5f)
            {
                var health = bGate.GetComponent<CharacterStats>(); Check(health.CurrentHealth < 600 && health.CurrentHealth > 0, "Team A damages Team B gate");
                health.ApplyDamage(health.CurrentHealth - 20); seedB = true;
            }
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
            {
                bool a = local.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA;
                bootstrap.TestInput = Vector2.zero; bootstrap.TestFacing = new Vector2(secondBegan < 0 ? -1 : 1, 0);
                bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestBlock = bootstrap.TestParry = bootstrap.TestDodge = false;
                bool request = secondBegan < 0 ? a ? age > .8f && age < 2.5f : age > 3.8f
                    : a ? secondAge > 7.8f : secondAge > 4.5f && secondAge < 6.5f;
                var skills = local.GetComponent<SessionSkills>();
                if (request && Time.realtimeSinceStartup >= nextRequest && skills.IsLearned(3) && skills.CooldownRemaining(3) <= 0)
                { local.GetComponent<NetworkSessionSkills>().RequestActivate(3); nextRequest = Time.realtimeSinceStartup + 1; }
            }
            // Keep the breached snapshot alive long enough for a background client to
            // render it before the normal round reset despawns these gate instances.
            if (bGate.IsOpen && bothBreachedAt < 0) bothBreachedAt = Time.realtimeSinceStartup;
            if (bGate.IsOpen && (!runner.IsServer || Time.realtimeSinceStartup - bothBreachedAt > 20))
            {
                Check(aGate.IsOpen && aGate.DestructionCount == 1 && bGate.DestructionCount == 1 && !bGate.GetComponent<BoxCollider>().enabled && !bGate.GetComponent<NavMeshObstacle>().enabled, "both gates breached once");
                Check(runner.IsServer || denied, "replica check performed"); finished = true;
                if (runner.IsServer)
                {
                    Check(friendlyA && friendlyB && seedA && seedB, "both team policies exercised");
                    Check(runner.TryFindObject(match.TeamAKingId, out var king), "King exists"); king.GetComponent<CharacterStats>().ApplyDamage(100000); // Normal death -> match finish -> reset pipeline.
                }
            }
            if (age > 150) { complete = true; Check(false, "encounter timeout"); }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 4;
                Debug.Log("[AssaultNetwork] server=" + runner.IsServer + " age=" + age.ToString("F1") + " A=" + aGate.GetComponent<NetworkCharacterStats>().Health + "/" + aGate.IsOpen + " B=" + bGate.GetComponent<NetworkCharacterStats>().Health + "/" + bGate.IsOpen + " round=" + match.RoundNumber);
            }
        }
        private void OnDisable()
        {
            if (bootstrap == null) return; bootstrap.TestInput = bootstrap.TestFacing = null;
            bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestBlock = bootstrap.TestParry = bootstrap.TestDodge = null;
        }
#endif
    }
}
