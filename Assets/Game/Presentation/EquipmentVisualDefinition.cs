using System;
using System.Collections.Generic;
using HeroesOfDragonland.Items;
using UnityEngine;

namespace HeroesOfDragonland.Presentation
{
    [Serializable]
    public sealed class EquipmentVisualPose
    {
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 eulerAngles;
        [SerializeField] private Vector3 scale = Vector3.one;
        public void Apply(Transform target)
        {
            target.localPosition = position;
            target.localRotation = Quaternion.Euler(eulerAngles);
            target.localScale = scale;
        }
        internal bool IsValid => Finite(position) && Finite(eulerAngles) && Finite(scale)
            && scale.x > 0 && scale.y > 0 && scale.z > 0;
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class EquipmentVisualPart
    {
        [Tooltip("Weapons may leave this empty to use the equipped WeaponDefinition's existing prefab.")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private CharacterSocket socket = CharacterSocket.RightHand;
        [Tooltip("LastBone uses the named character socket; armor uses an explicit Humanoid bone.")]
        [SerializeField] private HumanBodyBones bone = HumanBodyBones.LastBone;
        [SerializeField] private EquipmentVisualPose pose = new EquipmentVisualPose();
        public GameObject Prefab => prefab;
        public CharacterSocket Socket => socket;
        public HumanBodyBones Bone => bone;
        public EquipmentVisualPose Pose => pose;
        public Transform Resolve(CharacterVisual visual) => bone == HumanBodyBones.LastBone
            ? visual.GetSocket(socket)
            : visual.Animator != null && visual.Animator.isHuman ? visual.Animator.GetBoneTransform(bone) : null;
    }

    [CreateAssetMenu(menuName = "Heroes of Dragonland/Art/Equipment Visual Definition")]
    public sealed class EquipmentVisualDefinition : ScriptableObject
    {
        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private EquipmentVisualPart[] parts = Array.Empty<EquipmentVisualPart>();
        [SerializeField] private EquipmentVisualPose stowedPose = new EquipmentVisualPose();
        public EquipmentSlot Slot => slot;
        public IReadOnlyList<EquipmentVisualPart> Parts => Array.AsReadOnly(parts ?? Array.Empty<EquipmentVisualPart>());
        public EquipmentVisualPose StowedPose => stowedPose;
        public bool IsWeapon => slot == EquipmentSlot.MainHand || slot == EquipmentSlot.OffHand;
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (slot < EquipmentSlot.MainHand || slot > EquipmentSlot.Legs) reason = "Invalid visual equipment slot.";
            else if (parts == null || parts.Length == 0 || (IsWeapon && parts.Length != 1)) reason = "Visual needs parts; a weapon needs exactly one socket-mounted part.";
            else if (stowedPose == null || !stowedPose.IsValid) reason = "Invalid stowed pose.";
            else foreach (var part in parts)
            {
                if (part == null || part.Pose == null || !part.Pose.IsValid) reason = "Invalid part pose.";
                else if (IsWeapon && (part.Bone != HumanBodyBones.LastBone || part.Socket != (slot == EquipmentSlot.MainHand ? CharacterSocket.RightHand : CharacterSocket.LeftHand)))
                    reason = "Weapons must use their corresponding hand socket.";
                else if (!IsWeapon && (part.Bone == HumanBodyBones.LastBone || !Enum.IsDefined(typeof(HumanBodyBones), part.Bone)))
                    reason = "Rigid armor needs a mapped Humanoid bone.";
                else if (part.Prefab == null && !IsWeapon) reason = "Armor part prefab is missing.";
                else if (part.Prefab != null && !EquipmentVisualSafety.CanSpawn(part.Prefab, out reason)) return false;
                if (reason != null) return false;
            }
            return reason == null;
        }
    }
}
