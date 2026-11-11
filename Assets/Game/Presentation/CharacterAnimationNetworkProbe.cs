using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using HeroesOfDragonland.Combat;
using HeroesOfDragonland.Multiplayer;
using HeroesOfDragonland.Stats;
using HeroesOfDragonland.Teams;
#endif

namespace HeroesOfDragonland.Presentation
{
    // Explicit development acceptance scenario; never installed in ordinary matches.
    public sealed class CharacterAnimationNetworkProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hodrAnimationCheck")) return;
            var bootstrap = FindFirstObjectByType<MultiplayerBootstrap>();
            if (bootstrap != null) bootstrap.gameObject.AddComponent<CharacterAnimationNetworkProbe>();
        }
        private MultiplayerBootstrap bootstrap;
        private readonly HashSet<CharacterMotion> remoteMotions = new();
        private float began = -1, nextReport;
        private bool positioned, staggered, killed, revived, remoteDead, remoteRevived, complete;
        private void Awake() => bootstrap = GetComponent<MultiplayerBootstrap>();
        private void Update()
        {
            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !NetworkMatch.AllowsGameplay(runner)) return;
            var humans = FindObjectsByType<NetworkTestPlayer>(FindObjectsSortMode.None)
                .Where(p => p.Object != null && p.Object.IsValid && p.Runner == runner && !p.IsBot).ToArray();
            if (humans.Length < 2) return;
            if (began < 0) began = Time.realtimeSinceStartup;
            float age = Time.realtimeSinceStartup - began;
            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
            {
                bool teamA = local.GetComponent<NetworkTestPlayer>().Team == TeamId.TeamA;
                int phase = (int)(age / 3);
                bootstrap.TestFacing = age >= 24 ? new Vector2(teamA ? 1 : -1, 0) : Vector2.up;
                bootstrap.TestInput = phase switch {
                    0 => Vector2.up * .35f, 1 => Vector2.up, 2 => Vector2.left,
                    3 => Vector2.right, 4 => Vector2.down, _ => Vector2.zero
                };
                bootstrap.TestLockedOn = phase >= 2 && phase <= 4;
                bootstrap.TestBlock = phase == 5;
                bootstrap.TestAttack = phase == 6 || (phase == 8 && teamA) || (phase == 9 && !teamA);
                bootstrap.TestHeavyAttack = phase == 7;
                bootstrap.TestDodge = phase == 10;
            }
            if (runner.IsServer)
            {
                // Arrangement only. All swings/damage/defense still use normal server components.
                if (age >= 23.5f && !positioned)
                {
                    positioned = true;
                    foreach (var human in humans)
                    {
                        bool teamA = human.Team == TeamId.TeamA;
                        human.GetComponent<NetworkTransform>().Teleport(new Vector3(teamA ? -.7f : .7f, 1, -18), Quaternion.LookRotation(teamA ? Vector3.right : Vector3.left));
                        human.GetComponent<CombatDefense>().ResetState();
                        human.GetComponent<NetworkPlayerCombat>().CancelForRespawn();
                        human.GetComponent<CharacterStats>().RestoreForRespawn(RespawnResourcePolicy.Full, RespawnResourcePolicy.Full);
                    }
                }
                if (age >= 33 && !staggered)
                {
                    staggered = true;
                    foreach (var human in humans) { human.GetComponent<CombatDefense>().ResetState(); human.GetComponent<CombatDefense>().ApplyStagger(10000); }
                }
                if (age >= 36 && !killed)
                {
                    killed = true;
                    foreach (var human in humans) human.GetComponent<CharacterStats>().ApplyDamage(10000);
                }
                // Simulate a future server vertical motor solely in this opt-in test fixture.
                if (age >= 47 && age < 49)
                    foreach (var human in humans)
                    {
                        var position = human.transform.position;
                        position.y = 1 + Mathf.Sin((age - 47) * Mathf.PI / 2) * 2;
                        human.transform.position = position;
                    }
                if (age >= 49 && !revived)
                {
                    revived = true;
                    foreach (var human in humans) { var position = human.transform.position; position.y = 1; human.transform.position = position; }
                }
            }
            foreach (var human in humans.Where(p => !p.HasInputAuthority))
            {
                var visual = human.GetComponent<NetworkCharacterVisual>().Visual;
                remoteMotions.Add(visual.Motion);
                if (visual.State.Dead) remoteDead = true;
                if (remoteDead && !visual.State.Dead) remoteRevived = true;
            }
            var required = new[] { CharacterMotion.Idle, CharacterMotion.Walk, CharacterMotion.Run, CharacterMotion.Backward,
                CharacterMotion.StrafeLeft, CharacterMotion.StrafeRight, CharacterMotion.LightAttack, CharacterMotion.HeavyAttack,
                CharacterMotion.Block, CharacterMotion.HitReaction, CharacterMotion.Stagger, CharacterMotion.Dodge, CharacterMotion.Death,
                CharacterMotion.Jump, CharacterMotion.Fall, CharacterMotion.Land };
            if (!complete && age > 50 && required.All(remoteMotions.Contains) && remoteRevived)
            {
                complete = true;
                Debug.Log("[AnimationNetwork] PASS all 16 real remote motions, free/lock movement, accepted combat, server hit/stagger/death and respawn; server=" + runner.IsServer);
            }
            if (Time.realtimeSinceStartup > nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                Debug.Log("[AnimationNetwork] server=" + runner.IsServer + " age=" + age.ToString("F1") + " missing="
                    + string.Join(",", required.Where(m => !remoteMotions.Contains(m))) + " revive=" + remoteRevived + " complete=" + complete);
            }
        }
        private void OnDisable()
        {
            if (bootstrap == null) return;
            bootstrap.TestInput = bootstrap.TestFacing = null;
            bootstrap.TestAttack = bootstrap.TestHeavyAttack = bootstrap.TestDodge = bootstrap.TestBlock = bootstrap.TestLockedOn = null;
        }
#endif
    }
}
