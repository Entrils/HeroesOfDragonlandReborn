#if UNITY_EDITOR || DEBUG
using System;
using System.Reflection;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    internal static class NetworkLatencyProfiles
    {
        internal static NetworkProjectConfig FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-hodrLatency");
            if (index < 0) return null;
            if (index + 1 >= args.Length)
                throw new ArgumentException("Supply 30, 80, 150, jitter, or burst after -hodrLatency.");
            var config = Create(args[index + 1]);
            Debug.Log("[LatencyProfile] " + args[index + 1] + " enabled via Fusion debug transport");
            return config;
        }

        internal static NetworkProjectConfig Create(string profile)
        {
            if (NetworkRunner.BuildType != NetworkRunner.BuildTypes.Debug)
                throw new InvalidOperationException("Latency profiles require Fusion Debug DLLs.");

            // Fusion 2.1's own copy also preserves its runtime prefab/behavior tables.
            var copier = typeof(NetworkProjectConfig).GetMethod("Copy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (copier?.Invoke(NetworkProjectConfig.Global, null) is not NetworkProjectConfig config)
                throw new InvalidOperationException("The installed Fusion version cannot copy its runner configuration.");
            config.NetworkConditions = new NetworkSimulationConfiguration
            {
                Enabled = true,
                DelayMin = profile switch
                {
                    "30" => .030f,
                    "80" or "jitter" or "burst" => .080f,
                    "150" => .150f,
                    _ => throw new ArgumentException("Unknown latency profile " + profile)
                }
            };
            config.NetworkConditions.DelayMax = config.NetworkConditions.DelayMin;
            if (profile == "jitter") config.NetworkConditions.AdditionalJitter = .040f;
            if (profile == "burst")
            {
                config.NetworkConditions.LossChanceShape = NetConfigSimulationOscillator.WaveShape.Sine;
                config.NetworkConditions.LossChanceMax = 1;
                config.NetworkConditions.LossChancePeriod = 4;
                config.NetworkConditions.LossChanceThreshold = .92f;
            }
            return config;
        }
    }
}
#endif
