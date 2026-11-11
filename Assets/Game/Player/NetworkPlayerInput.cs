using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public struct NetworkPlayerInput : INetworkInput
    {
        public Vector2 Move;
        public NetworkBool Attack;
        public NetworkBool HeavyAttack;
        public uint LightAttackPress;
        public uint HeavyAttackPress;
        public NetworkBool SuppressAttacks;
        public NetworkBool Block;
        public NetworkBool Dodge;
        public NetworkBool LockedOn; // Cosmetic locomotion mode, not target identity or combat permission.
        // Local lock-on supplies facing intent only, never a trusted hit target.
        public Vector2 Facing;
    }
}
