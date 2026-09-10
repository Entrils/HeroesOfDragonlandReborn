using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public struct NetworkPlayerInput : INetworkInput
    {
        public Vector2 Move;
        public NetworkBool Attack;
        public NetworkBool Dodge;
        // Local lock-on supplies facing intent only, never a trusted hit target.
        public Vector2 Facing;
    }
}
