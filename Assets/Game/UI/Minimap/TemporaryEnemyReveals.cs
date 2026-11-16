using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.UI.Minimap
{
    [DisallowMultipleComponent]
    public sealed class TemporaryEnemyReveals : MonoBehaviour
    {
        private readonly EnemyRevealCache cache = new EnemyRevealCache();
        public string Scope { get; private set; }
        public void Bind(string scope, MapTeam audience)
        {
            if (!isActiveAndEnabled) { scope = null; audience = MapTeam.None; }
            Scope = scope; cache.Bind(scope, audience);
        }
        // No producer is installed in M62. A future targeted, server-authorized gameplay
        // adapter must validate recipient/match/round and translate its lifetime to local time.
        public bool ReceiveAuthorizedSnapshot(EnemyRevealSnapshot snapshot) => isActiveAndEnabled && cache.Accept(snapshot, Time.unscaledTimeAsDouble);
        public void Revoke(string token) => cache.Revoke(token);
        public void Append(List<MapMarker> markers) { if (isActiveAndEnabled) cache.Append(markers, Time.unscaledTimeAsDouble); }
        private void OnDisable() => Bind(null, MapTeam.None);
    }
}
