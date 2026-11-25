using Fusion;
using UnityEngine;

namespace HeroesOfDragonland.Multiplayer
{
    public struct NetworkPlayerInput : INetworkInput
    {
        public NetworkBool HasSample; // Default input from a missing tick is not an explicit neutral sample.
        public Vector2 Move;
        public NetworkBool Attack;
        public NetworkBool HeavyAttack;
        public uint LightAttackPress;
        public uint HeavyAttackPress;
        public uint ParryPress;
        public NetworkBool SuppressAttacks;
        public NetworkBool Block;
        public uint DodgePress;
        public NetworkBool Dodge; // Held dodge for existing development drivers.
        public NetworkBool LockedOn; // Cosmetic locomotion mode, not target identity or combat permission.
        // Local lock-on supplies facing intent only, never a trusted hit target.
        public Vector2 Facing;
        public Vector3 BowAim;
        public NetworkId BowTarget;
    }
}
