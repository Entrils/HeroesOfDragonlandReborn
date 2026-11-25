#if UNITY_EDITOR || DEBUG
using System;
using HeroesOfDragonland.Combat;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public static class NetworkLatencyValidation
    {
        public static void Run()
        {
            const float tick = 1f / 60f;
            foreach (var profile in new[] { "30", "80", "150", "jitter", "burst" })
            {
                var config = NetworkLatencyProfiles.Create(profile);
                var continuity = new ServerInputContinuity();
                var dodge = new AttackPressSequence();
                var parry = new AttackPressSequence();
                var melee = new AttackPressSequence();
                int dodgeHits = 0, parryHits = 0, meleeHits = 0;
                int delay = (int)Math.Round(config.NetworkConditions.DelayMin / (2 * tick));
                for (int frame = 0; frame < 85; frame++)
                {
                    int delivered = frame - delay;
                    if (profile == "jitter" && frame % 4 == 1) delivered--;
                    bool missing = delivered < 0 || (profile == "burst" && frame >= 20 && frame < 40);
                    var input = new NetworkPlayerInput
                    {
                        HasSample = !missing, Block = true, Move = Vector2.right,
                        DodgePress = delivered >= 60 ? 2u : delivered >= 25 ? 1u : 0, ParryPress = delivered >= 60 ? 2u : delivered >= 25 ? 1u : 0,
                        LightAttackPress = delivered >= 60 ? 2u : delivered >= 25 ? 1u : 0
                    };
                    var resolved = continuity.Resolve(input, tick, out bool rebaseline);
                    if (profile == "burst" && frame == 21)
                        Check(resolved.Block && !resolved.SuppressAttacks && resolved.Move == Vector2.right, "short gap continuity");
                    if (profile == "burst" && frame == 35)
                        Check(!resolved.Block && resolved.SuppressAttacks && resolved.Move == Vector2.zero, "long gap stops movement and guard");
                    if (rebaseline) { dodge.Reset(); parry.Reset(); melee.Reset(); }
                    if (!resolved.HasSample) continue;
                    if (dodge.TryRead(resolved.DodgePress, 0, out _)) dodgeHits++;
                    if (parry.TryRead(resolved.ParryPress, 0, out _)) parryHits++;
                    if (melee.TryRead(resolved.LightAttackPress, 0, out _)) meleeHits++;
                }
                int expected = profile == "burst" ? 1 : 2;
                Check(dodgeHits == expected && parryHits == expected && meleeHits == expected, profile + " one-shot counts");
                Debug.Log("[M86] PASS deterministic input delivery " + profile + " (not a transport measurement)");
            }

            var gate = new ServerInputContinuity();
            var suppressed = gate.Resolve(new NetworkPlayerInput
                { HasSample = true, SuppressAttacks = true, Move = Vector2.one, Block = true, Dodge = true }, tick, out _);
            Check(suppressed.Move == Vector2.zero && !suppressed.Block && !suppressed.Dodge, "explicit suppression overrides a held input");
        }

        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("[M86] " + label);
        }
    }
}
#endif
