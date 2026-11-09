using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public enum EnemySpawnZoneEntry { Allow, BlockEntry }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Spawn Zone Rules")]
    public sealed class SpawnZoneRules : ScriptableObject
    {
        [SerializeField] private bool protectedZone;
        [SerializeField] private EnemySpawnZoneEntry enemyPlayerEntry;
        public bool ProtectedZone => protectedZone;
        public EnemySpawnZoneEntry EnemyPlayerEntry => enemyPlayerEntry;
    }
}
