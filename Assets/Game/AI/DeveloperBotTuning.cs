#if UNITY_EDITOR || DEBUG
using System;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public enum DeveloperBotDifficulty { Recruit, Standard, Veteran }

    [Serializable]
    public sealed class DeveloperBotTuning
    {
        [Min(4)] public float sightRange = 25;
        [Range(60, 360)] public float sightArc = 250;
        [Min(.1f)] public float decisionInterval = .45f;
        [Min(.05f)] public float reactionSeconds = .18f;
        [Min(0)] public float lastSeenSeconds = 2;
        [Range(.05f, .6f)] public float retreatHealth = .27f;
        [Range(0, 1)] public float heavyChance = .24f;
        [Range(0, 1)] public float dodgeChance = .45f;
        [Range(0, 1)] public float parryChance = .12f;
        [Range(0, 1)] public float abilityChance = .6f;
        [Min(.1f)] public float attackInterval = .29f;
        [Min(1)] public int soloBossLevel = 6;

        internal static DeveloperBotTuning Recruit() => new()
        {
            sightRange = 19, sightArc = 215, decisionInterval = .65f, reactionSeconds = .31f,
            heavyChance = .13f, dodgeChance = .24f, parryChance = .035f, abilityChance = .35f,
            retreatHealth = .34f, attackInterval = .42f, soloBossLevel = 8
        };

        internal static DeveloperBotTuning Veteran() => new()
        {
            sightRange = 29, sightArc = 275, decisionInterval = .33f, reactionSeconds = .12f,
            heavyChance = .34f, dodgeChance = .58f, parryChance = .21f, abilityChance = .8f,
            retreatHealth = .23f, attackInterval = .24f, soloBossLevel = 5
        };
    }
}
#endif
