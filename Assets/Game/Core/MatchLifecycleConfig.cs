using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/Match Lifecycle")]
    public sealed class MatchLifecycleConfig : ScriptableObject
    {
        [SerializeField, Range(2, NetworkTeamRoster.PlayerCapacity)] private int minimumPlayers = 2;
        [SerializeField, Min(0)] private float startingDelay = 3;
        [SerializeField, Min(0)] private float finishedDelay = 8;
        [SerializeField, Min(0)] private float returningDelay = 1;
        public int MinimumPlayers => Mathf.Clamp(minimumPlayers, 2, NetworkTeamRoster.PlayerCapacity);
        public float StartingDelay => ValidDelay(startingDelay, 3);
        public float FinishedDelay => ValidDelay(finishedDelay, 8);
        public float ReturningDelay => ValidDelay(returningDelay, 1);
        private static float ValidDelay(float value, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0, value);
    }
}
